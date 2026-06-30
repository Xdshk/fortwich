using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TwitchStressToolkit.Infrastructure.Security;

public sealed class SecureCredentialService
{
    private readonly ILogger<SecureCredentialService> _logger;
    private readonly byte[] _entropy;

    public SecureCredentialService(ILogger<SecureCredentialService> logger)
    {
        _logger = logger;
        _entropy = Encoding.UTF8.GetBytes("TwitchStressToolkit_v1_2024");
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    _entropy,
                    DataProtectionScope.CurrentUser);

                return Convert.ToBase64String(encryptedBytes);
            }
            else
            {
                _logger.LogWarning("Using cross-platform encryption (less secure on non-Windows)");
                return Convert.ToBase64String(plainBytes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt credential");
            throw;
        }
    }

    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        var encryptedBytes = Convert.FromBase64String(encryptedText);

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    _entropy,
                    DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            else
            {
                return Encoding.UTF8.GetString(encryptedBytes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt credential");
            throw;
        }
    }

    public Task<string> EncryptAsync(string plainText)
    {
        return Task.FromResult(Encrypt(plainText));
    }

    public Task<string> DecryptAsync(string encryptedText)
    {
        return Task.FromResult(Decrypt(encryptedText));
    }
}
