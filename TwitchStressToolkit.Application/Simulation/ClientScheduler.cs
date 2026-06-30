using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Application.Simulation;

public sealed class ClientScheduler
{
    private readonly ILogger<ClientScheduler> _logger;
    private readonly VirtualClient _client;
    private readonly ActivityProfile _profile;
    private readonly Random _random = new();

    public ClientScheduler(
        ILogger<ClientScheduler> logger,
        VirtualClient client,
        ActivityProfile profile)
    {
        _logger = logger;
        _client = client;
        _profile = profile;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await ExecuteJoinAsync(ct);
            await ExecuteActiveLoopAsync(ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduler error for client {Id}", _client.Id);
        }
    }

    private async Task ExecuteJoinAsync(CancellationToken ct)
    {
        _logger.LogDebug("[Scheduler] Client {Id} joining...", _client.Id);
        await Task.Delay(_random.Next(100, 500), ct);
    }

    private async Task ExecuteActiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var action = GetNextAction();

            switch (action)
            {
                case SchedulerAction.SendMessage:
                    await ExecuteSendMessageAsync(ct);
                    break;

                case SchedulerAction.Idle:
                    await ExecuteIdleAsync(ct);
                    break;

                case SchedulerAction.Reconnect:
                    await ExecuteReconnectAsync(ct);
                    break;

                case SchedulerAction.Heartbeat:
                    await ExecuteHeartbeatAsync(ct);
                    break;
            }

            var delay = _random.Next(_profile.MinDelayMs, _profile.MaxDelayMs);
            await Task.Delay(delay, ct);
        }
    }

    private async Task ExecuteSendMessageAsync(CancellationToken ct)
    {
        _logger.LogDebug("[Scheduler] Client {Id} sending message", _client.Id);
        await Task.CompletedTask;
    }

    private async Task ExecuteIdleAsync(CancellationToken ct)
    {
        _logger.LogDebug("[Scheduler] Client {Id} idle", _client.Id);
        await Task.Delay(_random.Next(1000, 5000), ct);
    }

    private async Task ExecuteReconnectAsync(CancellationToken ct)
    {
        _logger.LogDebug("[Scheduler] Client {Id} reconnecting", _client.Id);
        await Task.Delay(_random.Next(1000, 3000), ct);
    }

    private async Task ExecuteHeartbeatAsync(CancellationToken ct)
    {
        _logger.LogDebug("[Scheduler] Client {Id} heartbeat", _client.Id);
        await Task.CompletedTask;
    }

    private SchedulerAction GetNextAction()
    {
        var roll = _random.NextDouble();

        return roll switch
        {
            < 0.6 => SchedulerAction.SendMessage,
            < 0.85 => SchedulerAction.Idle,
            < 0.95 => SchedulerAction.Heartbeat,
            _ => SchedulerAction.Reconnect
        };
    }
}

internal enum SchedulerAction
{
    SendMessage,
    Idle,
    Reconnect,
    Heartbeat
}
