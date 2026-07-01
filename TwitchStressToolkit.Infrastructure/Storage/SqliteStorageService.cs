using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Infrastructure.Storage;

public sealed class SqliteStorageService : IStorageService
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;

    public SqliteStorageService(string dbPath)
    {
        Batteries.Init();

        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        _connectionLock.Dispose();
    }

    private async Task<SqliteConnection> GetConnectionAsync(CancellationToken ct)
    {
        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_connection is null)
            {
                _connection = new SqliteConnection(_connectionString);
                await _connection.OpenAsync(ct);
                await InitializeSchemaAsyncInner(ct);
            }

            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task InitializeSchemaAsyncInner(CancellationToken ct)
    {
        if (_connection is null) return;
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Accounts (
                Id TEXT PRIMARY KEY,
                Username TEXT NOT NULL,
                Password TEXT NOT NULL,
                AuthToken TEXT,
                Cookies TEXT,
                Status INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                LastUsedAt TEXT,
                BanCount INTEGER NOT NULL DEFAULT 0,
                CurrentChannel TEXT,
                ProxyId TEXT,
                FingerprintId TEXT
            );

            CREATE TABLE IF NOT EXISTS SimulationResults (
                Id TEXT PRIMARY KEY,
                StartedAt TEXT NOT NULL,
                FinishedAt TEXT,
                TotalClients INTEGER NOT NULL,
                SuccessfulConnections INTEGER NOT NULL,
                FailedConnections INTEGER NOT NULL,
                TotalMessages INTEGER NOT NULL,
                TotalReconnects INTEGER NOT NULL,
                BanCount INTEGER NOT NULL,
                AverageLatencyMs REAL NOT NULL,
                P95LatencyMs REAL NOT NULL,
                P99LatencyMs REAL NOT NULL,
                PeakCpuUsage REAL NOT NULL,
                PeakMemoryMb REAL NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ActivityLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AccountId TEXT NOT NULL,
                Channel TEXT NOT NULL,
                Action TEXT NOT NULL,
                Timestamp TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Accounts_Username ON Accounts(Username);
            CREATE INDEX IF NOT EXISTS IX_ActivityLogs_AccountId ON ActivityLogs(AccountId);
            CREATE INDEX IF NOT EXISTS IX_SimulationResults_StartedAt ON SimulationResults(StartedAt DESC);
        ";

        await command.ExecuteNonQueryAsync(ct);
    }

    private Task InitializeSchemaAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task SaveAccountAsync(BotAccount account, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Accounts (Id, Username, Password, AuthToken, Cookies, Status, CreatedAt, LastUsedAt, BanCount, CurrentChannel, ProxyId, FingerprintId)
            VALUES (@Id, @Username, @Password, @AuthToken, @Cookies, @Status, @CreatedAt, @LastUsedAt, @BanCount, @CurrentChannel, @ProxyId, @FingerprintId)
            ON CONFLICT(Id) DO UPDATE SET
                AuthToken = excluded.AuthToken,
                Cookies = excluded.Cookies,
                Status = excluded.Status,
                LastUsedAt = excluded.LastUsedAt,
                BanCount = excluded.BanCount,
                CurrentChannel = excluded.CurrentChannel,
                ProxyId = excluded.ProxyId,
                FingerprintId = excluded.FingerprintId
        ";

        command.Parameters.AddWithValue("@Id", account.Id.ToString());
        command.Parameters.AddWithValue("@Username", account.Username);
        command.Parameters.AddWithValue("@Password", account.Password);
        command.Parameters.AddWithValue("@AuthToken", (object?)account.AuthToken ?? DBNull.Value);
        command.Parameters.AddWithValue("@Cookies", (object?)account.EncryptedCookies ?? DBNull.Value);
        command.Parameters.AddWithValue("@Status", (int)account.Status);
        command.Parameters.AddWithValue("@CreatedAt", account.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@LastUsedAt", (object?)account.LastUsedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("@BanCount", account.BanCount);
        command.Parameters.AddWithValue("@CurrentChannel", (object?)account.CurrentChannel ?? DBNull.Value);
        command.Parameters.AddWithValue("@ProxyId", (object?)account.ProxyId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("@FingerprintId", (object?)account.FingerprintId?.ToString() ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<BotAccount>> GetAccountsAsync(CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        var accounts = new List<BotAccount>();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Accounts ORDER BY CreatedAt DESC";

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            accounts.Add(MapAccount(reader));
        }

        return accounts;
    }

    public async Task<BotAccount?> GetAccountAsync(Guid id, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Accounts WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id.ToString());

        using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapAccount(reader);
        }

        return null;
    }

    public async Task DeleteAccountAsync(Guid id, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Accounts WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id.ToString());

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveSimulationResultAsync(SimulationResult result, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO SimulationResults (Id, StartedAt, FinishedAt, TotalClients, SuccessfulConnections, FailedConnections, TotalMessages, TotalReconnects, BanCount, AverageLatencyMs, P95LatencyMs, P99LatencyMs, PeakCpuUsage, PeakMemoryMb)
            VALUES (@Id, @StartedAt, @FinishedAt, @TotalClients, @SuccessfulConnections, @FailedConnections, @TotalMessages, @TotalReconnects, @BanCount, @AverageLatencyMs, @P95LatencyMs, @P99LatencyMs, @PeakCpuUsage, @PeakMemoryMb)
        ";

        command.Parameters.AddWithValue("@Id", result.Id.ToString());
        command.Parameters.AddWithValue("@StartedAt", result.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("@FinishedAt", (object?)result.FinishedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("@TotalClients", result.TotalClients);
        command.Parameters.AddWithValue("@SuccessfulConnections", result.SuccessfulConnections);
        command.Parameters.AddWithValue("@FailedConnections", result.FailedConnections);
        command.Parameters.AddWithValue("@TotalMessages", result.TotalMessages);
        command.Parameters.AddWithValue("@TotalReconnects", result.TotalReconnects);
        command.Parameters.AddWithValue("@BanCount", result.BanCount);
        command.Parameters.AddWithValue("@AverageLatencyMs", result.AverageLatencyMs);
        command.Parameters.AddWithValue("@P95LatencyMs", result.P95LatencyMs);
        command.Parameters.AddWithValue("@P99LatencyMs", result.P99LatencyMs);
        command.Parameters.AddWithValue("@PeakCpuUsage", result.PeakCpuUsage);
        command.Parameters.AddWithValue("@PeakMemoryMb", result.PeakMemoryMb);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<SimulationResult>> GetSimulationResultsAsync(int limit = 100, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        var results = new List<SimulationResult>();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM SimulationResults ORDER BY StartedAt DESC LIMIT @Limit";
        command.Parameters.AddWithValue("@Limit", limit);

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(MapResult(reader));
        }

        return results;
    }

    public async Task SaveActivityLogAsync(Guid accountId, string channel, string action, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO ActivityLogs (AccountId, Channel, Action, Timestamp)
            VALUES (@AccountId, @Channel, @Action, @Timestamp)
        ";

        command.Parameters.AddWithValue("@AccountId", accountId.ToString());
        command.Parameters.AddWithValue("@Channel", channel);
        command.Parameters.AddWithValue("@Action", action);
        command.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ActivityLogItem>> GetActivityLogsAsync(Guid accountId, int limit = 100, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        var logs = new List<ActivityLogItem>();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM ActivityLogs WHERE AccountId = @AccountId ORDER BY Timestamp DESC LIMIT @Limit";
        command.Parameters.AddWithValue("@AccountId", accountId.ToString());
        command.Parameters.AddWithValue("@Limit", limit);

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            logs.Add(new ActivityLogItem
            {
                Id = reader.GetInt64(0),
                AccountId = Guid.Parse(reader.GetString(1)),
                Channel = reader.GetString(2),
                Action = reader.GetString(3),
                Timestamp = DateTime.Parse(reader.GetString(4))
            });
        }

        return logs;
    }

    private static BotAccount MapAccount(SqliteDataReader reader)
    {
        return new BotAccount
        {
            Id = Guid.Parse(reader.GetString(0)),
            Username = reader.GetString(1),
            Password = reader.GetString(2),
            AuthToken = reader.IsDBNull(3) ? null : reader.GetString(3),
            EncryptedCookies = reader.IsDBNull(4) ? null : reader.GetString(4),
            Status = (AccountStatus)reader.GetInt32(5),
            CreatedAt = DateTime.Parse(reader.GetString(6)),
            LastUsedAt = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
            BanCount = reader.GetInt32(8),
            CurrentChannel = reader.IsDBNull(9) ? null : reader.GetString(9),
            ProxyId = reader.IsDBNull(10) ? null : Guid.Parse(reader.GetString(10)),
            FingerprintId = reader.IsDBNull(11) ? null : Guid.Parse(reader.GetString(11))
        };
    }

    private static SimulationResult MapResult(SqliteDataReader reader)
    {
        return new SimulationResult
        {
            Id = Guid.Parse(reader.GetString(0)),
            StartedAt = DateTime.Parse(reader.GetString(1)),
            FinishedAt = reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2)),
            TotalClients = reader.GetInt32(3),
            SuccessfulConnections = reader.GetInt32(4),
            FailedConnections = reader.GetInt32(5),
            TotalMessages = reader.GetInt64(6),
            TotalReconnects = reader.GetInt32(7),
            BanCount = reader.GetInt32(8),
            AverageLatencyMs = reader.GetDouble(9),
            P95LatencyMs = reader.GetDouble(10),
            P99LatencyMs = reader.GetDouble(11),
            PeakCpuUsage = reader.GetDouble(12),
            PeakMemoryMb = reader.GetDouble(13)
        };
    }
}
