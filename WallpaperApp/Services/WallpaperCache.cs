using System.Diagnostics;
using System.IO;
using System.Text.Json;

using WallpaperApp.Models;

namespace WallpaperApp.Services;

public sealed class WallpaperCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    public string GetCacheRoot()
    {
        return HomeDir.GetPath("cache");
    }

    public string GetCountryDir(string countryCode)
    {
        return Path.Combine(GetCacheRoot(), countryCode.ToLowerInvariant());
    }

    public async Task<(byte[] ImageBytes, CachedMetadata Metadata)?> TryLoadTodayAsync(string countryCode,
        CancellationToken ct = default)
    {
        var dir = GetCountryDir(countryCode);
        if (!Directory.Exists(dir)) return null;

        var jsonPath = Directory.EnumerateFiles(dir, "*.json").FirstOrDefault();
        if (jsonPath is null) return null;

        try
        {
            var jsonText = await File.ReadAllTextAsync(jsonPath, ct).ConfigureAwait(false);
            var metadata = JsonSerializer.Deserialize<CachedMetadata>(jsonText, JsonOptions);
            if (metadata is null) return null;

            var todayUtc = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (!string.Equals(metadata.Date, todayUtc, StringComparison.Ordinal))
            {
                return null;
            }

            if (!File.Exists(metadata.ImagePath)) return null;

            var imageBytes = await File.ReadAllBytesAsync(metadata.ImagePath, ct).ConfigureAwait(false);
            return (imageBytes, metadata);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            Debug.WriteLine($"[WallpaperCache] TryLoadTodayAsync failed for {countryCode}: {ex.Message}");
            return null;
        }
    }

    public async Task<CachedMetadata?> SaveAsync(
        Wallpaper wallpaper,
        byte[] imageBytes,
        string resolution,
        CancellationToken ct = default)
    {
        try
        {
            var dir = GetCountryDir(wallpaper.Country.Code);
            Directory.CreateDirectory(dir);

            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var safeSlug = MakeFilenameSafe(wallpaper.Slug);
            var baseName = $"{date}-{safeSlug}";
            var imagePath = Path.Combine(dir, $"{baseName}.jpg");
            var jsonPath = Path.Combine(dir, $"{baseName}.json");

            await File.WriteAllBytesAsync(imagePath, imageBytes, ct).ConfigureAwait(false);

            var metadata = new CachedMetadata
            {
                CountryCode = wallpaper.Country.Code,
                CountryName = wallpaper.Country.Name,
                Date = date,
                Slug = wallpaper.Slug,
                Title = wallpaper.Title,
                Copyright = wallpaper.Copyright,
                DetailUrl = wallpaper.DetailUrl,
                DownloadUrls = new Dictionary<string, string?>(wallpaper.DownloadUrls),
                DownloadedResolution = resolution,
                ImagePath = imagePath,
                Bytes = imageBytes.LongLength,
                FetchedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            };

            var json = JsonSerializer.Serialize(metadata, JsonOptions);
            await File.WriteAllTextAsync(jsonPath, json, ct).ConfigureAwait(false);

            PruneOldEntries(dir, imagePath, jsonPath);

            return metadata;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[WallpaperCache] SaveAsync failed for {wallpaper.Country.Code}: {ex.Message}");
            return null;
        }
    }

    private static void PruneOldEntries(string dir, string keepImagePath, string keepJsonPath)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                if (string.Equals(file, keepImagePath, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(file, keepJsonPath, StringComparison.OrdinalIgnoreCase)) continue;
                if (!file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
                    !file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Debug.WriteLine($"[WallpaperCache] PruneOldEntries: cannot delete {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[WallpaperCache] PruneOldEntries failed in {dir}: {ex.Message}");
        }
    }

    private static string MakeFilenameSafe(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = input.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}