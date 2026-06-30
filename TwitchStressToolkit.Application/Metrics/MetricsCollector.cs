using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Application.Metrics;

public sealed class MetricsCollector : IMetricsCollector
{
    private readonly ILogger<MetricsCollector> _logger;
    private readonly Channel<Sample> _channel;
    private readonly List<double> _latencies = [];
    private readonly object _lock = new();

    private long _totalMessagesSent;
    private long _totalMessagesReceived;
    private long _totalConnections;
    private long _totalDisconnections;
    private long _totalReconnects;
    private long _totalErrors;
    private long _banCount;
    private double _peakCpuUsage;
    private double _peakMemoryMb;
    private long _activeConnections; // Use long for Interlocked operations
    private DateTime _startedAt;

    public long TotalMessagesSent => Interlocked.Read(ref _totalMessagesSent);
    public long TotalMessagesReceived => Interlocked.Read(ref _totalMessagesReceived);
    public long TotalConnections => Interlocked.Read(ref _totalConnections);
    public long TotalDisconnections => Interlocked.Read(ref _totalDisconnections);
    public long TotalReconnects => Interlocked.Read(ref _totalReconnects);
    public long TotalErrors => Interlocked.Read(ref _totalErrors);
    public long BanCount => Interlocked.Read(ref _banCount);
    public double CurrentLatencyMs { get; private set; }
    public double AverageLatencyMs { get; private set; }
    public double P95LatencyMs { get; private set; }
    public double P99LatencyMs { get; private set; }
    public int ActiveConnections => (int)Interlocked.Read(ref _activeConnections);

    public ChannelReader<Sample> Samples => _channel.Reader;

    public MetricsCollector(ILogger<MetricsCollector> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<Sample>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public void RecordLatency(double latencyMs)
    {
        lock (_lock)
        {
            _latencies.Add(latencyMs);
            CurrentLatencyMs = latencyMs;

            if (_latencies.Count > 10000)
            {
                _latencies.RemoveRange(0, 5000);
            }

            AverageLatencyMs = _latencies.Count > 0 ? _latencies.Average() : 0;
            P95LatencyMs = CalculatePercentile(0.95);
            P99LatencyMs = CalculatePercentile(0.99);
        }

        TryWriteSample(latencyMs, "Latency", "ms");
    }

    public void RecordMessageSent()
    {
        Interlocked.Increment(ref _totalMessagesSent);
        TryWriteSample(1, "Messages", "sent");
    }

    public void RecordMessageReceived()
    {
        Interlocked.Increment(ref _totalMessagesReceived);
    }

    public void RecordConnection()
    {
        Interlocked.Increment(ref _totalConnections);
        Interlocked.Increment(ref _activeConnections);
        TryWriteSample(1, "Connections", "connected");
    }

    public void RecordDisconnection()
    {
        Interlocked.Increment(ref _totalDisconnections);
        // Prevent negative active connections count
        Interlocked.CompareExchange(ref _activeConnections, 0, 0); // fence
        Interlocked.Decrement(ref _activeConnections);
        if (Interlocked.Read(ref _activeConnections) < 0)
            Interlocked.Exchange(ref _activeConnections, 0);
        TryWriteSample(1, "Connections", "disconnected");
    }

    public void RecordReconnect()
    {
        Interlocked.Increment(ref _totalReconnects);
        TryWriteSample(1, "Reconnects", "reconnect");
    }

    public void RecordError(string error)
    {
        Interlocked.Increment(ref _totalErrors);
        _logger.LogError("Metric recorded error: {Error}", error);
    }

    public void RecordBan()
    {
        Interlocked.Increment(ref _banCount);
        TryWriteSample(1, "Bans", "ban");
    }

    public void RecordCpuUsage(double percent)
    {
        if (percent > _peakCpuUsage)
        {
            _peakCpuUsage = percent;
        }

        TryWriteSample(percent, "CPU", "%");
    }

    public void RecordMemoryUsage(double mb)
    {
        if (mb > _peakMemoryMb)
        {
            _peakMemoryMb = mb;
        }

        TryWriteSample(mb, "Memory", "MB");
    }

    public Task<SimulationResult> GetResultAsync(CancellationToken ct)
    {
        lock (_lock)
        {
            var result = new SimulationResult
            {
                StartedAt = _startedAt == default ? DateTime.UtcNow : _startedAt,
                FinishedAt = DateTime.UtcNow,
                TotalClients = (int)TotalConnections,
                SuccessfulConnections = (int)TotalConnections,
                FailedConnections = (int)TotalErrors,
                TotalMessages = TotalMessagesSent,
                TotalReconnects = (int)TotalReconnects,
                BanCount = (int)BanCount,
                AverageLatencyMs = AverageLatencyMs,
                P95LatencyMs = P95LatencyMs,
                P99LatencyMs = P99LatencyMs,
                PeakCpuUsage = _peakCpuUsage,
                PeakMemoryMb = _peakMemoryMb
            };

            return Task.FromResult(result);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _latencies.Clear();
            _startedAt = DateTime.UtcNow;
            _peakCpuUsage = 0;
            _peakMemoryMb = 0;
        }

        Interlocked.Exchange(ref _totalMessagesSent, 0);
        Interlocked.Exchange(ref _totalMessagesReceived, 0);
        Interlocked.Exchange(ref _totalConnections, 0);
        Interlocked.Exchange(ref _totalDisconnections, 0);
        Interlocked.Exchange(ref _totalReconnects, 0);
        Interlocked.Exchange(ref _totalErrors, 0);
        Interlocked.Exchange(ref _banCount, 0);
        Interlocked.Exchange(ref _activeConnections, 0);
    }

    private long _sampleCount;

    private bool TryWriteSample(double value, string category, string label)
    {
        var count = Interlocked.Increment(ref _sampleCount);
        if (count == 1 || count % 100 == 0)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MetricsCollector] Sample #{count} ({category}/{label}={value})");
        }
        try
        {
            return _channel.Writer.TryWrite(new Sample
            {
                Value = value,
                Category = category,
                Label = label
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MetricsCollector] Sample write failed: {ex}");
            throw;
        }
    }

    private double CalculatePercentile(double percentile)
    {
        if (_latencies.Count == 0) return 0;

        var sorted = _latencies.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }

    public ValueTask DisposeAsync()
    {
        // Защита от повторного закрытия: MetricsBus может уже закрыть канал
        // раньше, чем MetricsCollector дойдёт до своего Dispose.
        try
        {
            _channel.Writer.Complete();
        }
        catch (ChannelClosedException)
        {
            // Уже закрыт — игнорируем.
        }
        return ValueTask.CompletedTask;
    }
}
