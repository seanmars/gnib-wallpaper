using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace WallpaperApp.Services;

public sealed class FlagCache
{
    private const string FlagCdnTemplate = "https://flagcdn.com/w80/{0}.png";

    private static readonly Dictionary<string, string> CodeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["uk"] = "gb",
    };

    private static readonly HttpClient Http = CreateHttpClient();

    private readonly Dictionary<string, BitmapImage> _memory = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _memoryLock = new(1, 1);

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(BingFetcher.UserAgent);
        return http;
    }

    public string GetFlagsRoot()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "WallpaperApp", "flags");
    }

    public async Task<BitmapImage?> GetFlagAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var key = code.Trim().ToLowerInvariant();

        await _memoryLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_memory.TryGetValue(key, out var cached)) return cached;
        }
        finally
        {
            _memoryLock.Release();
        }

        var remoteCode = CodeAliases.TryGetValue(key, out var alias) ? alias : key;
        var dir = GetFlagsRoot();
        try { Directory.CreateDirectory(dir); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[FlagCache] CreateDirectory failed: {ex.Message}");
        }

        var path = Path.Combine(dir, $"{key}.png");
        byte[]? bytes = null;

        if (File.Exists(path))
        {
            try { bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException)
            {
                Debug.WriteLine($"[FlagCache] read failed for {key}: {ex.Message}");
            }
        }

        if (bytes is null || bytes.Length == 0)
        {
            try
            {
                var url = string.Format(FlagCdnTemplate, remoteCode);
                bytes = await Http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
                try { await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false); }
                catch (Exception ex) when (ex is IOException)
                {
                    Debug.WriteLine($"[FlagCache] write failed for {key}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FlagCache] download failed for {key}: {ex.Message}");
                return null;
            }
        }

        var bmp = LoadBitmap(bytes);

        await _memoryLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _memory[key] = bmp;
        }
        finally
        {
            _memoryLock.Release();
        }

        return bmp;
    }

    private static BitmapImage LoadBitmap(byte[] bytes)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = new MemoryStream(bytes);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}