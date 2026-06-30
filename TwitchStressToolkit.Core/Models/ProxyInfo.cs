using System;
using System.Text.Json.Serialization;
using TwitchStressToolkit.Core.Enums;

namespace TwitchStressToolkit.Core.Models;

public sealed class ProxyInfo
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    [JsonPropertyName("host")]
    public string Host { get; init; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("password")]
    public string? Password { get; init; }

    [JsonPropertyName("type")]
    public ProxyType Type { get; init; } = ProxyType.Http;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("latencyMs")]
    public int? LatencyMs { get; set; }

    [JsonPropertyName("lastCheckedAt")]
    public DateTime? LastCheckedAt { get; set; }

    [JsonPropertyName("failCount")]
    public int FailCount { get; set; }

    public string Address => $"{Host}:{Port}";
}
