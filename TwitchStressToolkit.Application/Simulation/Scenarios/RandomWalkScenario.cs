using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Application.Simulation.Scenarios;

public sealed class RandomWalkScenario
{
    private readonly ILogger _logger;
    private readonly Random _random = new();

    public string Name => "Random Walk";

    public RandomWalkScenario(ILogger logger)
    {
        _logger = logger;
    }

    public async Task RunAsync(
        Func<VirtualClient> factory,
        Scenario scenario,
        CancellationToken ct)
    {
        _logger.LogInformation("Starting random walk scenario with {Count} clients", scenario.ClientCount);

        for (int i = 0; i < scenario.ClientCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            var delay = _random.Next(100, 5000);
            await Task.Delay(delay, ct);

            var client = factory();

            try
            {
                await client.ConnectAsync("testchannel", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Random walk client {Index} failed", i);
            }
        }
    }
}
