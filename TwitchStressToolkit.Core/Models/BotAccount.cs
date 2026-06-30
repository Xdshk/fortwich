using System;
using System.Text.Json.Serialization;

namespace TwitchStressToolkit.Core.Models;

public sealed class BotAccount
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;

    [JsonPropertyName("authToken")]
    public string? AuthToken { get; set; }

    [JsonPropertyName("cookies")]
    public string? EncryptedCookies { get; set; }

    [JsonPropertyName("status")]
    public AccountStatus Status { get; set; } = AccountStatus.Valid;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("lastUsedAt")]
    public DateTime? LastUsedAt { get; set; }

    [JsonPropertyName("banCount")]
    public int BanCount { get; set; }

    [JsonPropertyName("channel")]
    public string? CurrentChannel { get; set; }

    [JsonPropertyName("proxyId")]
    public Guid? ProxyId { get; set; }

    [JsonPropertyName("fingerprintId")]
    public Guid? FingerprintId { get; set; }
}

public enum AccountStatus
{
    Valid,
    Banned,
    Shadowbanned,
    Suspended,
    Invalid,
    Pending,
    Active,
    Error
}
