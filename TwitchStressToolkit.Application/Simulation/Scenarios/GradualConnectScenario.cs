using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Application.Simulation.Scenarios;

public sealed class GradualConnectScenario
{
    private readonly ILogger _logger;
    private readonly IMetricsCollector _metrics;

    public string Name => "Gradual Connect";

    public GradualConnectScenario(ILogger logger, IMetricsCollector metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public async Task RunAsync(
        Func<VirtualClient> factory,
        Scenario scenario,
        CancellationToken ct)
    {
        _logger.LogInformation("Starting gradual connect scenario with {Count} clients", scenario.ClientCount);

        for (int i = 0; i < scenario.ClientCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            var client = factory();

            try
            {
                await client.ConnectAsync("testchannel", ct);
                _metrics.RecordConnection();
            }
            catch (Exception ex)
            {
                _metrics.RecordError(ex.Message);
            }

            await Task.Delay(scenario.ConnectionConfig.RampUpDelayMs, ct);
        }
    }
}
