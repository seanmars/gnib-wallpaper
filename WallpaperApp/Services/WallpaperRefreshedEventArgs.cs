using WallpaperApp.Models;

namespace WallpaperApp.Services;

public sealed class WallpaperRefreshedEventArgs : EventArgs
{
    public string CountryCode { get; }
    public Wallpaper Wallpaper { get; }
    public byte[] ImageBytes { get; }
    public string? ImagePath { get; }

    public WallpaperRefreshedEventArgs(string countryCode, Wallpaper wallpaper, byte[] imageBytes, string? imagePath)
    {
        CountryCode = countryCode;
        Wallpaper = wallpaper;
        ImageBytes = imageBytes;
        ImagePath = imagePath;
    }
}