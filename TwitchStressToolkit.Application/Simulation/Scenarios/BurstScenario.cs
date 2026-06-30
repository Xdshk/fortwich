using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Application.Simulation.Scenarios;

public sealed class BurstScenario
{
    private readonly ILogger _logger;

    public string Name => "Burst Load";

    public BurstScenario(ILogger logger)
    {
        _logger = logger;
    }

    public async Task RunAsync(
        Func<VirtualClient> factory,
        Scenario scenario,
        CancellationToken ct)
    {
        _logger.LogInformation("Starting burst scenario with {Count} clients", scenario.ClientCount);

        var tasks = new List<Task>();

        for (int i = 0; i < scenario.ClientCount; i++)
        {
            var client = factory();

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await client.ConnectAsync("testchannel", ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Burst client {Index} failed", i);
                }
            }, ct));
        }

        await Task.WhenAll(tasks);

        _logger.LogInformation("Burst complete");
    }
}
