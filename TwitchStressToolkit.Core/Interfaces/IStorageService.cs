using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Core.Interfaces;

public interface IStorageService : IAsyncDisposable
{
    Task SaveAccountAsync(BotAccount account, CancellationToken ct = default);
    Task<IReadOnlyList<BotAccount>> GetAccountsAsync(CancellationToken ct = default);
    Task<BotAccount?> GetAccountAsync(Guid id, CancellationToken ct = default);
    Task DeleteAccountAsync(Guid id, CancellationToken ct = default);

    Task SaveSimulationResultAsync(SimulationResult result, CancellationToken ct = default);
    Task<IReadOnlyList<SimulationResult>> GetSimulationResultsAsync(int limit = 100, CancellationToken ct = default);

    Task SaveActivityLogAsync(Guid accountId, string channel, string action, CancellationToken ct = default);
    Task<IReadOnlyList<ActivityLogItem>> GetActivityLogsAsync(Guid accountId, int limit = 100, CancellationToken ct = default);
}

public sealed class ActivityLogItem
{
    public long Id { get; init; }
    public Guid AccountId { get; init; }
    public string Channel { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
