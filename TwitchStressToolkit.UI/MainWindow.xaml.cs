using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            SetFallbackContent(name, ex);
        }
    }

    private void SetFallbackContent(string viewName, Exception exception)
    {
        var fallback = new Border
        {
            CornerRadius = new CornerRadius(18),
            Margin = new Thickness(24),
            Padding = new Thickness(24),
            Background = new SolidColorBrush(Color.FromRgb(26, 35, 48)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(255, 107, 107)),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = $"{viewName} is temporarily unavailable",
                        FontSize = 22,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    },
                    new TextBlock
                    {
                        Margin = new Thickness(0, 10, 0, 0),
                        Text = exception.Message,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.FromRgb(152, 167, 184))
                    }
                }
            }
        };

        switch (viewName)
        {
            case "Dashboard":
                DashboardContent.Content = fallback;
                break;
            case "BotManager":
                BotManagerContent.Content = fallback;
                break;
            case "Charts":
                ChartsContent.Content = fallback;
                break;
            case "Settings":
                SettingsContent.Content = fallback;
                break;
        }

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.AddLog($"{viewName} view fallback activated: {exception.Message}");
        }
    }
}
