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
    public CloseAction? CloseAction { get; set; }
}
