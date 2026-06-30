using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Core.Interfaces;

public interface IProxyManager
{
    IReadOnlyList<ProxyInfo> All { get; }
    int ActiveCount { get; }

    Task<ProxyInfo?> GetNextAsync();
    Task<ProxyInfo?> GetForAccountAsync(Guid accountId);
    Task ReturnAsync(ProxyInfo proxy);
    Task MarkFailedAsync(ProxyInfo proxy);
    Task AddProxyAsync(ProxyInfo proxy);
    Task RemoveProxyAsync(Guid proxyId);
    Task<IReadOnlyList<ProxyInfo>> LoadFromFileAsync(string path);
    Task HealthCheckAsync();
}
