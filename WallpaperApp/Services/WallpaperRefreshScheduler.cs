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
    private readonly IWallpaperSetterService _wallpaperSetter;
    private readonly DesktopStateStore _desktopState;
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
        IUserPreferencesService preferences,
        IWallpaperSetterService wallpaperSetter,
        DesktopStateStore desktopState)
    {
        _fetcher = fetcher;
        _cache = cache;
        _preferences = preferences;
        _wallpaperSetter = wallpaperSetter;
        _desktopState = desktopState;
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

        var allCountries = await TryDiscoverCountriesAsync(ct).ConfigureAwait(false);
        if (allCountries.Count == 0) return;

        var codes = await ResolveCountryCodesAsync(countryCodes, ct).ConfigureAwait(false);
        var resolved = MapCodesToCountries(codes, allCountries);
        if (resolved.Count == 0) return;

        await RefreshManyAsync(resolved, ct).ConfigureAwait(false);
    }

    private async Task RefreshManyAsync(IReadOnlyList<Country> countries, CancellationToken ct)
    {
        var tasks = countries.Select(country => RefreshOneAsync(country, ct)).ToArray();
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch
        {
            // Per-country failures are logged inside RefreshOneAsync; swallow aggregate so other countries proceed.
        }
    }

    private async Task<IReadOnlyList<Country>> TryDiscoverCountriesAsync(CancellationToken ct)
    {
        try
        {
            return await _fetcher.DiscoverCountriesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WallpaperRefreshScheduler] DiscoverCountriesAsync failed: {ex.Message}");
            return Array.Empty<Country>();
        }
    }

    private static List<Country> MapCodesToCountries(IReadOnlyList<string> codes, IReadOnlyList<Country> allCountries)
    {
        var resolved = new List<Country>(codes.Count);
        foreach (var code in codes)
        {
            var country = allCountries.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
            if (country is null)
            {
                Debug.WriteLine($"[WallpaperRefreshScheduler] Country '{code}' not in DiscoverCountriesAsync result; skipping.");
                continue;
            }
            resolved.Add(country);
        }
        return resolved;
    }

    private async Task<List<string>> ResolveCountryCodesAsync(IReadOnlyList<string>? requested, CancellationToken ct)
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

        var fallback = await ResolveDefaultCountryCodeAsync(ct).ConfigureAwait(false);
        return new List<string> { fallback };
    }

    private async Task RefreshOneAsync(Country country, CancellationToken ct)
    {
        var countryCode = country.Code;

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
                    await RunTickAsync(ct).ConfigureAwait(false);
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

    private async Task RunTickAsync(CancellationToken ct)
    {
        // Step 1: refresh every country's cache (slug-gated; only changed countries download).
        var allCountries = await TryDiscoverCountriesAsync(ct).ConfigureAwait(false);
        if (allCountries.Count > 0)
        {
            await RefreshManyAsync(allCountries, ct).ConfigureAwait(false);
        }

        // Step 2: reconcile the desktop against the default country (hash-gated, independent of
        // whether that country's slug changed this tick).
        await ReconcileDesktopAsync(ct).ConfigureAwait(false);
    }

    private async Task ReconcileDesktopAsync(CancellationToken ct)
    {
        try
        {
            var code = await ResolveDefaultCountryCodeAsync(ct).ConfigureAwait(false);

            var loaded = await _cache.TryLoadLatestAsync(code, ct).ConfigureAwait(false);
            if (loaded is null)
            {
                Debug.WriteLine($"[WallpaperRefreshScheduler] Desktop reconcile: no cached image for '{code}'; skipping.");
                return;
            }

            var (imageBytes, metadata) = loaded.Value;
            var hash = ImageHash.Compute(imageBytes);

            var state = await _desktopState.LoadAsync(ct).ConfigureAwait(false);
            if (string.Equals(state.AppliedImageHash, hash, StringComparison.Ordinal))
            {
                // Unchanged since last applied; leave the desktop untouched.
                return;
            }

            try
            {
                _wallpaperSetter.SetWallpaperFromFile(metadata.ImagePath);
            }
            catch (WallpaperSetterException ex)
            {
                // Do not record the new hash, so the next tick retries.
                Debug.WriteLine($"[WallpaperRefreshScheduler] Desktop apply failed for '{code}': {ex.Message}");
                return;
            }

            await _desktopState
                .SaveAsync(new DesktopState { AppliedImageHash = hash, AppliedCountryCode = code }, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stop() / Dispose() was called.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WallpaperRefreshScheduler] Desktop reconcile failed: {ex.Message}");
        }
    }

    private async Task<string> ResolveDefaultCountryCodeAsync(CancellationToken ct)
    {
        var prefs = await _preferences.LoadAsync(ct).ConfigureAwait(false);
        var code = !string.IsNullOrWhiteSpace(prefs.DefaultCountryCode)
            ? prefs.DefaultCountryCode!.ToLowerInvariant()
            : FallbackCountryCode;
        return CountryCodePattern.IsMatch(code) ? code : FallbackCountryCode;
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