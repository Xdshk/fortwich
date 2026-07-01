using System.Diagnostics;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.UI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly string _dataDirectory;
    private readonly string _settingsPath;
    private bool _suspendPersistence;

    [ObservableProperty]
    private int _maxClients = 20;

    [ObservableProperty]
    private int _connectionTimeoutMs = 10000;

    [ObservableProperty]
    private int _reconnectDelayMs = 5000;

    [ObservableProperty]
    private int _maxReconnectAttempts = 5;

    [ObservableProperty]
    private int _heartbeatIntervalMs = 30000;

    [ObservableProperty]
    private int _messageSendMinDelayMs = 1500;

    [ObservableProperty]
    private int _messageSendMaxDelayMs = 5000;

    [ObservableProperty]
    private int _rampUpDelayMs = 1000;

    [ObservableProperty]
    private string _targetChannel = string.Empty;

    [ObservableProperty]
    private string _ircUrl = "wss://irc-ws.chat.twitch.tv:443";

    [ObservableProperty]
    private string _gqlUrl = "https://gql.twitch.tv/gql";

    [ObservableProperty]
    private bool _enableChat = true;

    [ObservableProperty]
    private bool _enableViewer = true;

    [ObservableProperty]
    private bool _enableReconnects = true;

    [ObservableProperty]
    private bool _enableAutoRotate = false;

    [ObservableProperty]
    private string _statusText = "Settings are ready.";

    [ObservableProperty]
    private string _settingsPathText = string.Empty;

    public IRelayCommand SaveSettingsCommand { get; }
    public IRelayCommand ResetDefaultsCommand { get; }
    public IRelayCommand OpenDataFolderCommand { get; }
    public IRelayCommand OpenExportsFolderCommand { get; }

    public string TargetChannelDisplay =>
        string.IsNullOrWhiteSpace(TargetChannel) ? "Not configured" : $"#{TargetChannel}";

    public SettingsViewModel()
    {
        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TwitchStressToolkit");
        _settingsPath = Path.Combine(_dataDirectory, "ui-settings.json");
        SettingsPathText = _settingsPath;

        SaveSettingsCommand = new RelayCommand(SaveSettings);
        ResetDefaultsCommand = new RelayCommand(ResetDefaults);
        OpenDataFolderCommand = new RelayCommand(() => OpenFolder(_dataDirectory, "App data folder opened."));
        OpenExportsFolderCommand = new RelayCommand(() =>
        {
            var exportsDirectory = Path.Combine(_dataDirectory, "exports");
            Directory.CreateDirectory(exportsDirectory);
            OpenFolder(exportsDirectory, "Exports folder opened.");
        });

        LoadSettings();
    }

    public ConnectionConfig ToConfig() => new()
    {
        BaseUrl = IrcUrl,
        GqlUrl = GqlUrl,
        MaxClients = MaxClients,
        ConnectionTimeoutMs = ConnectionTimeoutMs,
        ReconnectDelayMs = ReconnectDelayMs,
        MaxReconnectAttempts = MaxReconnectAttempts,
        HeartbeatIntervalMs = HeartbeatIntervalMs,
        MessageSendMinDelayMs = MessageSendMinDelayMs,
        MessageSendMaxDelayMs = MessageSendMaxDelayMs,
        RampUpDelayMs = RampUpDelayMs,
        TargetChannel = TargetChannel,
        EnableChat = EnableChat,
        EnableViewer = EnableViewer,
        EnableReconnects = EnableReconnects
    };

    partial void OnMaxClientsChanged(int value) => PersistSettings();
    partial void OnConnectionTimeoutMsChanged(int value) => PersistSettings();
    partial void OnReconnectDelayMsChanged(int value) => PersistSettings();
    partial void OnMaxReconnectAttemptsChanged(int value) => PersistSettings();
    partial void OnHeartbeatIntervalMsChanged(int value) => PersistSettings();
    partial void OnMessageSendMinDelayMsChanged(int value) => PersistSettings();
    partial void OnMessageSendMaxDelayMsChanged(int value) => PersistSettings();
    partial void OnRampUpDelayMsChanged(int value) => PersistSettings();
    partial void OnIrcUrlChanged(string value) => PersistSettings();
    partial void OnGqlUrlChanged(string value) => PersistSettings();
    partial void OnEnableChatChanged(bool value) => PersistSettings();
    partial void OnEnableViewerChanged(bool value) => PersistSettings();
    partial void OnEnableReconnectsChanged(bool value) => PersistSettings();
    partial void OnEnableAutoRotateChanged(bool value) => PersistSettings();

    partial void OnTargetChannelChanged(string value)
    {
        OnPropertyChanged(nameof(TargetChannelDisplay));
        PersistSettings();
    }

    private void LoadSettings()
    {
        Directory.CreateDirectory(_dataDirectory);

        if (!File.Exists(_settingsPath))
        {
            SaveSettings("Default settings created.");
            return;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var stored = JsonSerializer.Deserialize<StoredSettings>(json);

            if (stored is null)
            {
                StatusText = "Settings file was empty, defaults restored.";
                SaveSettings();
                return;
            }

            _suspendPersistence = true;
            MaxClients = stored.MaxClients;
            ConnectionTimeoutMs = stored.ConnectionTimeoutMs;
            ReconnectDelayMs = stored.ReconnectDelayMs;
            MaxReconnectAttempts = stored.MaxReconnectAttempts;
            HeartbeatIntervalMs = stored.HeartbeatIntervalMs;
            MessageSendMinDelayMs = stored.MessageSendMinDelayMs;
            MessageSendMaxDelayMs = stored.MessageSendMaxDelayMs;
            RampUpDelayMs = stored.RampUpDelayMs;
            TargetChannel = stored.TargetChannel;
            IrcUrl = stored.IrcUrl;
            GqlUrl = stored.GqlUrl;
            EnableChat = stored.EnableChat;
            EnableViewer = stored.EnableViewer;
            EnableReconnects = stored.EnableReconnects;
            EnableAutoRotate = stored.EnableAutoRotate;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsViewModel] Failed to load settings: {ex}");
            StatusText = $"Settings load error: {ex.Message}";
        }
        finally
        {
            _suspendPersistence = false;
        }

        NormalizeValues();
        SaveSettings("Settings loaded.");
    }

    private void SaveSettings() => SaveSettings("Settings saved.");

    private void SaveSettings(string statusMessage)
    {
        NormalizeValues();
        Directory.CreateDirectory(_dataDirectory);

        var stored = new StoredSettings
        {
            MaxClients = MaxClients,
            ConnectionTimeoutMs = ConnectionTimeoutMs,
            ReconnectDelayMs = ReconnectDelayMs,
            MaxReconnectAttempts = MaxReconnectAttempts,
            HeartbeatIntervalMs = HeartbeatIntervalMs,
            MessageSendMinDelayMs = MessageSendMinDelayMs,
            MessageSendMaxDelayMs = MessageSendMaxDelayMs,
            RampUpDelayMs = RampUpDelayMs,
            TargetChannel = TargetChannel,
            IrcUrl = IrcUrl,
            GqlUrl = GqlUrl,
            EnableChat = EnableChat,
            EnableViewer = EnableViewer,
            EnableReconnects = EnableReconnects,
            EnableAutoRotate = EnableAutoRotate
        };

        var json = JsonSerializer.Serialize(stored, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
        StatusText = $"{statusMessage} {DateTime.Now:HH:mm:ss}";
    }

    private void ResetDefaults()
    {
        _suspendPersistence = true;
        try
        {
            MaxClients = 20;
            ConnectionTimeoutMs = 10000;
            ReconnectDelayMs = 5000;
            MaxReconnectAttempts = 5;
            HeartbeatIntervalMs = 30000;
            MessageSendMinDelayMs = 1500;
            MessageSendMaxDelayMs = 5000;
            RampUpDelayMs = 1000;
            TargetChannel = string.Empty;
            IrcUrl = "wss://irc-ws.chat.twitch.tv:443";
            GqlUrl = "https://gql.twitch.tv/gql";
            EnableChat = true;
            EnableViewer = true;
            EnableReconnects = true;
            EnableAutoRotate = false;
        }
        finally
        {
            _suspendPersistence = false;
        }

        SaveSettings("Default settings restored.");
    }

    private void NormalizeValues()
    {
        _suspendPersistence = true;
        try
        {
            MaxClients = Math.Max(1, MaxClients);
            ConnectionTimeoutMs = Math.Max(1000, ConnectionTimeoutMs);
            ReconnectDelayMs = Math.Max(250, ReconnectDelayMs);
            MaxReconnectAttempts = Math.Max(0, MaxReconnectAttempts);
            HeartbeatIntervalMs = Math.Max(1000, HeartbeatIntervalMs);
            MessageSendMinDelayMs = Math.Max(100, MessageSendMinDelayMs);
            MessageSendMaxDelayMs = Math.Max(MessageSendMinDelayMs, MessageSendMaxDelayMs);
            RampUpDelayMs = Math.Max(0, RampUpDelayMs);
            TargetChannel = TargetChannel.Trim().TrimStart('#');
            IrcUrl = string.IsNullOrWhiteSpace(IrcUrl) ? "wss://irc-ws.chat.twitch.tv:443" : IrcUrl.Trim();
            GqlUrl = string.IsNullOrWhiteSpace(GqlUrl) ? "https://gql.twitch.tv/gql" : GqlUrl.Trim();
        }
        finally
        {
            _suspendPersistence = false;
        }
    }

    private void PersistSettings()
    {
        if (_suspendPersistence)
        {
            return;
        }

        try
        {
            SaveSettings("Settings updated.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsViewModel] Failed to save settings: {ex}");
            StatusText = $"Settings save error: {ex.Message}";
        }
    }

    private void OpenFolder(string path, string successMessage)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            StatusText = $"{successMessage} {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not open folder: {ex.Message}";
        }
    }

    private sealed class StoredSettings
    {
        public int MaxClients { get; set; } = 20;
        public int ConnectionTimeoutMs { get; set; } = 10000;
        public int ReconnectDelayMs { get; set; } = 5000;
        public int MaxReconnectAttempts { get; set; } = 5;
        public int HeartbeatIntervalMs { get; set; } = 30000;
        public int MessageSendMinDelayMs { get; set; } = 1500;
        public int MessageSendMaxDelayMs { get; set; } = 5000;
        public int RampUpDelayMs { get; set; } = 1000;
        public string TargetChannel { get; set; } = string.Empty;
        public string IrcUrl { get; set; } = "wss://irc-ws.chat.twitch.tv:443";
        public string GqlUrl { get; set; } = "https://gql.twitch.tv/gql";
        public bool EnableChat { get; set; } = true;
        public bool EnableViewer { get; set; } = true;
        public bool EnableReconnects { get; set; } = true;
        public bool EnableAutoRotate { get; set; }
    }
}
