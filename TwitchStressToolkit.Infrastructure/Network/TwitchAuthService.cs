using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Exceptions;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Core.Models;
using TwitchStressToolkit.Infrastructure.Security;

namespace TwitchStressToolkit.Infrastructure.Network;

public sealed class TwitchAuthService : IAuthService, IDisposable
{
    private readonly ILogger<TwitchAuthService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SecureCredentialService _credentialService;

    private const string TokenUrl = "https://id.twitch.tv/oauth2/token";
    private const string ValidateUrl = "https://id.twitch.tv/oauth2/validate";

    public TwitchAuthService(
        ILogger<TwitchAuthService> logger,
        HttpClient httpClient,
        SecureCredentialService credentialService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _credentialService = credentialService;
    }

    public async Task<bool> AuthenticateAsync(BotAccount account, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Authenticating account: {Username}", account.Username);

            if (!string.IsNullOrEmpty(account.AuthToken))
            {
                return await ValidateTokenAsync(account.AuthToken, ct);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication failed for {Username}", account.Username);
            throw new AuthenticationException(
                "Authentication failed",
                account.Username,
                ex.Message);
        }
    }

    public async Task<bool> ValidateSessionAsync(BotAccount account, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(account.AuthToken))
        {
            return false;
        }

        return await ValidateTokenAsync(account.AuthToken, ct);
    }

    public async Task<string?> GetAuthTokenAsync(BotAccount account, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(account.AuthToken))
        {
            return account.AuthToken;
        }

        return null;
    }

    public async Task RefreshTokenAsync(BotAccount account, CancellationToken ct = default)
    {
        _logger.LogInformation("Refreshing token for {Username}", account.Username);
        await Task.CompletedTask;
    }

    private async Task<bool> ValidateTokenAsync(string token, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ValidateUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                _logger.LogDebug("Token validation successful");
                return true;
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Token validation failed: {Status} - {Error}",
                response.StatusCode, error);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token validation error");
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
