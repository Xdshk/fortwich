using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Infrastructure.Network;

public sealed class TwitchIrcClient : IAsyncDisposable
{
    private readonly ILogger<TwitchIrcClient> _logger;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private bool _disposed;

    public event Action<string>? MessageReceived;
    public event Action<string>? Disconnected;
    public event Action<Exception>? ErrorOccurred;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public TwitchIrcClient(ILogger<TwitchIrcClient> logger)
    {
        _logger = logger;
    }

    public async Task ConnectAsync(string uri, string? oauthToken, string username, CancellationToken ct)
    {
        _webSocket = new ClientWebSocket();
        _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

        if (!string.IsNullOrEmpty(oauthToken))
        {
            _webSocket.Options.SetRequestHeader("Authorization", $"OAuth {oauthToken}");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            await _webSocket.ConnectAsync(new Uri(uri), _cts.Token);
            _logger.LogDebug("WebSocket connected to {Uri}", uri);

            if (!string.IsNullOrEmpty(oauthToken))
            {
                await SendRawAsync($"PASS oauth:{oauthToken}", _cts.Token);
            }

            await SendRawAsync($"NICK {username}", _cts.Token);
            await SendRawAsync("CAP REQ :twitch.tv/tags twitch.tv/commands twitch.tv/membership", _cts.Token);

            _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to {Uri}", uri);
            await DisposeAsync();
            throw;
        }
    }

    public async Task ConnectWithProxyAsync(string uri, string? oauthToken, string username, ProxyInfo proxy, CancellationToken ct)
    {
        _webSocket = new ClientWebSocket();
        _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        _webSocket.Options.Proxy = CreateProxy(proxy);

        if (!string.IsNullOrEmpty(oauthToken))
        {
            _webSocket.Options.SetRequestHeader("Authorization", $"OAuth {oauthToken}");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            await _webSocket.ConnectAsync(new Uri(uri), _cts.Token);
            _logger.LogDebug("WebSocket connected via proxy {Proxy}", proxy.Address);

            if (!string.IsNullOrEmpty(oauthToken))
            {
                await SendRawAsync($"PASS oauth:{oauthToken}", _cts.Token);
            }

            await SendRawAsync($"NICK {username}", _cts.Token);
            await SendRawAsync("CAP REQ :twitch.tv/tags twitch.tv/commands twitch.tv/membership", _cts.Token);

            _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect via proxy {Proxy}", proxy.Address);
            await DisposeAsync();
            throw;
        }
    }

    public async Task JoinChannelAsync(string channel, CancellationToken ct)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected");
        }

        await SendRawAsync($"JOIN #{channel}", ct);
        _logger.LogDebug("Joined channel #{Channel}", channel);
    }

    public async Task LeaveChannelAsync(string channel, CancellationToken ct)
    {
        if (!IsConnected) return;

        await SendRawAsync($"PART #{channel}", ct);
        _logger.LogDebug("Left channel #{Channel}", channel);
    }

    public async Task SendMessageAsync(string channel, string message, CancellationToken ct)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected");
        }

        await SendRawAsync($"PRIVMSG #{channel} :{message}", ct);
    }

    public async Task SendRawAsync(string message, CancellationToken ct)
    {
        if (_webSocket is not { State: WebSocketState.Open }) return;

        var bytes = Encoding.UTF8.GetBytes(message + "\r\n");
        var segment = new ArraySegment<byte>(bytes);

        await _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, ct);
    }

    public async Task PingAsync(CancellationToken ct)
    {
        await SendRawAsync("PING :tmi.twitch.tv", ct);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_webSocket is null) return;

        var buffer = new byte[8192];

        try
        {
            while (_webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var segment = new ArraySegment<byte>(buffer);
                WebSocketReceiveResult result;
                try
                {
                    result = await _webSocket.ReceiveAsync(segment, ct);
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    _logger.LogDebug("Connection closed prematurely");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogDebug("WebSocket closed by server");
                    break;
                }

                if (result.Count == 0) continue;

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                MessageReceived?.Invoke(message);

                if (message.StartsWith("PING"))
                {
                    await SendRawAsync("PONG :tmi.twitch.tv", ct);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket error");
            ErrorOccurred?.Invoke(ex);
        }
        catch (ObjectDisposedException)
        {
            // Expected during reconnect/shutdown
        }
        finally
        {
            Disconnected?.Invoke("WebSocket receive loop ended");
        }
    }

    private static System.Net.WebProxy CreateProxy(ProxyInfo proxy)
    {
        var address = new UriBuilder
        {
            Scheme = proxy.Type switch
            {
                Core.Enums.ProxyType.Socks4 => "socks4",
                Core.Enums.ProxyType.Socks5 => "socks5",
                _ => "http"
            },
            Host = proxy.Host,
            Port = proxy.Port
        };

        if (!string.IsNullOrEmpty(proxy.Username))
        {
            address.UserName = proxy.Username;
        }

        if (!string.IsNullOrEmpty(proxy.Password))
        {
            address.Password = proxy.Password;
        }

        return new System.Net.WebProxy(address.Uri)
        {
            Credentials = string.IsNullOrEmpty(proxy.Username)
                ? null
                : new System.Net.NetworkCredential(proxy.Username, proxy.Password)
        };
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

        if (_webSocket is { State: WebSocketState.Open or WebSocketState.Connecting or WebSocketState.None })
        {
            try
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Disposing",
                    CancellationToken.None);
            }
            catch { }
        }

        if (_receiveTask is not null)
        {
            try { await _receiveTask; }
            catch { }
        }

        try
        {
            _webSocket?.Dispose();
        }
        catch { }

        try
        {
            _cts?.Dispose();
        }
        catch { }
    }
}
