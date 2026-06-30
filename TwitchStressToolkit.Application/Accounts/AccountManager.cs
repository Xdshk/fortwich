using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Core.Models;
using TwitchStressToolkit.Infrastructure.Security;
using TwitchStressToolkit.Infrastructure.Storage;

namespace TwitchStressToolkit.Application.Accounts;

public sealed class AccountManager
{
    private readonly ILogger<AccountManager> _logger;
    private readonly IStorageService _storage;
    private readonly AccountLoader _loader;
    private readonly SecureCredentialService _credentialService;
    private readonly IAuthService _authService;

    private readonly List<BotAccount> _accounts = [];
    private readonly SemaphoreSlim _accountsLock = new(1, 1);

    public IReadOnlyList<BotAccount> Accounts
    {
        get
        {
            _accountsLock.Wait();
            try
            {
                return _accounts.ToList(); // Return a snapshot to avoid concurrent modification
            }
            finally
            {
                _accountsLock.Release();
            }
        }
    }

    public event Action<BotAccount>? AccountAdded;
    public event Action<BotAccount>? AccountRemoved;
    public event Action<BotAccount, string>? AccountStatusChanged;

    public AccountManager(
        ILogger<AccountManager> logger,
        IStorageService storage,
        AccountLoader loader,
        SecureCredentialService credentialService,
        IAuthService authService)
    {
        _logger = logger;
        _storage = storage;
        _loader = loader;
        _credentialService = credentialService;
        _authService = authService;
    }

    public async Task LoadAccountsAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Account file not found: {Path}", filePath);
            return;
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        var accounts = extension switch
        {
            ".json" => await _loader.LoadFromJsonFileAsync(filePath),
            ".txt" => await _loader.LoadFromTextFileAsync(filePath),
            _ => await _loader.LoadFromTextFileAsync(filePath)
        };

        _accountsLock.Wait();
        try
        {
            foreach (var account in accounts)
            {
                account.EncryptedCookies = _credentialService.EncryptAsync(account.Password).GetAwaiter().GetResult();
                _storage.SaveAccountAsync(account).GetAwaiter().GetResult();
                _accounts.Add(account);
            }
        }
        finally
        {
            _accountsLock.Release();
        }

        // Fire events outside the lock to avoid deadlocks
        foreach (var account in accounts)
        {
            AccountAdded?.Invoke(account);
        }

        _logger.LogInformation("Loaded {Count} accounts", accounts.Count);
    }

    public async Task AddAccountAsync(string username, string password, string? authToken = null)
    {
        if (_accounts.Any(a => a.Username == username))
        {
            _logger.LogWarning("Account {Username} already exists", username);
            return;
        }

        var account = new BotAccount
        {
            Username = username,
            Password = password,
            AuthToken = authToken,
            Status = AccountStatus.Pending
        };

        account.EncryptedCookies = await _credentialService.EncryptAsync(password);

        await _accountsLock.WaitAsync();
        try
        {
            await _storage.SaveAccountAsync(account);
            _accounts.Add(account);
        }
        finally
        {
            _accountsLock.Release();
        }

        AccountAdded?.Invoke(account);
        _logger.LogInformation("Added account: {Username}", username);
    }

    public async Task RemoveAccountAsync(Guid accountId)
    {
        var account = _accounts.FirstOrDefault(a => a.Id == accountId);
        if (account is null) return;

        await _accountsLock.WaitAsync();
        try
        {
            await _storage.DeleteAccountAsync(accountId);
            _accounts.Remove(account);
        }
        finally
        {
            _accountsLock.Release();
        }

        AccountRemoved?.Invoke(account);
        _logger.LogInformation("Removed account: {Username}", account.Username);
    }

    public async Task ValidateAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = _accounts.FirstOrDefault(a => a.Id == accountId);
        if (account is null) return;

        try
        {
            var isValid = await _authService.AuthenticateAsync(account, ct);
            account.Status = isValid ? AccountStatus.Valid : AccountStatus.Invalid;
            AccountStatusChanged?.Invoke(account, account.Status.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation failed for {Username}", account.Username);
            account.Status = AccountStatus.Invalid;
        }
    }

    public async Task ValidateAllAccountsAsync(CancellationToken ct = default)
    {
        foreach (var account in _accounts)
        {
            if (ct.IsCancellationRequested) break;
            await ValidateAccountAsync(account.Id, ct);
        }
    }

    public async Task<BotAccount?> GetNextAvailableAsync()
    {
        return _accounts
            .Where(a => a.Status == AccountStatus.Valid)
            .OrderBy(a => a.LastUsedAt)
            .FirstOrDefault();
    }

    public async Task MarkAccountUsedAsync(Guid accountId)
    {
        var account = _accounts.FirstOrDefault(a => a.Id == accountId);
        if (account is not null)
        {
            account.LastUsedAt = DateTime.UtcNow;
            await _storage.SaveAccountAsync(account);
        }
    }

    public async Task ChangeAccountChannelAsync(Guid accountId, string channel)
    {
        var account = _accounts.FirstOrDefault(a => a.Id == accountId);
        if (account is null) return;

        account.CurrentChannel = channel;
        await _storage.SaveAccountAsync(account);

        _logger.LogInformation("Changed channel for {Username} to {Channel}", account.Username, channel);
    }
}
