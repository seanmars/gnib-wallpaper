using System.Text.Json.Serialization;

namespace WallpaperApp.Models;

public sealed class CachedMetadata
{
    [JsonPropertyName("country_code")]
    public string CountryCode { get; set; } = "";

    [JsonPropertyName("country_name")]
    public string CountryName { get; set; } = "";

    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("copyright")]
    public string Copyright { get; set; } = "";

    [JsonPropertyName("detail_url")]
    public string DetailUrl { get; set; } = "";

    [JsonPropertyName("download_urls")]
    public Dictionary<string, string?> DownloadUrls { get; set; } = new();

    [JsonPropertyName("downloaded_resolution")]
    public string DownloadedResolution { get; set; } = "";

    [JsonPropertyName("image_path")]
    public string ImagePath { get; set; } = "";

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }

    [JsonPropertyName("fetched_at")]
    public string FetchedAt { get; set; } = "";
}