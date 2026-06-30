using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Infrastructure.Fingerprint;
using TwitchStressToolkit.Infrastructure.Logging;
using TwitchStressToolkit.Infrastructure.Network;
using TwitchStressToolkit.Infrastructure.Proxy;
using TwitchStressToolkit.Infrastructure.Security;
using TwitchStressToolkit.Infrastructure.Storage;

namespace TwitchStressToolkit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string dataDirectory)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [InfrastructureDI] Enter (dataDirectory={dataDirectory})");
        Directory.CreateDirectory(dataDirectory);

        var dbPath = Path.Combine(dataDirectory, "twitch_stress_toolkit.db");
        var logsDirectory = Path.Combine(dataDirectory, "logs");

        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [InfrastructureDI] Storage services");
        services.AddSingleton(new SqliteStorageService(dbPath));
        services.AddSingleton<IStorageService>(sp => sp.GetRequiredService<SqliteStorageService>());

        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [InfrastructureDI] Proxy + Fingerprint services");
        services.AddSingleton<ProxyManager>();
        services.AddSingleton<IProxyManager>(sp => sp.GetRequiredService<ProxyManager>());

        services.AddSingleton<FingerprintManager>();
        services.AddSingleton<IFingerprintManager>(sp => sp.GetRequiredService<FingerprintManager>());

        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [InfrastructureDI] AccountLoader + Credential + IrcClient");
        services.AddSingleton<AccountLoader>();
        services.AddSingleton<SecureCredentialService>();
        // TwitchIrcClient must be Transient — each VirtualClient needs its own instance.
        // Using Singleton causes all clients to share one WebSocket, leading to cross-client disposal crashes.
        services.AddTransient<TwitchIrcClient>();

        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [InfrastructureDI] AuthService");
        services.AddSingleton<IAuthService>(sp =>
        {
            var httpClient = new HttpClient();
            var logger = sp.GetRequiredService<ILogger<TwitchAuthService>>();
            var credentialService = sp.GetRequiredService<SecureCredentialService>();
            return new TwitchAuthService(logger, httpClient, credentialService);
        });

        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [InfrastructureDI] LoggerFactory");
        services.AddSingleton(SerilogConfigurator.CreateLoggerFactory(logsDirectory));

        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [InfrastructureDI] ErrorLogService");
        // Error log service — writes crash/error details to a dedicated file per day
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ErrorLogService>>();
            return new ErrorLogService(logger, dataDirectory);
        });

        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [InfrastructureDI] Complete");
        return services;
    }
}
