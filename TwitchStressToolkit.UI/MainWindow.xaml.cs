using System.Diagnostics;
using System.Windows;
using TwitchStressToolkit.UI.ViewModels;
using TwitchStressToolkit.UI.Views;

namespace TwitchStressToolkit.UI;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel, DashboardViewModel dashboard, BotManagerViewModel botManager, SettingsViewModel settings, ChartsViewModel charts)
    {
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainWindow] Constructor enter");
        InitializeComponent();
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainWindow] InitializeComponent done");
        DataContext = viewModel;
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainWindow] DataContext set");

        Loaded += (_, _) => Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainWindow] Loaded");

        // Оборачиваем каждый UserControl в try/catch, чтобы точно увидеть,
        // какой из них падает при создании (если XAML содержит ошибку).
        TryCreate("Dashboard", () => DashboardContent.Content = new DashboardView { DataContext = dashboard });
        TryCreate("BotManager", () => BotManagerContent.Content = new BotManagerView { DataContext = botManager });
        TryCreate("Charts", () => ChartsContent.Content = new ChartsView { DataContext = charts });
        TryCreate("Settings", () => SettingsContent.Content = new SettingsView { DataContext = settings });
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainWindow] Constructor complete");

        // Navigate to Dashboard view by default
        (DataContext as MainViewModel)?.ShowDashboardCommand?.Execute(null);
    }

    private void TryCreate(string name, Action action)
    {
        try
        {
            action();
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainWindow] {name} view created");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MainWindow] FAILED to create {name} view: {ex}");
        }
    }
}
