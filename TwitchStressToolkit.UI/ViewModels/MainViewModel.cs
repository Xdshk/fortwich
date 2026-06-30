using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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

    // 0=Dashboard, 1=BotManager, 2=Charts, 3=Settings, 4=Logs
    [ObservableProperty]
    private int _selectedTabIndex;

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

    // Tracks whether we have received the first MetricsBus snapshot yet.
    private bool _firstSnapshotReceived;

    public MainViewModel(
        AccountManager accountManager,
        SqliteStorageService storage,
        ISimulationEngine simulationEngine,
        MetricsBus metricsBus)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainViewModel] Constructor enter");
        _accountManager = accountManager;
        _storage = storage;
        _simulationEngine = simulationEngine;
        _metricsBus = metricsBus;

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

        // Engine log → UI log
        _simulationEngine.LogGenerated += log => AddLog(log);

        // Engine finished → reset running flag
        _simulationEngine.SimulationCompleted += _ =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                IsRunning = false;
                StatusMessage = "Simulation finished";
            });
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

        var config = new ConnectionConfig
        {
            TargetChannel = TargetChannel,
            MaxClients = MaxClients,
            RampUpDelayMs = 1000
        };

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
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainViewModel] StartAsync error: {ex}");
            AddLog($"Simulation error: {ex.Message}");
            IsRunning = false;
            StatusMessage = "Error";
        }
    }

    private async Task StopSimulationAsync()
    {
        StatusMessage = "Stopping...";
        AddLog("Stopping simulation...");
        try
        {
            await _simulationEngine.StopAsync(CancellationToken.None);
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
}
