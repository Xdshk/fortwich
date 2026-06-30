using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Enums;
using TwitchStressToolkit.Core.Exceptions;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Core.Models;
using TwitchStressToolkit.Application.Accounts;
using TwitchStressToolkit.Infrastructure.Logging;

namespace TwitchStressToolkit.Application.Simulation;

public sealed class SimulationEngine : ISimulationEngine
{
    private readonly ILogger<SimulationEngine> _logger;
    private readonly IMetricsCollector _metrics;
    private readonly IProxyManager _proxyManager;
    private readonly IFingerprintManager _fingerprintManager;
    private readonly IAuthService _authService;
    private readonly AccountManager _accountManager;
    private readonly Func<VirtualClient> _clientFactory;

    private readonly ConcurrentDictionary<Guid, VirtualClient> _clients = new();
    private readonly ErrorLogService _errorLog;
    private ConnectionConfig _config = null!;
    private CancellationTokenSource? _cts;
    private volatile bool _isRunning;
    private volatile bool _disposed;

    public Guid Id { get; } = Guid.NewGuid();
    public bool IsRunning => _isRunning;
    public ConnectionConfig Config => _config;
    public SimulationResult? LastResult { get; private set; }

    public event Action<SimulationResult>? SimulationCompleted;
    public event Action<string>? LogGenerated;

    public SimulationEngine(
        ILogger<SimulationEngine> logger,
        IMetricsCollector metrics,
        IProxyManager proxyManager,
        IFingerprintManager fingerprintManager,
        IAuthService authService,
        AccountManager accountManager,
        Func<VirtualClient> clientFactory,
        ErrorLogService errorLog)
    {
        _logger = logger;
        _metrics = metrics;
        _proxyManager = proxyManager;
        _fingerprintManager = fingerprintManager;
        _authService = authService;
        _accountManager = accountManager;
        _clientFactory = clientFactory;
        _errorLog = errorLog;
    }

    public async Task StartAsync(ConnectionConfig config, CancellationToken ct)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [SimulationEngine] StartAsync enter (channel={config.TargetChannel}, max={config.MaxClients})");
        if (_isRunning)
        {
            throw new InvalidOperationException("Simulation already running");
        }

        _config = config;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _isRunning = true;
        _metrics.Reset();

        LogGenerated?.Invoke("Simulation started");

        try
        {
            await RunSimulationInternalAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [SimulationEngine] StartAsync cancelled");
            _logger.LogInformation("Simulation cancelled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [SimulationEngine] StartAsync FATAL: {ex}");
            _logger.LogError(ex, "Simulation failed");
            _errorLog.LogError("SimulationEngine", "Simulation failed", ex);
            _metrics.RecordError(ex.Message);
        }
        finally
        {
            await FinishSimulationAsync();
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [SimulationEngine] StartAsync exit");
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [SimulationEngine] StopAsync enter");
        if (!_isRunning) return;

        _logger.LogInformation("Stopping simulation...");

        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException) { }

        foreach (var client in _clients.Values)
        {
            try
            {
                await client.DisposeAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [SimulationEngine] Error disposing client {client.Id}: {ex}");
                _logger.LogWarning(ex, "Error disposing client {Id}", client.Id);
            }
        }

        _clients.Clear();
        _isRunning = false;
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [SimulationEngine] StopAsync exit");
    }

    public async Task<SimulationResult> RunScenarioAsync(Scenario scenario, CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _isRunning = true;
        _metrics.Reset();

        try
        {
            await RunSimulationInternalAsync(_cts.Token);
        }
        finally
        {
            await FinishSimulationAsync();
        }

        return LastResult ?? await _metrics.GetResultAsync(ct);
    }

    private async Task RunSimulationInternalAsync(CancellationToken ct)
    {
        List<BotAccount> accounts;
        try
        {
            accounts = _accountManager.Accounts.ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [SimulationEngine] RunSimulationInternalAsync: _accountManager.Accounts threw: {ex}");
            throw;
        }

        if (accounts.Count == 0)
        {
            LogGenerated?.Invoke("No accounts loaded! Please add accounts first.");
            _logger.LogWarning("No accounts available");
            return;
        }

        LogGenerated?.Invoke($"Starting with {accounts.Count} accounts");

        var delay = _config.RampUpDelayMs;
        var channel = _config.TargetChannel;

        if (string.IsNullOrEmpty(channel))
        {
            LogGenerated?.Invoke("No target channel specified!");
            return;
        }

        for (int i = 0; i < accounts.Count && !ct.IsCancellationRequested; i++)
        {
            var account = accounts[i];
            var client = _clientFactory();
            client.SetAccount(account);

            client.StateChanged += (id, state) => HandleClientStateChanged(id, state);
            client.ErrorOccurred += (id, error) => HandleClientError(id, error);

            _clients.TryAdd(client.Id, client);

            try
            {
                await client.ConnectAsync(channel, ct);
                _metrics.RecordConnection();
                account.Status = AccountStatus.Active;
                LogGenerated?.Invoke($"[{i + 1}/{accounts.Count}] {account.Username} connected to #{channel}");
            }
            catch (Exception ex)
            {
                _metrics.RecordError(ex.Message);
                account.Status = AccountStatus.Error;
                LogGenerated?.Invoke($"[{i + 1}/{accounts.Count}] {account.Username} failed: {ex.Message}");
            }

            if (delay > 0)
            {
                await Task.Delay(delay, ct);
            }
        }

        LogGenerated?.Invoke($"All clients connected. Running...");

        // Monitor clients — stop if all are disconnected/errored
        while (!ct.IsCancellationRequested && _isRunning)
        {
            await Task.Delay(1000, ct);

            // If all clients are in error/disconnected state, stop the simulation
            if (_clients.Count > 0 && _clients.Values.All(c =>
                c.State == ClientState.Error || c.State == ClientState.Disconnected))
            {
                LogGenerated?.Invoke("All clients disconnected — stopping simulation.");
                break;
            }
        }
    }

    private async Task FinishSimulationAsync()
    {
        LogGenerated?.Invoke("Cleaning up...");

        foreach (var client in _clients.Values)
        {
            try
            {
                await client.DisposeAsync();
            }
            catch { }
        }

        _clients.Clear();

        LastResult = await _metrics.GetResultAsync(CancellationToken.None);
        SimulationCompleted?.Invoke(LastResult);

        _isRunning = false;
        LogGenerated?.Invoke("Simulation finished");
    }

    private void HandleClientStateChanged(Guid clientId, ClientState state)
    {
        _logger.LogDebug("Client {Id} state: {State}", clientId, state);

        switch (state)
        {
            case ClientState.Disconnected:
                _metrics.RecordDisconnection();
                break;
            case ClientState.Reconnecting:
                _metrics.RecordReconnect();
                break;
            case ClientState.Banned:
                _metrics.RecordBan();
                break;
        }
    }

    private void HandleClientError(Guid clientId, string error)
    {
        _metrics.RecordError(error);
        _logger.LogWarning("Client {Id} error: {Error}", clientId, error);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync(CancellationToken.None);

        try
        {
            _cts?.Dispose();
        }
        catch (ObjectDisposedException) { }
    }
}
