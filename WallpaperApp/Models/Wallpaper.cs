namespace WallpaperApp.Models;

public static class Resolutions
{
    public const string Uhd4k = "uhd_4k";
    public const string Qhd2k = "qhd_2k";
    public const string Fhd1080 = "fhd_1080";

    public static readonly IReadOnlyList<string> Priority = new[] { Uhd4k, Qhd2k, Fhd1080 };
}

public sealed record Wallpaper(
    Country Country,
    string Slug,
    string Title,
    string Copyright,
    string DetailUrl,
    IReadOnlyDictionary<string, string?> DownloadUrls);