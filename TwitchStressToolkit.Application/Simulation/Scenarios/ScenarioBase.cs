using System;
using System.Threading;
using System.Threading.Tasks;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Application.Simulation.Scenarios;

public abstract class ScenarioBase
{
    public abstract string Name { get; }
    public async Task ExecuteAsync(
        Func<VirtualClient> factory,
        Scenario scenario,
        IMetricsCollector metrics,
        CancellationToken ct)
    {
        await RunAsync(factory, scenario, metrics, ct);
    }

    protected abstract Task RunAsync(
        Func<VirtualClient> factory,
        Scenario scenario,
        IMetricsCollector metrics,
        CancellationToken ct);
}
