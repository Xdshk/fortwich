using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Interfaces;

namespace TwitchStressToolkit.Application.Metrics;

/// <summary>
/// Single hub that bridges the SimulationEngine/MetricsCollector to the UI layer.
/// UI ViewModels subscribe to SnapshotUpdated; the bus reads Samples from
/// IMetricsCollector on a background thread and raises the event on a cadence,
/// so the UI thread is never flooded with per-sample updates.
/// </summary>
public sealed class MetricsBus : IAsyncDisposable
{
    private readonly ILogger<MetricsBus> _logger;
    private readonly IMetricsCollector _collector;
    private CancellationTokenSource? _cts;
    private Task? _reader;

    public bool IsRunning { get; private set; }

    /// <summary>
    /// Raised ~4x/sec with a snapshot of current metrics. Handlers run on the
    /// reader thread — UI consumers must marshal to the Dispatcher themselves.
    /// </summary>
    public event Action<MetricsSnapshot>? SnapshotUpdated;

    // Throttles the per-iteration trace so DebugView isn't flooded.
    private static long _iterationCount;

    public MetricsBus(ILogger<MetricsBus> logger, IMetricsCollector collector)
    {
        _logger = logger;
        _collector = collector;
    }

    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        _cts = new CancellationTokenSource();
        _reader = Task.Run(() => ReadLoopAsync(_cts.Token));
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MetricsBus] Start() — reader launched");
    }

    public async Task StopAsync()
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MetricsBus] StopAsync() enter");
        if (!IsRunning) return;
        IsRunning = false;

        if (_cts is not null)
        {
            try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        }

        if (_reader is not null)
        {
            try { await _reader; }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MetricsBus] Reader stopped with error: {ex}");
                _logger.LogWarning(ex, "MetricsBus reader stopped with error");
            }
        }

        _cts?.Dispose();
        _cts = null;
        _reader = null;
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MetricsBus] StopAsync() complete");
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MetricsBus] ReadLoopAsync enter");
        // Drain the channel and emit a snapshot a few times per second.
        var reader = _collector.Samples;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Drain everything currently buffered so the snapshot is fresh.
                while (reader.TryRead(out _)) { }

                MetricsSnapshot snapshot;
                try
                {
                    snapshot = new MetricsSnapshot
                    {
                        Timestamp = DateTime.UtcNow,
                        ActiveConnections = _collector.ActiveConnections,
                        TotalMessagesSent = _collector.TotalMessagesSent,
                        CurrentLatencyMs = _collector.CurrentLatencyMs,
                        AverageLatencyMs = _collector.AverageLatencyMs,
                        P95LatencyMs = _collector.P95LatencyMs,
                        TotalErrors = _collector.TotalErrors,
                        BanCount = _collector.BanCount
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MetricsBus] Snapshot build exception: {ex}");
                    throw;
                }

                try
                {
                    SnapshotUpdated?.Invoke(snapshot);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MetricsBus] SnapshotUpdated handler threw: {ex}");
                    _logger.LogWarning(ex, "MetricsBus SnapshotUpdated handler threw");
                }

                var count = Interlocked.Increment(ref _iterationCount);
                if (count == 1 || count % 100 == 0)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MetricsBus] Iteration #{count} (active={snapshot.ActiveConnections}, msgs={snapshot.TotalMessagesSent}, errs={snapshot.TotalErrors})");
                }

                try
                {
                    await timer.WaitForNextTickAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MetricsBus] ReadLoopAsync cancelled");
        }
        catch (Exception ex)
        {
            // Log BEFORE rethrowing so we capture the real crash point.
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MetricsBus] ReadLoopAsync FATAL: {ex}");
            throw;
        }
        finally
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MetricsBus] ReadLoopAsync exiting");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}

public sealed class MetricsSnapshot
{
    public DateTime Timestamp { get; init; }
    public int ActiveConnections { get; init; }
    public long TotalMessagesSent { get; init; }
    public double CurrentLatencyMs { get; init; }
    public double AverageLatencyMs { get; init; }
    public double P95LatencyMs { get; init; }
    public long TotalErrors { get; init; }
    public long BanCount { get; init; }
}
