using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

using WallpaperApp.Models;

namespace WallpaperApp.Services;

public sealed class WallpaperRefreshScheduler : IWallpaperRefreshScheduler
{
    private const string FallbackCountryCode = "us";
    private const int MaxConcurrentRefresh = 2;

    private static readonly Regex CountryCodePattern = new("^[a-z]{2}$", RegexOptions.Compiled);

    private readonly BingFetcher _fetcher;
    private readonly WallpaperCache _cache;
    private readonly IUserPreferencesService _preferences;
    private readonly SemaphoreSlim _refreshGate =
        new(initialCount: MaxConcurrentRefresh, maxCount: MaxConcurrentRefresh);
    private readonly object _stateLock = new();

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private TimeSpan _currentInterval;
    private bool _disposed;

    public event EventHandler<WallpaperRefreshedEventArgs>? WallpaperRefreshed;

    public bool Started { get; private set; }

    public WallpaperRefreshScheduler(
        BingFetcher fetcher,
        WallpaperCache cache,
        IUserPreferencesService preferences)
    {
        _fetcher = fetcher;
        _cache = cache;
        _preferences = preferences;
        _preferences.PreferencesChanged += OnPreferencesChanged;
    }

    public void Start()
    {
        lock (_stateLock)
        {
            ThrowIfDisposed();
            if (Started) return;
            StartLoopLocked();
        }
    }

    public void Stop()
    {
        CancellationTokenSource? toCancel;
        Task? toWait;
        lock (_stateLock)
        {
            if (!Started) return;
            Started = false;
            toCancel = _loopCts;
            toWait = _loopTask;
            _loopCts = null;
            _loopTask = null;
        }

        try
        {
            toCancel?.Cancel();
        }
        catch (ObjectDisposedException) { }

        try
        {
            toWait?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException) { }
        catch (OperationCanceledException) { }
        finally
        {
            toCancel?.Dispose();
        }
    }

    public async Task RefreshAsync(IReadOnlyList<string>? countryCodes, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var resolved = await ResolveCountriesAsync(countryCodes, ct).ConfigureAwait(false);
        if (resolved.Count == 0) return;

        var tasks = resolved.Select(code => RefreshOneAsync(code, ct)).ToArray();
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch
        {
            // Per-country failures are logged inside RefreshOneAsync; swallow aggregate so other countries proceed.
        }
    }

    private async Task<List<string>> ResolveCountriesAsync(IReadOnlyList<string>? requested, CancellationToken ct)
    {
        if (requested is { Count: > 0 })
        {
            var filtered = new List<string>(requested.Count);
            foreach (var code in requested)
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    Debug.WriteLine("[WallpaperRefreshScheduler] Skipped empty country code.");
                    continue;
                }
                if (!CountryCodePattern.IsMatch(code))
                {
                    Debug.WriteLine($"[WallpaperRefreshScheduler] Skipped invalid country code: '{code}'.");
                    continue;
                }
                filtered.Add(code);
            }
            return filtered;
        }

        var prefs = await _preferences.LoadAsync(ct).ConfigureAwait(false);
        var fallback = !string.IsNullOrWhiteSpace(prefs.DefaultCountryCode)
            ? prefs.DefaultCountryCode!.ToLowerInvariant()
            : FallbackCountryCode;
        return CountryCodePattern.IsMatch(fallback)
            ? new List<string> { fallback }
            : new List<string> { FallbackCountryCode };
    }

    private async Task RefreshOneAsync(string countryCode, CancellationToken ct)
    {
        var country = await ResolveCountryAsync(countryCode, ct).ConfigureAwait(false);
        if (country is null)
        {
            Debug.WriteLine($"[WallpaperRefreshScheduler] Country '{countryCode}' not in DiscoverCountriesAsync result; skipping.");
            return;
        }

        try
        {
            await _refreshGate.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            var link = await _fetcher.GetTodayDetailLinkAsync(country, ct).ConfigureAwait(false);
            if (link is null)
            {
                Debug.WriteLine($"[WallpaperRefreshScheduler] No detail link for {countryCode} today.");
                return;
            }

            var existingSlug = await TryReadCachedSlugAsync(countryCode, ct).ConfigureAwait(false);
            if (existingSlug is not null && string.Equals(existingSlug, link.Slug, StringComparison.Ordinal))
            {
                return;
            }

            var wallpaper = await _fetcher.FetchAndParseDetailAsync(link, ct).ConfigureAwait(false);
            var resolution = BingFetcher.PickBestResolution(wallpaper);
            if (resolution is null || !wallpaper.DownloadUrls.TryGetValue(resolution, out var url) || string.IsNullOrEmpty(url))
            {
                Debug.WriteLine($"[WallpaperRefreshScheduler] No downloadable URL for {countryCode}.");
                return;
            }

            var bytes = await _fetcher.DownloadImageBytesAsync(url, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            var saved = await _cache.SaveAsync(wallpaper, bytes, resolution, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            RaiseWallpaperRefreshed(countryCode, wallpaper, bytes, saved?.ImagePath);
        }
        catch (OperationCanceledException)
        {
            // Stop() was called.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WallpaperRefreshScheduler] Refresh for {countryCode} failed: {ex.Message}");
        }
        finally
        {
            try { _refreshGate.Release(); } catch (ObjectDisposedException) { }
        }
    }

    private async Task<Country?> ResolveCountryAsync(string countryCode, CancellationToken ct)
    {
        try
        {
            var countries = await _fetcher.DiscoverCountriesAsync(ct).ConfigureAwait(false);
            return countries.FirstOrDefault(c => string.Equals(c.Code, countryCode, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WallpaperRefreshScheduler] DiscoverCountriesAsync failed: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> TryReadCachedSlugAsync(string countryCode, CancellationToken ct)
    {
        var dir = _cache.GetCountryDir(countryCode);
        if (!Directory.Exists(dir)) return null;

        try
        {
            var jsonPath = Directory.EnumerateFiles(dir, "*.json").FirstOrDefault();
            if (jsonPath is null) return null;

            var jsonText = await File.ReadAllTextAsync(jsonPath, ct).ConfigureAwait(false);
            var metadata = JsonSerializer.Deserialize<CachedMetadata>(jsonText);
            return metadata?.Slug;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            Debug.WriteLine($"[WallpaperRefreshScheduler] TryReadCachedSlugAsync failed for {countryCode}: {ex.Message}");
            return null;
        }
    }

    private void RaiseWallpaperRefreshed(string countryCode, Wallpaper wallpaper, byte[] bytes, string? imagePath)
    {
        var handler = WallpaperRefreshed;
        if (handler is null) return;

        var args = new WallpaperRefreshedEventArgs(countryCode, wallpaper, bytes, imagePath);
        foreach (var subscriber in handler.GetInvocationList().Cast<EventHandler<WallpaperRefreshedEventArgs>>())
        {
            try
            {
                subscriber(this, args);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WallpaperRefreshScheduler] WallpaperRefreshed subscriber threw: {ex.Message}");
            }
        }
    }

    private void StartLoopLocked()
    {
        var prefs = TryLoadPreferencesBlocking();
        _currentInterval = ComputeIntervalFromPrefs(prefs);

        var cts = new CancellationTokenSource();
        _loopCts = cts;
        Started = true;
        _loopTask = Task.Run(() => RunLoopAsync(_currentInterval, cts.Token));
    }

    private async Task RunLoopAsync(TimeSpan interval, CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    await RefreshAsync(null, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WallpaperRefreshScheduler] Tick failed: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal stop / dispose.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WallpaperRefreshScheduler] Loop terminated unexpectedly: {ex.Message}");
        }
    }

    private void OnPreferencesChanged(object? sender, UserPreferences prefs)
    {
        lock (_stateLock)
        {
            if (_disposed) return;

            var newInterval = ComputeIntervalFromPrefs(prefs);
            var shouldRun = prefs.AutoRefreshEnabled;
            var intervalChanged = newInterval != _currentInterval;

            if (!shouldRun && Started)
            {
                StopLocked();
                return;
            }

            if (shouldRun && !Started)
            {
                StartLoopLocked();
                return;
            }

            if (shouldRun && Started && intervalChanged)
            {
                StopLocked();
                StartLoopLocked();
            }
        }
    }

    private void StopLocked()
    {
        if (!Started) return;
        Started = false;
        var toCancel = _loopCts;
        _loopCts = null;
        _loopTask = null;
        try { toCancel?.Cancel(); } catch (ObjectDisposedException) { }
        toCancel?.Dispose();
    }

    private UserPreferences TryLoadPreferencesBlocking()
    {
        try
        {
            return _preferences.LoadAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WallpaperRefreshScheduler] Preference load failed: {ex.Message}");
            return new UserPreferences();
        }
    }

    private static TimeSpan ComputeIntervalFromPrefs(UserPreferences prefs)
    {
        var minutes = Math.Clamp(
            prefs.AutoRefreshIntervalMinutes,
            UserPreferences.AutoRefreshIntervalMin,
            UserPreferences.AutoRefreshIntervalMax);
        return TimeSpan.FromMinutes(minutes);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WallpaperRefreshScheduler));
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed) return;
            _disposed = true;
            _preferences.PreferencesChanged -= OnPreferencesChanged;
            StopLocked();
        }
        _refreshGate.Dispose();
    }
}
