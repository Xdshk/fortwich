using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TwitchStressToolkit.Core.Models;

public sealed class SimulationResult
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; init; }

    [JsonPropertyName("finishedAt")]
    public DateTime? FinishedAt { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan? Duration => FinishedAt - StartedAt;

    [JsonPropertyName("totalClients")]
    public int TotalClients { get; init; }

    [JsonPropertyName("successfulConnections")]
    public int SuccessfulConnections { get; set; }

    [JsonPropertyName("failedConnections")]
    public int FailedConnections { get; set; }

    [JsonPropertyName("totalMessages")]
    public long TotalMessages { get; set; }

    [JsonPropertyName("totalReconnects")]
    public int TotalReconnects { get; set; }

    [JsonPropertyName("banCount")]
    public int BanCount { get; set; }

    [JsonPropertyName("averageLatencyMs")]
    public double AverageLatencyMs { get; set; }

    [JsonPropertyName("p95LatencyMs")]
    public double P95LatencyMs { get; set; }

    [JsonPropertyName("p99LatencyMs")]
    public double P99LatencyMs { get; set; }

    [JsonPropertyName("peakCpuUsage")]
    public double PeakCpuUsage { get; set; }

    [JsonPropertyName("peakMemoryMb")]
    public double PeakMemoryMb { get; set; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; init; } = new();
}
