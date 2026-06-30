using System.Text.Json.Serialization;

namespace TwitchStressToolkit.Core.Models;

public sealed class ActivityProfile
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("messagesPerMinute")]
    public double MessagesPerMinute { get; init; }

    [JsonPropertyName("typoChance")]
    public double TypoChance { get; init; }

    [JsonPropertyName("emojiChance")]
    public double EmojiChance { get; init; }

    [JsonPropertyName("minDelayMs")]
    public int MinDelayMs { get; init; }

    [JsonPropertyName("maxDelayMs")]
    public int MaxDelayMs { get; init; }

    public static ActivityProfile Default => new()
    {
        Name = "Normal",
        MessagesPerMinute = 2,
        TypoChance = 0.05,
        EmojiChance = 0.15,
        MinDelayMs = 1500,
        MaxDelayMs = 5000
    };

    public static ActivityProfile Casual => new()
    {
        Name = "Casual",
        MessagesPerMinute = 0.5,
        TypoChance = 0.02,
        EmojiChance = 0.1,
        MinDelayMs = 5000,
        MaxDelayMs = 30000
    };

    public static ActivityProfile Active => new()
    {
        Name = "Active",
        MessagesPerMinute = 5,
        TypoChance = 0.1,
        EmojiChance = 0.25,
        MinDelayMs = 800,
        MaxDelayMs = 2000
    };

    public static ActivityProfile Spammer => new()
    {
        Name = "Spammer",
        MessagesPerMinute = 15,
        TypoChance = 0.0,
        EmojiChance = 0.0,
        MinDelayMs = 50,
        MaxDelayMs = 200
    };
}
