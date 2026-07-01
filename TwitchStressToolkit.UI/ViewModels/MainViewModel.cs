using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchStressToolkit.Application.Accounts;
using TwitchStressToolkit.Application.Metrics;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Core.Models;
using TwitchStressToolkit.Infrastructure.Storage;

namespace TwitchStressToolkit.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly AccountManager _accountManager;
    private readonly SqliteStorageService _storage;
    private readonly ISimulationEngine _simulationEngine;
    private readonly MetricsBus _metricsBus;
    private readonly SettingsViewModel _settings;

    [ObservableProperty]
    private int _activeConnections;

    [ObservableProperty]
    private int _messagesPerSecond;

    [ObservableProperty]
    private double _latency;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _targetChannel = string.Empty;

    [ObservableProperty]
    private int _maxClients = 20;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _logsText = "Logs will appear here after the first action.";

    // 0=Dashboard, 1=BotManager, 2=Charts, 3=Settings, 4=Logs
    [ObservableProperty]
    private int _selectedTabIndex;

    public string ConfiguredChannelDisplay =>
        string.IsNullOrWhiteSpace(TargetChannel) ? "Channel is not configured" : $"Shared channel: #{TargetChannel}";

    public ObservableCollection<BotAccount> Accounts { get; } = [];
    public ObservableCollection<string> Logs { get; } = [];

    public IAsyncRelayCommand StartSimulationCommand { get; }
    public IAsyncRelayCommand StopSimulationCommand { get; }
    public IAsyncRelayCommand LoadAccountsCommand { get; }
    public IRelayCommand ExitCommand { get; }
    public IRelayCommand ShowDashboardCommand { get; }
    public IRelayCommand ShowBotManagerCommand { get; }
    public IRelayCommand ShowChartsCommand { get; }
    public IRelayCommand ShowSettingsCommand { get; }
    public IRelayCommand ShowLogsCommand { get; }
    public IRelayCommand AboutCommand { get; }
    public IAsyncRelayCommand ExportDiagnosticsCommand { get; }

    // Tracks whether we have received the first MetricsBus snapshot yet.
    private bool _firstSnapshotReceived;

    public MainViewModel(
        AccountManager accountManager,
        SqliteStorageService storage,
        ISimulationEngine simulationEngine,
        MetricsBus metricsBus,
        SettingsViewModel settings)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainViewModel] Constructor enter");
        _accountManager = accountManager;
        _storage = storage;
        _simulationEngine = simulationEngine;
        _metricsBus = metricsBus;
        _settings = settings;
        TargetChannel = _settings.TargetChannel;
        MaxClients = _settings.MaxClients;

        StartSimulationCommand = new AsyncRelayCommand(StartSimulationAsync, () => !IsRunning);
        StopSimulationCommand = new AsyncRelayCommand(StopSimulationAsync, () => IsRunning);
        LoadAccountsCommand = new AsyncRelayCommand(LoadAccountsAsync);
        ExitCommand = new RelayCommand(() => System.Windows.Application.Current?.Shutdown());
        ShowDashboardCommand = new RelayCommand(() =>
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainViewModel] ShowDashboardCommand executed");
            SelectedTabIndex = 0;
        });
        ShowBotManagerCommand = new RelayCommand(() => SelectedTabIndex = 1);
        ShowChartsCommand = new RelayCommand(() => SelectedTabIndex = 2);
        ShowSettingsCommand = new RelayCommand(() => SelectedTabIndex = 3);
        ShowLogsCommand = new RelayCommand(() => SelectedTabIndex = 4);
        AboutCommand = new RelayCommand(() => MessageBox.Show("Twitch Stress Toolkit v0.1.0", "About", MessageBoxButton.OK, MessageBoxImage.Information));
        ExportDiagnosticsCommand = new AsyncRelayCommand(ExportDiagnosticsAsync);
        _settings.PropertyChanged += SettingsOnPropertyChanged;

        // Engine log → UI log
        _simulationEngine.LogGenerated += log => AddLog(log);

        // Engine finished → reset running flag
        _simulationEngine.SimulationCompleted += _result =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                IsRunning = false;
                StatusMessage = "Simulation finished";
            });
            _ = _metricsBus.StopAsync();
        };

        // Metrics bus → UI properties (marshalled to UI thread)
        _metricsBus.SnapshotUpdated += snapshot =>
        {
            if (!_firstSnapshotReceived)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainViewModel] First MetricsBus snapshot received");
                _firstSnapshotReceived = true;
            }
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                ActiveConnections = snapshot.ActiveConnections;
                ErrorCount = (int)snapshot.TotalErrors;
                Latency = snapshot.CurrentLatencyMs;
                // Approximate msg/sec from total sent (delta would need timing;
                // for now surface total as a coarse indicator).
                MessagesPerSecond = (int)snapshot.TotalMessagesSent;
            });
        };

        _accountManager.AccountAdded += account =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() => Accounts.Add(account));
            AddLog($"Account added: {account.Username}");
        };

        _accountManager.AccountRemoved += account =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() => Accounts.Remove(account));
            AddLog($"Account removed: {account.Username}");
        };

        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainViewModel] Constructor complete");
    }

    private async Task StartSimulationAsync()
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainViewModel] StartSimulationAsync enter");
        var config = _settings.ToConfig();
        TargetChannel = config.TargetChannel ?? string.Empty;
        MaxClients = config.MaxClients;

        if (string.IsNullOrWhiteSpace(TargetChannel))
        {
            AddLog("Please enter a target channel first (use Settings or Bot Manager).");
            return;
        }

        IsRunning = true;
        StatusMessage = "Starting simulation...";
        AddLog($"Simulation started → #{TargetChannel}");

        _metricsBus.Start();
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainViewModel] MetricsBus started");

        try
        {
            // Fire and forget — the engine raises SimulationCompleted when done.
            await _simulationEngine.StartAsync(config, CancellationToken.None);
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainViewModel] StartAsync (engine) returned");
        }
        catch (InvalidOperationException ex)
        {
            AddLog(ex.Message);
            IsRunning = false;
            StatusMessage = "Already running";
            await _metricsBus.StopAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainViewModel] StartAsync error: {ex}");
            AddLog($"Simulation error: {ex.Message}");
            IsRunning = false;
            StatusMessage = "Error";
            await _metricsBus.StopAsync();
        }
    }

    private async Task StopSimulationAsync()
    {
        StatusMessage = "Stopping...";
        AddLog("Stopping simulation...");
        try
        {
            await _simulationEngine.StopAsync(CancellationToken.None);
            await _metricsBus.StopAsync();
        }
        catch (Exception ex)
        {
            AddLog($"Stop error: {ex.Message}");
        }
        IsRunning = false;
        StatusMessage = "Stopped";
        AddLog("Simulation stopped");
    }

    private async Task LoadAccountsAsync()
    {
        var dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TwitchStressToolkit");

        var txtPath = Path.Combine(dataPath, "accounts.txt");
        var jsonPath = Path.Combine(dataPath, "accounts.json");

        if (File.Exists(txtPath))
        {
            await _accountManager.LoadAccountsAsync(txtPath);
        }
        else if (File.Exists(jsonPath))
        {
            await _accountManager.LoadAccountsAsync(jsonPath);
        }
        else
        {
            AddLog("No accounts file found. Place accounts.txt in %LocalAppData%/TwitchStressToolkit/");
        }
    }

    public void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var entry = $"[{timestamp}] {message}";

        void AddEntry()
        {
            Logs.Insert(0, entry);
            if (Logs.Count > 1000)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }

            LogsText = string.Join(Environment.NewLine, Logs);
        }

        if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.Invoke(AddEntry);
        }
        else
        {
            AddEntry();
        }
    }

    partial void OnIsRunningChanged(bool value)
    {
        StartSimulationCommand.NotifyCanExecuteChanged();
        StopSimulationCommand.NotifyCanExecuteChanged();
    }

    partial void OnTargetChannelChanged(string value)
    {
        OnPropertyChanged(nameof(ConfiguredChannelDisplay));
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsViewModel.TargetChannel):
                TargetChannel = _settings.TargetChannel;
                break;
            case nameof(SettingsViewModel.MaxClients):
                MaxClients = _settings.MaxClients;
                break;
        }
    }

    private async Task ExportDiagnosticsAsync()
    {
        var exportDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TwitchStressToolkit",
            "exports");
        Directory.CreateDirectory(exportDirectory);

        var filePath = Path.Combine(exportDirectory, $"diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        var builder = new StringBuilder();
        builder.AppendLine("Twitch Stress Toolkit Diagnostics");
        builder.AppendLine($"Created: {DateTime.Now:O}");
        builder.AppendLine($"Status: {StatusMessage}");
        builder.AppendLine($"ActiveConnections: {ActiveConnections}");
        builder.AppendLine($"MessagesShown: {MessagesPerSecond}");
        builder.AppendLine($"LatencyMs: {Latency:F0}");
        builder.AppendLine($"Errors: {ErrorCount}");
        builder.AppendLine();
        builder.AppendLine("Recent logs:");
        foreach (var logEntry in Logs)
        {
            builder.AppendLine(logEntry);
        }

        await File.WriteAllTextAsync(filePath, builder.ToString());
        AddLog($"Diagnostics exported to {filePath}");
        StatusMessage = "Diagnostics exported";
    }
}
