using System;
using System.Threading.Tasks;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Core.Interfaces;

public interface IFingerprintManager
{
    Task<FingerprintProfile> GenerateAsync();
    Task<FingerprintProfile?> GetForAccountAsync(Guid accountId);
    Task SaveAsync(FingerprintProfile profile);
    Task<FingerprintProfile> GetOrCreateForAccountAsync(Guid accountId);
}
