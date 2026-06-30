using CommunityToolkit.Mvvm.ComponentModel;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.UI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
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

    public ConnectionConfig ToConfig() => new()
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
        EnableChat = EnableChat,
        EnableViewer = EnableViewer,
        EnableReconnects = EnableReconnects
    };
}
