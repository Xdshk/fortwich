using System.Threading.Tasks;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Core.Interfaces;

public interface IChatMessageGenerator
{
    Task<string> GenerateAsync(ActivityProfile profile);
    Task InitializeAsync(string filePath);
}
