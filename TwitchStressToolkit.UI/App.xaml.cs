using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using TwitchStressToolkit.Application;
using TwitchStressToolkit.Infrastructure;
using TwitchStressToolkit.Infrastructure.Logging;
using TwitchStressToolkit.Infrastructure.Storage;
using TwitchStressToolkit.UI.ViewModels;
using TwitchStressToolkit.UI.Views;

namespace TwitchStressToolkit.UI;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private ErrorLogService? _errorLog;
    private StreamWriter? _traceLog;
    private static readonly object _traceLock = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Write a marker to the trace file FIRST, before anything else, so we can
        // see whether the app even got to OnStartup.
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TwitchStressToolkit");
        Directory.CreateDirectory(dataDirectory);
        var tracePath = Path.Combine(dataDirectory, "logs", "trace.log");
        // FileShare.ReadWrite позволяет другим процессам (редакторы, антивирус)
        // читать файл, пока мы пишем. Если файл заблокирован — работаем без
        // трейс-лога, но приложение не падает.
        try
        {
            var fs = new FileStream(tracePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _traceLog = new StreamWriter(fs) { AutoFlush = true };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Cannot open trace.log: {ex.Message}");
        }
        if (_traceLog != null)
        {
            Trace.Listeners.Add(new TextWriterTraceListener(_traceLog));
        }
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] OnStartup enter (PID={Environment.ProcessId})");

        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] OnStartup enter");
        base.OnStartup(e);

        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] base.OnStartup done");

        // Catch EVERY exception the moment it is thrown (before any catch block).
        // This is critical for diagnosing silent crashes.
        AppDomain.CurrentDomain.FirstChanceException += (_, args) =>
        {
            try
            {
                var ex = args.Exception;
                Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FirstChance] {ex.GetType().FullName}: {ex.Message}");
                Trace.WriteLine($"  Stack: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Trace.WriteLine($"  Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                }
            }
            catch { /* swallow — don't crash the handler */ }
        };

        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] Logger creating");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(Path.Combine(dataDirectory, "logs", "log-.txt"), rollingInterval: RollingInterval.Day)
            .WriteTo.Console()
            .CreateLogger();

        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] Logger created; wiring global exception handlers");

        // Global exception handlers — log crashes that would otherwise terminate the process
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] Building host");
            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices(services =>
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] DI: registering Application services");
                    services.AddApplication();
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] DI: registering Infrastructure services");
                    services.AddInfrastructure(dataDirectory);

                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] DI: registering UI ViewModels/Windows");
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<DashboardViewModel>();
                    services.AddSingleton<BotManagerViewModel>();
                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<ChartsViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] Host built; starting");

            await _host.StartAsync();
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] Host started");

            // Resolve error log service for global crash logging
            _errorLog = _host.Services.GetRequiredService<ErrorLogService>();
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] ErrorLogService resolved");

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] MainWindow resolved; showing");

            mainWindow.Show();
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] MainWindow shown");

            _errorLog.LogError("App", "Application started successfully");
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] OnStartup complete");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] FATAL STARTUP: {ex}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] FATAL STARTUP: {ex}");
            Log.Fatal(ex, "Application failed to start");
            _errorLog?.LogError("Startup", "Application failed to start", ex);
            MessageBox.Show($"Failed to start:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void WriteCrashDump(string context, Exception exception)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TwitchStressToolkit", "logs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "crash.log");
            var text = $"[{DateTime.UtcNow:O}] [{context}] {exception.GetType().FullName}: {exception.Message}{Environment.NewLine}{exception.StackTrace}{Environment.NewLine}";
            if (exception.InnerException is not null)
            {
                text += $"  Inner: {exception.InnerException.GetType().FullName}: {exception.InnerException.Message}{Environment.NewLine}{exception.InnerException.StackTrace}{Environment.NewLine}";
            }
            text += Environment.NewLine;
            File.AppendAllText(path, text);
        }
        catch { /* last resort — ignore */ }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] FATAL DispatcherUnhandledException: {e.Exception}");
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] FATAL DispatcherUnhandledException: {e.Exception}");
        WriteCrashDump("UIThread", e.Exception);
        _errorLog?.LogError("UIThread", $"Unhandled UI exception: {e.Exception.Message}", e.Exception);
        Log.Fatal(e.Exception, "Unhandled UI thread exception");
        MessageBox.Show($"A fatal error occurred:\n{e.Exception.Message}\n\nThe application will close.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(1);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] FATAL AppDomain.UnhandledException: {exception}");
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] FATAL AppDomain.UnhandledException: {exception}");
        if (exception is not null)
        {
            WriteCrashDump("AppDomain", exception);
        }
        _errorLog?.LogError("AppDomain", $"Unhandled domain exception: {exception?.Message ?? "Unknown"}", exception);
        Log.Fatal(exception, "Unhandled app domain exception");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] UnobservedTaskException: {e.Exception}");
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] UnobservedTaskException: {e.Exception}");
        WriteCrashDump("TaskScheduler", e.Exception?.Flatten() ?? new Exception("Unknown"));
        _errorLog?.LogError("TaskScheduler", $"Unobserved task exception: {e.Exception?.Message ?? "Unknown"}", e.Exception?.Flatten());
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved(); // Prevent process termination
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] OnExit enter (ExitCode={e.ApplicationExitCode})");
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] OnExit enter (ExitCode={e.ApplicationExitCode})");
        // Detach handlers first so a crash during shutdown isn't re-routed here.
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

        // Stop the host — this disposes all IAsyncDisposable singletons, including
        // ErrorLogService. Do NOT dispose _errorLog separately afterwards.
        if (_host is not null)
        {
            try
            {
                Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] Stopping host");
                await _host.StopAsync();
                Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] Host stopped");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] Host stop error: {ex}");
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] Host stop error: {ex}");
                WriteCrashDump("Shutdown", ex);
            }

            _host.Dispose();
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] Host disposed");
        }

        Log.CloseAndFlush();
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] Log closed; calling base.OnExit");
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [App] Log closed; calling base.OnExit");

        base.OnExit(e);
    }
}
