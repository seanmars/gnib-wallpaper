using System.Net.Http;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using WallpaperApp.Models;

namespace WallpaperApp.Services;

public sealed class BingFetcher
{
    public const string BaseUrl = "https://bingwallpaper.anerg.com";
    public const string UserAgent = "Mozilla/5.0 (compatible; WallpaperApp/0.1)";

    private static readonly HashSet<string> NonCountryPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "archive", "detail", "about", "api", "search", "tag", "category",
    };

    private static readonly HttpClient Http = CreateHttpClient();
    private static readonly HtmlParser Parser = new();

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return http;
    }

    public async Task<IReadOnlyList<Country>> DiscoverCountriesAsync(CancellationToken ct = default)
    {
        var html = await GetStringAsync(BaseUrl, ct).ConfigureAwait(false);
        var document = await Parser.ParseDocumentAsync(html, ct).ConfigureAwait(false);

        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var anchor in document.QuerySelectorAll("a[href]").OfType<IHtmlAnchorElement>())
        {
            var href = anchor.GetAttribute("href")?.Trim();
            if (string.IsNullOrEmpty(href) || !href.StartsWith("/", StringComparison.Ordinal)) continue;

            var path = href.TrimStart('/').TrimEnd('/');
            if (path.Length != 2) continue;
            if (NonCountryPaths.Contains(path)) continue;
            if (!path.All(char.IsLetter)) continue;

            var code = path.ToLowerInvariant();
            var name = (anchor.TextContent ?? "").Trim();
            if (string.IsNullOrEmpty(name)) name = code.ToUpperInvariant();

            if (!found.ContainsKey(code))
            {
                found[code] = name;
            }
        }

        return found.Select(kv => new Country(kv.Key, kv.Value)).ToList();
    }

    public async Task<DetailLink?> GetTodayDetailLinkAsync(Country country, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/{country.Code}";
        var html = await GetStringAsync(url, ct).ConfigureAwait(false);
        var document = await Parser.ParseDocumentAsync(html, ct).ConfigureAwait(false);

        var prefix = $"/detail/{country.Code}/";
        var anchor = document
            .QuerySelectorAll("a[href]")
            .OfType<IHtmlAnchorElement>()
            .FirstOrDefault(a => (a.GetAttribute("href") ?? "").StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (anchor is null) return null;

        var href = anchor.GetAttribute("href")!;
        var slug = href[prefix.Length..].TrimEnd('/');
        if (slug.Length == 0) return null;

        return new DetailLink(country, slug, $"{BaseUrl}{href}");
    }

    public async Task<Wallpaper> FetchAndParseDetailAsync(DetailLink link, CancellationToken ct = default)
    {
        var html = await GetStringAsync(link.DetailUrl, ct).ConfigureAwait(false);
        var document = await Parser.ParseDocumentAsync(html, ct).ConfigureAwait(false);

        var downloadUrls = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [Resolutions.Uhd4k] = FindImageUrlContaining(document, "w:3840"),
            [Resolutions.Qhd2k] = FindImageUrlContaining(document, "w:2560"),
            [Resolutions.Fhd1080] = FindImageUrlContaining(document, "w:1920"),
        };

        var (title, copyright) = ExtractTitleAndCopyright(document);

        return new Wallpaper(
            link.Country,
            link.Slug,
            title,
            copyright,
            link.DetailUrl,
            downloadUrls);
    }

    public static string? PickBestResolution(Wallpaper wallpaper)
    {
        foreach (var res in Resolutions.Priority)
        {
            if (wallpaper.DownloadUrls.TryGetValue(res, out var url) && !string.IsNullOrEmpty(url))
            {
                return res;
            }
        }
        return null;
    }

    public async Task<byte[]> DownloadImageBytesAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new FetcherException($"Image download timed out: {url}");
        }
        catch (HttpRequestException ex)
        {
            throw new FetcherException($"Image download failed: {url} ({ex.Message})", ex);
        }
    }

    private async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await Http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new FetcherException($"HTTP {(int)response.StatusCode} for {url}");
            }
            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new FetcherException($"Request timed out: {url}");
        }
        catch (HttpRequestException ex)
        {
            throw new FetcherException($"Request failed: {url} ({ex.Message})", ex);
        }
    }

    private static string? FindImageUrlContaining(IDocument document, string marker)
    {
        foreach (var anchor in document.QuerySelectorAll("a[href]").OfType<IHtmlAnchorElement>())
        {
            var href = anchor.GetAttribute("href") ?? "";
            if (href.Contains(marker, StringComparison.Ordinal) &&
                href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return href;
            }
        }

        foreach (var img in document.QuerySelectorAll("img[src]").OfType<IHtmlImageElement>())
        {
            var src = img.GetAttribute("src") ?? "";
            if (src.Contains(marker, StringComparison.Ordinal) &&
                src.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return src;
            }
        }

        return null;
    }

    private static (string Title, string Copyright) ExtractTitleAndCopyright(IDocument document)
    {
        var img = document.QuerySelectorAll("img[alt]").OfType<IHtmlImageElement>().FirstOrDefault();
        if (img is not null)
        {
            var alt = img.GetAttribute("alt") ?? "";
            var open = alt.IndexOf('(');
            var close = alt.LastIndexOf(')');
            if (open > 0 && close > open)
            {
                var title = alt[..open].Trim();
                var inside = alt.Substring(open + 1, close - open - 1).Trim();
                if (inside.StartsWith("©", StringComparison.Ordinal))
                {
                    return (title, inside);
                }
            }
            if (!string.IsNullOrWhiteSpace(alt))
            {
                return (alt.Trim(), "");
            }
        }

        var title2 = document.QuerySelector("title")?.TextContent?.Trim() ?? "";
        return (title2, "");
    }
}
