using System;
using System.Text.Json.Serialization;
using TwitchStressToolkit.Core.Enums;

namespace TwitchStressToolkit.Core.Models;

public sealed class ConnectionConfig
{
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; init; } = "wss://irc-ws.chat.twitch.tv:443";

    [JsonPropertyName("gqlUrl")]
    public string GqlUrl { get; init; } = "https://gql.twitch.tv/gql";

    [JsonPropertyName("maxClients")]
    public int MaxClients { get; init; } = 20;

    [JsonPropertyName("connectionTimeoutMs")]
    public int ConnectionTimeoutMs { get; init; } = 10000;

    [JsonPropertyName("reconnectDelayMs")]
    public int ReconnectDelayMs { get; init; } = 5000;

    [JsonPropertyName("maxReconnectAttempts")]
    public int MaxReconnectAttempts { get; init; } = 5;

    [JsonPropertyName("heartbeatIntervalMs")]
    public int HeartbeatIntervalMs { get; init; } = 30000;

    [JsonPropertyName("messageSendMinDelayMs")]
    public int MessageSendMinDelayMs { get; init; } = 1500;

    [JsonPropertyName("messageSendMaxDelayMs")]
    public int MessageSendMaxDelayMs { get; init; } = 5000;

    [JsonPropertyName("scenarioType")]
    public ScenarioType ScenarioType { get; init; } = ScenarioType.GradualConnect;

    [JsonPropertyName("rampUpDelayMs")]
    public int RampUpDelayMs { get; init; } = 1000;

    [JsonPropertyName("targetChannel")]
    public string? TargetChannel { get; init; }

    [JsonPropertyName("enableChat")]
    public bool EnableChat { get; init; } = true;

    [JsonPropertyName("enableViewer")]
    public bool EnableViewer { get; init; } = true;

    [JsonPropertyName("enableReconnects")]
    public bool EnableReconnects { get; init; } = true;
}
