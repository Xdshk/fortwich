using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Core.Models;
using TwitchStressToolkit.Application.Accounts;
using TwitchStressToolkit.Application.Chat;
using TwitchStressToolkit.Application.Metrics;
using Sim = TwitchStressToolkit.Application.Simulation;
using TwitchStressToolkit.Infrastructure.Logging;
using TwitchStressToolkit.Infrastructure.Network;

namespace TwitchStressToolkit.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ApplicationDI] Chat-message services");
        services.AddSingleton<ChatMessageGenerator>();
        services.AddSingleton<IChatMessageGenerator>(sp => sp.GetRequiredService<ChatMessageGenerator>());

        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ApplicationDI] Metrics services (Collector + Bus)");
        services.AddSingleton<MetricsCollector>();
        services.AddSingleton<IMetricsCollector>(sp => sp.GetRequiredService<MetricsCollector>());

        services.AddSingleton<MetricsBus>();

        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ApplicationDI] Accounts service");
        services.AddSingleton<AccountManager>();

        services.AddSingleton<Sim.SimulationEngine>();
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ApplicationDI] Simulation engine");
        services.AddSingleton<ISimulationEngine>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Sim.SimulationEngine>>();
            var metrics = sp.GetRequiredService<IMetricsCollector>();
            var proxyManager = sp.GetRequiredService<IProxyManager>();
            var fingerprintManager = sp.GetRequiredService<IFingerprintManager>();
            var authService = sp.GetRequiredService<IAuthService>();
            var accountManager = sp.GetRequiredService<AccountManager>();
            var chatGenerator = sp.GetRequiredService<IChatMessageGenerator>();
            var activityProfile = ActivityProfile.Default;
            var errorLog = sp.GetRequiredService<Infrastructure.Logging.ErrorLogService>();

            return new Sim.SimulationEngine(
                logger,
                metrics,
                proxyManager,
                fingerprintManager,
                authService,
                accountManager,
                () =>
                {
                    var clientLogger = sp.GetRequiredService<ILogger<Sim.VirtualClient>>();
                    var errorLog = sp.GetRequiredService<ErrorLogService>();
                    var ircClient = sp.GetRequiredService<TwitchIrcClient>();
                    var proxyManagerInner = sp.GetRequiredService<IProxyManager>();
                    return new Sim.VirtualClient(clientLogger, errorLog, ircClient, chatGenerator, proxyManagerInner, activityProfile);
                },
                errorLog);
        });

        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ApplicationDI] Complete");
        return services;
    }
}
