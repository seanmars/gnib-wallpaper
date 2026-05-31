using System.Text.Json.Serialization;

namespace WallpaperApp.Models;

/// <summary>
/// Persisted runtime state describing the wallpaper currently applied to the desktop.
/// Stored as a single value (not per-country) so switching the default country
/// re-applies correctly. Kept separate from <see cref="UserPreferences"/>.
/// </summary>
public sealed class DesktopState
{
    [JsonPropertyName("appliedImageHash")]
    public string? AppliedImageHash { get; set; }

    [JsonPropertyName("appliedCountryCode")]
    public string? AppliedCountryCode { get; set; }
}
