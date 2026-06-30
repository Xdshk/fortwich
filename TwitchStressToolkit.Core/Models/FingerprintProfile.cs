using System;
using System.Text.Json.Serialization;

namespace TwitchStressToolkit.Core.Models;

public sealed class FingerprintProfile
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    [JsonPropertyName("userAgent")]
    public required string UserAgent { get; init; }

    [JsonPropertyName("screenWidth")]
    public int ScreenWidth { get; init; } = 1920;

    [JsonPropertyName("screenHeight")]
    public int ScreenHeight { get; init; } = 1080;

    [JsonPropertyName("language")]
    public string Language { get; init; } = "en-US";

    [JsonPropertyName("timezone")]
    public string Timezone { get; init; } = "UTC";

    [JsonPropertyName("platform")]
    public string Platform { get; init; } = "Win32";

    [JsonPropertyName("webglVendor")]
    public string? WebGLVendor { get; init; }

    [JsonPropertyName("webglRenderer")]
    public string? WebGLRenderer { get; init; }

    [JsonPropertyName("canvasHash")]
    public string? CanvasHash { get; init; }

    [JsonPropertyName("audioHash")]
    public string? AudioHash { get; init; }

    [JsonPropertyName("fonts")]
    public List<string>? Fonts { get; init; }

    [JsonPropertyName("accountId")]
    public Guid? AccountId { get; set; }
}
