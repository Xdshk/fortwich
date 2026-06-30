using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace TwitchStressToolkit.Infrastructure.Logging;



public static class SerilogConfigurator
{
    public static ILoggerFactory CreateLoggerFactory(string logsDirectory)
    {
        Directory.CreateDirectory(logsDirectory);

        var logger = CreateLogger(logsDirectory).CreateLogger();

        return LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(logger, dispose: true);
        });
    }

    public static LoggerConfiguration CreateLogger(string logsDirectory)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logsDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: Path.Combine(logsDirectory, "log-.json"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .Enrich.WithProperty("Application", "TwitchStressToolkit")
            .Enrich.WithProperty("Environment", "Development");
    }

    public static void ConfigureMinimumLevel(LogEventLevel level)
    {
        Log.Logger = (Log.Logger as LoggerConfiguration)?
            .MinimumLevel.Is(level)
            .CreateLogger() ?? Log.Logger;
    }
}
