using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Application.Simulation.Scenarios;

public sealed class MassConnectScenario
{
    private readonly ILogger _logger;
    private readonly IMetricsCollector _metrics;

    public string Name => "Mass Connect";

    public MassConnectScenario(ILogger logger, IMetricsCollector metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public async Task RunAsync(
        Func<VirtualClient> factory,
        Scenario scenario,
        CancellationToken ct)
    {
        _logger.LogInformation("Starting mass connect scenario with {Count} clients", scenario.ClientCount);

        var tasks = new List<Task>();

        for (int i = 0; i < scenario.ClientCount; i++)
        {
            var client = factory();

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await client.ConnectAsync("testchannel", ct);
                    _metrics.RecordConnection();
                }
                catch (Exception ex)
                {
                    _metrics.RecordError(ex.Message);
                }
            }, ct));
        }

        await Task.WhenAll(tasks);
    }
}
