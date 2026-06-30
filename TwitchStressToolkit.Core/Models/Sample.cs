using System;

namespace TwitchStressToolkit.Core.Models;

public sealed class Sample
{
    public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public double Value { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}
