using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchStressToolkit.Core.Interfaces;
using TwitchStressToolkit.Core.Models;

namespace TwitchStressToolkit.Application.Chat;

public sealed class ChatMessageGenerator : IChatMessageGenerator
{
    private readonly ILogger<ChatMessageGenerator> _logger;
    private readonly List<string> _templates = [];
    // ThreadLocal<Random> because Random is not thread-safe and GenerateAsync can be called
    // concurrently from multiple VirtualClient worker loops.
    private static readonly ThreadLocal<Random> _random = new(() => new Random(Guid.NewGuid().GetHashCode()));

    public ChatMessageGenerator(ILogger<ChatMessageGenerator> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Message file not found: {Path}, using defaults", filePath);
            _templates.AddRange(GetDefaultMessages());
            return;
        }

        var lines = await File.ReadAllLinesAsync(filePath);
        var messages = lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim())
            .Distinct()
            .ToList();

        _templates.AddRange(messages);
        _logger.LogInformation("Loaded {Count} message templates", messages.Count);
    }

    public Task<string> GenerateAsync(ActivityProfile profile)
    {
        if (_templates.Count == 0)
        {
            return Task.FromResult("Hello!");
        }

        var message = _templates[_random.Value!.Next(_templates.Count)];

        if (_random.Value!.NextDouble() < profile.TypoChance)
        {
            message = IntroduceTypo(message);
        }

        if (_random.Value!.NextDouble() < profile.EmojiChance)
        {
            message = AppendEmoji(message);
        }

        return Task.FromResult(message);
    }

    private string IntroduceTypo(string message)
    {
        if (message.Length < 3) return message;

        var index = _random.Value!.Next(1, message.Length - 1);
        var chars = message.ToCharArray();

        (chars[index], chars[index - 1]) = (chars[index - 1], chars[index]);

        return new string(chars);
    }

    private string AppendEmoji(string message)
    {
        var emojis = new[] { "😀", "😂", "🔥", "❤️", "👍", "😍", "🎉", "💯", "🚀", "✨", "😎", "🤔", "😅", "🙌" };
        var emoji = emojis[_random.Value!.Next(emojis.Length)];
        return _random.Value!.NextDouble() < 0.5 ? $"{emoji} {message}" : $"{message} {emoji}";
    }

    private static List<string> GetDefaultMessages() =>
    [
        "привет", "хахахаха", "как дела", "нормик", "че делаешь",
        "ого круто", "GG", "wp", "nice", "топ стрим", "лол", "кек",
        "❤️", "🔥", "👍", "😀", "😂"
    ];
}
