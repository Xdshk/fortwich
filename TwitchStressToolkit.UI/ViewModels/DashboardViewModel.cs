using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TwitchStressToolkit.Application.Metrics;

namespace TwitchStressToolkit.UI.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private int _activeConnections;

    [ObservableProperty]
    private int _messagesPerSecond;

    [ObservableProperty]
    private double _latency;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _banCount;

    [ObservableProperty]
    private int _reconnectCount;

    [ObservableProperty]
    private double _cpuUsage;

    [ObservableProperty]
    private double _memoryUsage;

    // LiveCharts series — CartesianChart.Series expects IEnumerable<ISeries>.
    // LineSeries<double> keeps an internal ObservableCollection of values, so
    // we just .Add() to it and the chart updates automatically.
    public ISeries[] LatencyChartSeries => [LatencySeries];
    public ISeries[] ThroughputChartSeries => [ThroughputSeries];

    public LineSeries<double> LatencySeries { get; } = new()
    {
        Name = "Latency (ms)",
        Values = new ObservableCollection<double>(),
        Stroke = new SolidColorPaint(SKColors.Orange, 2),
        Fill = null,
        GeometrySize = 0
    };

    public LineSeries<double> ThroughputSeries { get; } = new()
    {
        Name = "Msg/sec",
        Values = new ObservableCollection<double>(),
        Stroke = new SolidColorPaint(SKColors.LimeGreen, 2),
        Fill = null,
        GeometrySize = 0
    };

    public Axis[] XAxes { get; } =
    [
        new Axis
        {
            Name = "Time",
            LabelsPaint = new SolidColorPaint(SKColors.LightGray),
            TextSize = 10
        }
    ];

    public Axis[] YAxes { get; } =
    [
        new Axis
        {
            Name = "Value",
            LabelsPaint = new SolidColorPaint(SKColors.LightGray),
            TextSize = 10
        }
    ];

    private bool _firstSnapshot;

    public DashboardViewModel(MetricsBus metricsBus)
    {
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DashboardViewModel] Constructor");
        metricsBus.SnapshotUpdated += snapshot =>
        {
            if (!_firstSnapshot)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DashboardViewModel] First MetricsBus snapshot");
                _firstSnapshot = true;
            }
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                ActiveConnections = snapshot.ActiveConnections;
                ErrorCount = (int)snapshot.TotalErrors;
                Latency = snapshot.CurrentLatencyMs;
                MessagesPerSecond = (int)snapshot.TotalMessagesSent;
                BanCount = (int)snapshot.BanCount;
                UpdateLatency(snapshot.CurrentLatencyMs);
                UpdateThroughput((int)snapshot.TotalMessagesSent);
            });
        };
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DashboardViewModel] MetricsBus subscribed");
    }

    public void UpdateLatency(double latencyMs)
    {
        Latency = latencyMs;

        var values = (ObservableCollection<double>)LatencySeries.Values!;
        values.Add(latencyMs);
        if (values.Count > 50)
        {
            values.RemoveAt(0);
        }
    }

    public void UpdateThroughput(int messagesPerSec)
    {
        MessagesPerSecond = messagesPerSec;

        var values = (ObservableCollection<double>)ThroughputSeries.Values!;
        values.Add(messagesPerSec);
        if (values.Count > 50)
        {
            values.RemoveAt(0);
        }
    }
}
