using System.Text.Json.Serialization;

namespace WallpaperApp.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CloseAction
{
    MinimizeToTray,
    Exit,
}

public sealed class UserPreferences
{
    public const int AutoRefreshIntervalMin = 5;
    public const int AutoRefreshIntervalMax = 1440;
    public const int AutoRefreshIntervalDefault = 60;

    public CloseAction? CloseAction { get; set; }

    public string? DefaultCountryCode { get; set; }

    [JsonPropertyName("autoRefreshEnabled")]
    public bool AutoRefreshEnabled { get; set; } = true;

    [JsonPropertyName("autoRefreshIntervalMinutes")]
    public int AutoRefreshIntervalMinutes { get; set; } = AutoRefreshIntervalDefault;
}