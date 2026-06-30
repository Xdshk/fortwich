using System;
using TwitchStressToolkit.Core.Enums;

namespace TwitchStressToolkit.Core.Models;

public sealed class VirtualClient
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required BotAccount Account { get; init; }
    public ClientState State { get; set; } = ClientState.Idle;
    public string? CurrentChannel { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public int MessagesSent { get; set; }
    public int ReconnectCount { get; set; }
    public bool IsBanned { get; set; }
    public string? LastError { get; set; }
}
