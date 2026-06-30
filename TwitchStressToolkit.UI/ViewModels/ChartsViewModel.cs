using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TwitchStressToolkit.Application.Metrics;

namespace TwitchStressToolkit.UI.ViewModels;

public sealed partial class ChartsViewModel : ObservableObject
{
    [ObservableProperty]
    private int _activeConnections;

    [ObservableProperty]
    private int _messagesPerSecond;

    [ObservableProperty]
    private double _latency;

    [ObservableProperty]
    private int _errorCount;

    // Two series on a single chart. Series is IEnumerable<ISeries>, so we bind
    // the whole collection.
    public ObservableCollection<ISeries> Series { get; } =
    [
        new LineSeries<double>
        {
            Name = "Latency (ms)",
            Values = new ObservableCollection<double>(),
            Stroke = new SolidColorPaint(SKColors.Orange, 2),
            Fill = null,
            GeometrySize = 0
        },
        new LineSeries<double>
        {
            Name = "Msg/sec",
            Values = new ObservableCollection<double>(),
            Stroke = new SolidColorPaint(SKColors.LimeGreen, 2),
            Fill = null,
            GeometrySize = 0
        }
    ];

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

    public ChartsViewModel(MetricsBus metricsBus)
    {
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ChartsViewModel] Constructor");
        metricsBus.SnapshotUpdated += snapshot =>
        {
            if (!_firstSnapshot)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ChartsViewModel] First MetricsBus snapshot");
                _firstSnapshot = true;
            }
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                ActiveConnections = snapshot.ActiveConnections;
                ErrorCount = (int)snapshot.TotalErrors;
                Latency = snapshot.CurrentLatencyMs;
                MessagesPerSecond = (int)snapshot.TotalMessagesSent;
                UpdateLatency(snapshot.CurrentLatencyMs);
                UpdateThroughput((int)snapshot.TotalMessagesSent);
            });
        };
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ChartsViewModel] MetricsBus subscribed");
    }

    public void UpdateLatency(double latencyMs)
    {
        Latency = latencyMs;
        var values = (ObservableCollection<double>)((LineSeries<double>)Series[0]).Values!;
        values.Add(latencyMs);
        if (values.Count > 50)
        {
            values.RemoveAt(0);
        }
    }

    public void UpdateThroughput(int messagesPerSec)
    {
        MessagesPerSecond = messagesPerSec;
        var values = (ObservableCollection<double>)((LineSeries<double>)Series[1]).Values!;
        values.Add(messagesPerSec);
        if (values.Count > 50)
        {
            values.RemoveAt(0);
        }
    }
}
