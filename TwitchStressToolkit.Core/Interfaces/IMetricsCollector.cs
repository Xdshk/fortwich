using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Core.Interfaces;

public interface IMetricsCollector : IAsyncDisposable
{
    long TotalMessagesSent { get; }
    long TotalMessagesReceived { get; }
    long TotalConnections { get; }
    long TotalDisconnections { get; }
    long TotalReconnects { get; }
    long TotalErrors { get; }
    long BanCount { get; }
    double CurrentLatencyMs { get; }
    double AverageLatencyMs { get; }
    double P95LatencyMs { get; }
    double P99LatencyMs { get; }
    int ActiveConnections { get; }

    ChannelReader<Sample> Samples { get; }

    void RecordLatency(double latencyMs);
    void RecordMessageSent();
    void RecordMessageReceived();
    void RecordConnection();
    void RecordDisconnection();
    void RecordReconnect();
    void RecordError(string error);
    void RecordBan();
    void RecordCpuUsage(double percent);
    void RecordMemoryUsage(double mb);

    Task<SimulationResult> GetResultAsync(CancellationToken ct);
    void Reset();
}
