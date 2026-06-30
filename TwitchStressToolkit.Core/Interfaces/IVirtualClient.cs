using System;
using System.Threading;
using System.Threading.Tasks;
using TwitchStressToolkit.Core.Enums;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Core.Interfaces;

public interface IVirtualClient : IAsyncDisposable
{
    Guid Id { get; }
    BotAccount Account { get; }
    ClientState State { get; }
    string? CurrentChannel { get; }

    event Action<Guid, ClientState>? StateChanged;
    event Action<Guid, string>? MessageReceived;
    event Action<Guid, double>? LatencyUpdated;
    event Action<Guid, string>? ErrorOccurred;

    Task ConnectAsync(string channel, CancellationToken ct);
    Task DisconnectAsync(CancellationToken ct);
    Task SendMessageAsync(string message, CancellationToken ct);
    Task ReconnectAsync(CancellationToken ct);
}
