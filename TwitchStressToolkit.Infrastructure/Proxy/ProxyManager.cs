using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Infrastructure.Proxy;

public sealed class ProxyManager : IProxyManager, IAsyncDisposable
{
    private readonly ILogger<ProxyManager> _logger;
    private readonly ConcurrentBag<ProxyInfo> _available = new();
    private readonly ConcurrentDictionary<Guid, ProxyInfo> _all = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    public IReadOnlyList<ProxyInfo> All => _all.Values.ToList();
    public int ActiveCount => _all.Values.Count(p => p.IsActive);

    public ProxyManager(ILogger<ProxyManager> logger)
    {
        _logger = logger;
    }

    public async Task<ProxyInfo?> GetNextAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var proxy = _all.Values
                .Where(p => p.IsActive)
                .OrderBy(p => p.FailCount)
                .ThenBy(_ => Guid.NewGuid())
                .FirstOrDefault();

            return proxy;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<ProxyInfo?> GetForAccountAsync(Guid accountId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var proxy = _all.Values
                .Where(p => p.IsActive)
                .OrderBy(p => Guid.NewGuid())
                .FirstOrDefault();

            if (proxy is not null)
            {
                _logger.LogDebug("Assigned proxy {Proxy} to account {AccountId}", proxy.Address, accountId);
            }

            return proxy;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task ReturnAsync(ProxyInfo proxy)
    {
        _logger.LogDebug("Returned proxy {Proxy} to pool", proxy.Address);
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(ProxyInfo proxy)
    {
        lock (proxy) // Lock on the proxy instance to prevent concurrent modification
        {
            proxy.FailCount++;
            _logger.LogWarning("Proxy {Proxy} marked as failed (count: {Count})", proxy.Address, proxy.FailCount);

            if (proxy.FailCount >= 5)
            {
                proxy.IsActive = false;
                _logger.LogError("Proxy {Proxy} deactivated after 5 failures", proxy.Address);
            }
        }
        return Task.CompletedTask;
    }

    public Task AddProxyAsync(ProxyInfo proxy)
    {
        _all.TryAdd(proxy.Id, proxy);
        _logger.LogInformation("Added proxy {Proxy} (total: {Count})", proxy.Address, _all.Count);
        return Task.CompletedTask;
    }

    public Task RemoveProxyAsync(Guid proxyId)
    {
        _all.TryRemove(proxyId, out _);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ProxyInfo>> LoadFromFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            _logger.LogWarning("Proxy file not found: {Path}", path);
            return Array.Empty<ProxyInfo>();
        }

        var lines = await File.ReadAllLinesAsync(path);
        var proxies = new List<ProxyInfo>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;

            var parts = line.Split(':');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var port))
            {
                var proxy = new ProxyInfo
                {
                    Host = parts[0],
                    Port = port,
                    Username = parts.Length > 2 ? parts[2] : null,
                    Password = parts.Length > 3 ? parts[3] : null
                };

                _all.TryAdd(proxy.Id, proxy);
                proxies.Add(proxy);
            }
        }

        _logger.LogInformation("Loaded {Count} proxies from {Path}", proxies.Count, path);
        return proxies;
    }

    public async Task HealthCheckAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            foreach (var proxy in _all.Values.Where(p => p.IsActive).ToList()) // Snapshot to avoid concurrent modification
            {
                try
                {
                    using var client = new TcpClient();
                    var connectTask = client.ConnectAsync(proxy.Host, proxy.Port);
                    var timeoutTask = Task.Delay(5000);
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                    if (completedTask == connectTask && client.Connected)
                    {
                        proxy.LatencyMs = 0;
                        proxy.LastCheckedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        proxy.FailCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Proxy {Proxy} health check failed", proxy.Address);
                    proxy.FailCount++;
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _semaphore.Dispose();
        return ValueTask.CompletedTask;
    }
}
