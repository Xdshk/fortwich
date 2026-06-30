using System;
using System.Text.Json.Serialization;
using TwitchStressToolkit.Core.Enums;

namespace TwitchStressToolkit.Core.Models;

public sealed class Scenario
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public ScenarioType Type { get; init; }

    [JsonPropertyName("connectionConfig")]
    public ConnectionConfig ConnectionConfig { get; init; } = new();

    [JsonPropertyName("activityProfile")]
    public ActivityProfile? ActivityProfile { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan? Duration { get; set; }

    [JsonPropertyName("clientCount")]
    public int ClientCount { get; init; } = 10;
}
