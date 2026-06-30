using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Application.Simulation.Scenarios;

public sealed class WaveScenario
{
    private readonly ILogger _logger;

    public string Name => "Wave Load";

    public WaveScenario(ILogger logger)
    {
        _logger = logger;
    }

    public async Task RunAsync(
        Func<VirtualClient> factory,
        Scenario scenario,
        CancellationToken ct)
    {
        _logger.LogInformation("Starting wave scenario with {Count} clients", scenario.ClientCount);

        for (int wave = 0; wave < 3; wave++)
        {
            _logger.LogInformation("Wave {Wave} starting...", wave + 1);

            for (int i = 0; i < scenario.ClientCount / 3; i++)
            {
                var client = factory();

                try
                {
                    await client.ConnectAsync("testchannel", ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Wave {Wave} client {Index} failed", wave, i);
                }

                await Task.Delay(500, ct);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }
}
