using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Enums;
using TwitchStressToolkit.Core.Exceptions;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Core.Models;
using TwitchStressToolkit.Infrastructure.Logging;
using TwitchStressToolkit.Infrastructure.Network;

namespace TwitchStressToolkit.Application.Simulation;

public sealed class VirtualClient : IVirtualClient
{
    private readonly ILogger<VirtualClient> _logger;
    private readonly ErrorLogService _errorLog;
    private readonly TwitchIrcClient _ircClient;
    private readonly IChatMessageGenerator _messageGenerator;
    private readonly IProxyManager _proxyManager;
    private readonly ActivityProfile _activityProfile;
    private readonly Random _random = new();

    private BotAccount _account = null!;
    private ProxyInfo? _proxy;
    private string? _channel;
    private CancellationTokenSource? _cts;
    private Task? _workerTask;
    private bool _disposed;
    private readonly object _stateLock = new();
    private volatile bool _isReconnecting;

    public Guid Id { get; }
    public BotAccount Account => _account;
    public ClientState State { get; private set; } = ClientState.Idle;
    public string? CurrentChannel => _channel;

    public event Action<Guid, ClientState>? StateChanged;
    public event Action<Guid, string>? MessageReceived;
    public event Action<Guid, double>? LatencyUpdated;
    public event Action<Guid, string>? ErrorOccurred;

    public VirtualClient(
        ILogger<VirtualClient> logger,
        ErrorLogService errorLog,
        TwitchIrcClient ircClient,
        IChatMessageGenerator messageGenerator,
        IProxyManager proxyManager,
        ActivityProfile activityProfile)
    {
        Id = Guid.NewGuid();
        _logger = logger;
        _errorLog = errorLog;
        _ircClient = ircClient;
        _messageGenerator = messageGenerator;
        _proxyManager = proxyManager;
        _activityProfile = activityProfile;

        _ircClient.MessageReceived += OnMessageReceived;
        _ircClient.Disconnected += OnDisconnected;
        _ircClient.ErrorOccurred += OnError;
    }

    public void SetAccount(BotAccount account)
    {
        _account = account ?? throw new ArgumentNullException(nameof(account));
    }

    public async Task ConnectAsync(string channel, CancellationToken ct)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [VirtualClient] ConnectAsync enter for {_account?.Username ?? "(no account)"} -> #{channel}");
        try
        {
            if (_account == null)
            {
                throw new InvalidOperationException("Account not set. Call SetAccount first.");
            }

            // Prevent concurrent connect/reconnect attempts
            lock (_stateLock)
            {
                if (_cts is { IsCancellationRequested: false })
                {
                    // Already connecting/connected — don't start a second connect cycle
                    _logger.LogDebug("[{Account}] Connect skipped — already connecting/connected", _account.Username);
                    return;
                }

                _channel = channel;
                _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            }

            try
            {
                _proxy = await _proxyManager.GetForAccountAsync(Id);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [VirtualClient] GetForAccountAsync failed for {_account.Username}: {ex}");
                throw;
            }
            if (_proxy is not null)
            {
                _logger.LogInformation("[{Account}] Using proxy {Proxy}", _account.Username, _proxy.Address);
            }

            try
            {
                SetState(ClientState.Connecting);

                if (_proxy is not null)
                {
                    await _ircClient.ConnectWithProxyAsync(
                        "wss://irc-ws.chat.twitch.tv:443",
                        _account.AuthToken,
                        _account.Username,
                        _proxy,
                        _cts.Token);
                }
                else
                {
                    await _ircClient.ConnectAsync(
                        "wss://irc-ws.chat.twitch.tv:443",
                        _account.AuthToken,
                        _account.Username,
                        _cts.Token);
                }

                await _ircClient.JoinChannelAsync(channel, _cts.Token);

                SetState(ClientState.Connected);
                _logger.LogInformation("[{Account}] Connected to #{Channel}", _account.Username, channel);

                // Only start worker loop if not already running
                if (_workerTask is null || _workerTask.IsCompleted)
                {
                    _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token), _cts.Token);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [VirtualClient] Connect failed for {_account.Username}: {ex}");
                SetState(ClientState.Error);
                ErrorOccurred?.Invoke(Id, ex.Message);

                if (_proxy is not null)
                {
                    try { await _proxyManager.MarkFailedAsync(_proxy); }
                    catch (Exception pex)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [VirtualClient] MarkFailedAsync failed for {_account.Username}: {pex}");
                    }
                }

                _logger.LogError(ex, "[{Account}] Failed to connect", _account.Username);
                _errorLog.LogError("VirtualClient.Connect", $"Failed to connect {_account.Username} to {channel}", ex);
                throw new ConnectionException($"Failed to connect to {channel}", channel, _account.Username);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [VirtualClient] ConnectAsync exit WITH ERROR for {_account?.Username ?? "(no account)"}: {ex}");
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        _cts?.Cancel();

        if (_workerTask is not null)
        {
            try { await _workerTask; }
            catch (OperationCanceledException) { }
            _workerTask = null;
        }

        if (_proxy is not null)
        {
            await _proxyManager.ReturnAsync(_proxy);
        }

        await _ircClient.DisposeAsync();
        SetState(ClientState.Disconnected);
    }

    public async Task SendMessageAsync(string message, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_channel))
        {
            throw new InvalidOperationException("No channel joined");
        }

        await _ircClient.SendMessageAsync(_channel, message, ct);
    }

    public async Task ReconnectAsync(CancellationToken ct)
    {
        if (_cts?.IsCancellationRequested == true) return;

        // Prevent concurrent reconnect attempts
        if (Interlocked.CompareExchange(ref _isReconnecting, true, false) == true)
        {
            _logger.LogDebug("[{Account}] Reconnect already in progress, skipping", _account.Username);
            return;
        }

        try
        {
            SetState(ClientState.Reconnecting);

            if (_proxy is not null)
            {
                await _proxyManager.ReturnAsync(_proxy);
                _proxy = await _proxyManager.GetForAccountAsync(Id);
            }

            // Dispose old IRC client before reconnecting
            await _ircClient.DisposeAsync();

            // Exponential backoff: 1s, 2s, 4s, 8s, max 30s
            const int maxAttempts = 10;

            for (int reconnectAttempts = 0; reconnectAttempts < maxAttempts && !ct.IsCancellationRequested; reconnectAttempts++)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, reconnectAttempts), 30));
                await Task.Delay(delay, ct);

                if (_channel is not null)
                {
                    try
                    {
                        await ConnectAsync(_channel, ct);
                        return; // Success
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[{Account}] Reconnect attempt {Attempt} failed", _account.Username, reconnectAttempts + 1);
                    }
                }
            }

            _logger.LogError("[{Account}] Failed to reconnect after {Max} attempts", _account.Username, maxAttempts);
            SetState(ClientState.Error);
        }
        finally
        {
            _isReconnecting = false;
        }
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        try
        {
            SetState(ClientState.Active);

            while (!ct.IsCancellationRequested)
            {
                var delay = _random.Next(_activityProfile.MinDelayMs, _activityProfile.MaxDelayMs);

                await Task.Delay(delay, ct);

                if (_ircClient.IsConnected)
                {
                    var message = await _messageGenerator.GenerateAsync(_activityProfile);
                    await SendMessageAsync(message, ct);
                }
                else
                {
                    // Connection lost — exit the loop, let the engine handle reconnect
                    SetState(ClientState.Disconnected);
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _errorLog.LogError("VirtualClient.WorkerLoop", $"Worker loop error for {_account.Username}", ex);
            ErrorOccurred?.Invoke(Id, ex.Message);
            SetState(ClientState.Error);
        }
    }

    private void OnMessageReceived(string message)
    {
        MessageReceived?.Invoke(Id, message);
    }

    private void OnDisconnected(string reason)
    {
        SetState(ClientState.Disconnected);
    }

    private void OnError(Exception ex)
    {
        SetState(ClientState.Error);
        ErrorOccurred?.Invoke(Id, ex.Message);
    }

    private void SetState(ClientState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(Id, state);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException) { }

        if (_workerTask is not null)
        {
            try { await _workerTask; }
            catch { }
            _workerTask = null;
        }

        _ircClient.MessageReceived -= OnMessageReceived;
        _ircClient.Disconnected -= OnDisconnected;
        _ircClient.ErrorOccurred -= OnError;

        await _ircClient.DisposeAsync();

        try
        {
            _cts?.Dispose();
        }
        catch { }
    }
}
