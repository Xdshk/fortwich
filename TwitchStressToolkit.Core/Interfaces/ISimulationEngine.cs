using System;
using System.Threading;
using System.Threading.Tasks;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Core.Interfaces;

public interface ISimulationEngine : IAsyncDisposable
{
    Guid Id { get; }
    bool IsRunning { get; }
    ConnectionConfig Config { get; }
    SimulationResult? LastResult { get; }

    event Action<SimulationResult>? SimulationCompleted;
    event Action<string>? LogGenerated;

    Task StartAsync(ConnectionConfig config, CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    Task<SimulationResult> RunScenarioAsync(Scenario scenario, CancellationToken ct);
}
