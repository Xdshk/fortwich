using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Core.Models;
using TwitchStressToolkit.Infrastructure.Storage;

namespace TwitchStressToolkit.Infrastructure.Fingerprint;

public sealed class FingerprintManager : IFingerprintManager, IAsyncDisposable
{
    private readonly ILogger<FingerprintManager> _logger;
    private readonly SqliteStorageService _storage;
    private readonly Random _random = new();
    private bool _disposed;

    private static readonly string[] UserAgents = [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko/20100101 Firefox/132.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Safari/605.1.15"
    ];

    private static readonly string[] Languages = ["en-US", "en-GB", "ru-RU", "de-DE", "fr-FR", "es-ES"];
    private static readonly string[] Timezones = ["America/New_York", "America/Chicago", "America/Los_Angeles", "Europe/London", "Europe/Moscow", "Europe/Berlin"];
    private static readonly (int Width, int Height)[] Resolutions = [(1920, 1080), (2560, 1440), (1366, 768), (1536, 864), (1440, 900)];

    public FingerprintManager(ILogger<FingerprintManager> logger, SqliteStorageService storage)
    {
        _logger = logger;
        _storage = storage;
    }

    public Task<FingerprintProfile> GenerateAsync()
    {
        var ua = UserAgents[Random.Shared.Next(UserAgents.Length)];
        var (width, height) = Resolutions[Random.Shared.Next(Resolutions.Length)];
        var lang = Languages[Random.Shared.Next(Languages.Length)];
        var tz = Timezones[Random.Shared.Next(Timezones.Length)];

        var profile = new FingerprintProfile
        {
            UserAgent = ua,
            ScreenWidth = width,
            ScreenHeight = height,
            Language = lang,
            Timezone = tz,
            Platform = "Win32",
            WebGLVendor = "Google Inc. (NVIDIA)",
            WebGLRenderer = "ANGLE (NVIDIA GeForce GTX 1660 SUPER Direct3D11 vs_5_0 ps_5_0)",
            CanvasHash = GenerateHash(),
            AudioHash = GenerateHash()
        };

        _logger.LogDebug("Generated fingerprint for UA: {UA}", ua[..Math.Min(50, ua.Length)]);
        return Task.FromResult(profile);
    }

    public async Task<FingerprintProfile?> GetForAccountAsync(Guid accountId)
    {
        var accounts = await _storage.GetAccountsAsync();
        var account = accounts.FirstOrDefault(a => a.Id == accountId);

        if (account?.FingerprintId is null) return null;

        return await GenerateAsync();
    }

    public async Task SaveAsync(FingerprintProfile profile)
    {
        _logger.LogDebug("Saving fingerprint {Id}", profile.Id);
    }

    public async Task<FingerprintProfile> GetOrCreateForAccountAsync(Guid accountId)
    {
        var existing = await GetForAccountAsync(accountId);
        if (existing is not null) return existing;

        var profile = await GenerateAsync();
        profile.AccountId = accountId;
        await SaveAsync(profile);

        return profile;
    }

    private static string GenerateHash()
    {
        var bytes = new byte[16];
        Random.Shared.NextBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
