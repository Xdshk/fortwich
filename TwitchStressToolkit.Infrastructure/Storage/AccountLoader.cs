using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Infrastructure.Storage;

public sealed class AccountLoader
{
    private readonly ILogger<AccountLoader> _logger;

    public AccountLoader(ILogger<AccountLoader> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<BotAccount>> LoadFromTextFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            _logger.LogWarning("Account file not found: {Path}", path);
            return Array.Empty<BotAccount>();
        }

        var lines = await File.ReadAllLinesAsync(path);
        var accounts = new List<BotAccount>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);

            if (parts.Length == 2)
            {
                accounts.Add(new BotAccount
                {
                    Username = parts[0],
                    Password = parts[1]
                });
            }
            else
            {
                _logger.LogWarning("Invalid line format: {Line}", line);
            }
        }

        _logger.LogInformation("Loaded {Count} accounts from {Path}", accounts.Count, path);
        return accounts;
    }

    public async Task<IReadOnlyList<BotAccount>> LoadFromJsonFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            _logger.LogWarning("JSON account file not found: {Path}", path);
            return Array.Empty<BotAccount>();
        }

        var json = await File.ReadAllTextAsync(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        try
        {
            var accounts = JsonSerializer.Deserialize<List<BotAccount>>(json, options);
            if (accounts is not null)
            {
                _logger.LogInformation("Loaded {Count} accounts from JSON {Path}", accounts.Count, path);
                return accounts;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON from {Path}", path);
        }

        return Array.Empty<BotAccount>();
    }

    public async Task SaveAccountsAsync(string path, IReadOnlyList<BotAccount> accounts)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(accounts, options);
        await File.WriteAllTextAsync(path, json);

        _logger.LogInformation("Saved {Count} accounts to {Path}", accounts.Count, path);
    }
}
