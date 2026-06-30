using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchStressToolkit.Application.Accounts;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Core.Models;
using TwitchStressToolkit.Infrastructure.Network;

namespace TwitchStressToolkit.UI.ViewModels;

public sealed partial class BotManagerViewModel : ObservableObject
{
    private readonly AccountManager _accountManager;
    private readonly ISimulationEngine _simulationEngine;
    private readonly IProxyManager _proxyManager;

    [ObservableProperty]
    private ObservableCollection<BotAccount> _accounts = [];

    [ObservableProperty]
    private ObservableCollection<ProxyInfo> _proxies = [];

    [ObservableProperty]
    private BotAccount? _selectedAccount;

    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _newAuthToken = string.Empty;

    [ObservableProperty]
    private string _newChannel = string.Empty;

    [ObservableProperty]
    private string _targetChannel = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _accountsFile = string.Empty;

    [ObservableProperty]
    private string _proxiesFile = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private int _connectedCount;

    [ObservableProperty]
    private int _errorCount;

    public IAsyncRelayCommand LoadAccountsCommand { get; }
    public IAsyncRelayCommand AddAccountCommand { get; }
    public IAsyncRelayCommand RemoveAccountCommand { get; }
    public IAsyncRelayCommand ChangeChannelCommand { get; }
    public IAsyncRelayCommand ValidateAccountCommand { get; }
    public IAsyncRelayCommand ValidateAllCommand { get; }
    public IAsyncRelayCommand StartSimulationCommand { get; }
    public IAsyncRelayCommand StopSimulationCommand { get; }
    public IAsyncRelayCommand LoadProxiesCommand { get; }
    public IAsyncRelayCommand AddProxyCommand { get; }

    public BotManagerViewModel(AccountManager accountManager, ISimulationEngine simulationEngine, IProxyManager proxyManager)
    {
        _accountManager = accountManager;
        _simulationEngine = simulationEngine;
        _proxyManager = proxyManager;

        LoadAccountsCommand = new AsyncRelayCommand(LoadAccountsAsync, () => !IsLoading);
        AddAccountCommand = new AsyncRelayCommand(AddAccountAsync);
        RemoveAccountCommand = new AsyncRelayCommand(RemoveAccountAsync, () => SelectedAccount is not null);
        ChangeChannelCommand = new AsyncRelayCommand(ChangeChannelAsync, () => SelectedAccount is not null);
        ValidateAccountCommand = new AsyncRelayCommand(ValidateAccountAsync, () => SelectedAccount is not null);
        ValidateAllCommand = new AsyncRelayCommand(ValidateAllAsync);
        StartSimulationCommand = new AsyncRelayCommand(StartSimulationAsync, () => !IsRunning);
        StopSimulationCommand = new AsyncRelayCommand(StopSimulationAsync, () => IsRunning);
        LoadProxiesCommand = new AsyncRelayCommand(LoadProxiesAsync);
        AddProxyCommand = new AsyncRelayCommand(AddProxyAsync);

        _accountManager.AccountAdded += account =>
        {
            // ObservableCollection must be modified on the UI thread
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Accounts.Add(account);
            });
            StatusText = $"Added: {account.Username}";
        };

        _accountManager.AccountRemoved += account =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Accounts.Remove(account);
            });
            StatusText = $"Removed: {account.Username}";
        };

        _simulationEngine.LogGenerated += log =>
        {
            StatusText = log;
        };
    }

    private async Task LoadAccountsAsync()
    {
        if (string.IsNullOrWhiteSpace(AccountsFile))
        {
            StatusText = "Please select a file";
            return;
        }

        IsLoading = true;
        StatusText = "Loading accounts...";

        try
        {
            await _accountManager.LoadAccountsAsync(AccountsFile);
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AddAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewPassword))
        {
            StatusText = "Username and password required";
            return;
        }

        await _accountManager.AddAccountAsync(NewUsername, NewPassword, NewAuthToken);

        NewUsername = string.Empty;
        NewPassword = string.Empty;
        NewAuthToken = string.Empty;
    }

    private async Task RemoveAccountAsync()
    {
        if (SelectedAccount is null) return;

        await _accountManager.RemoveAccountAsync(SelectedAccount.Id);
        SelectedAccount = null;
    }

    private async Task ChangeChannelAsync()
    {
        if (SelectedAccount is null || string.IsNullOrWhiteSpace(NewChannel))
            return;

        await _accountManager.ChangeAccountChannelAsync(SelectedAccount.Id, NewChannel);
        StatusText = $"Channel changed to: {NewChannel}";
    }

    private async Task ValidateAccountAsync()
    {
        if (SelectedAccount is null) return;

        StatusText = $"Validating {SelectedAccount.Username}...";

        try
        {
            await _accountManager.ValidateAccountAsync(SelectedAccount.Id);
            StatusText = $"Status: {SelectedAccount.Status}";
        }
        catch (Exception ex)
        {
            StatusText = $"Validation error: {ex.Message}";
        }
    }

    private async Task ValidateAllAsync()
    {
        StatusText = "Validating all accounts...";

        try
        {
            await _accountManager.ValidateAllAccountsAsync();
            StatusText = "Validation complete";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    private async Task StartSimulationAsync()
    {
        if (string.IsNullOrWhiteSpace(TargetChannel))
        {
            StatusText = "Please enter target channel!";
            return;
        }

        if (Accounts.Count == 0)
        {
            StatusText = "No accounts loaded!";
            return;
        }

        IsRunning = true;
        StatusText = "Starting simulation...";

        try
        {
            var config = new ConnectionConfig
            {
                TargetChannel = TargetChannel,
                MaxClients = Accounts.Count,
                RampUpDelayMs = 1000
            };

            await _simulationEngine.StartAsync(config, CancellationToken.None);
        }
        catch (Exception ex)
        {
            StatusText = $"Simulation error: {ex.Message}";
            IsRunning = false;
        }
    }

    private async Task StopSimulationAsync()
    {
        StatusText = "Stopping...";
        await _simulationEngine.StopAsync(CancellationToken.None);
        IsRunning = false;
        StatusText = "Stopped";
    }

    private async Task LoadProxiesAsync()
    {
        if (string.IsNullOrWhiteSpace(ProxiesFile))
        {
            StatusText = "Please select a proxies file";
            return;
        }

        try
        {
            var proxies = await _proxyManager.LoadFromFileAsync(ProxiesFile);
            foreach (var proxy in proxies)
            {
                Proxies.Add(proxy);
            }
            StatusText = $"Loaded {proxies.Count} proxies";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    private async Task AddProxyAsync()
    {
        StatusText = "Use proxies.txt file format: host:port:user:pass";
    }
}
