using System.Threading;
using System.Threading.Tasks;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Core.Interfaces;

public interface IAuthService
{
    Task<bool> AuthenticateAsync(BotAccount account, CancellationToken ct = default);
    Task<bool> ValidateSessionAsync(BotAccount account, CancellationToken ct = default);
    Task<string?> GetAuthTokenAsync(BotAccount account, CancellationToken ct = default);
    Task RefreshTokenAsync(BotAccount account, CancellationToken ct = default);
}
