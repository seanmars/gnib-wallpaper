using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WallpaperApp.Models;
using WallpaperApp.Services;

namespace WallpaperApp.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private const string DefaultCountryCode = "us";

    private readonly BingFetcher _fetcher;
    private readonly WallpaperCache _cache;
    private readonly FlagCache _flagCache;
    private CancellationTokenSource? _activeCts;

    [ObservableProperty]
    private ObservableCollection<CountryItem> _countries = new();

    [ObservableProperty]
    private CountryItem? _selectedCountry;

    [ObservableProperty]
    private BitmapImage? _currentImage;

    [ObservableProperty]
    private Wallpaper? _currentWallpaper;

    [ObservableProperty]
    private LoadState _state = LoadState.Loading;

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private string _windowTitle = "Bing Wallpaper";

    public MainViewModel()
        : this(new BingFetcher(), new WallpaperCache(), new FlagCache())
    {
    }

    public MainViewModel(BingFetcher fetcher, WallpaperCache cache, FlagCache flagCache)
    {
        _fetcher = fetcher;
        _cache = cache;
        _flagCache = flagCache;
    }

    public async Task InitializeAsync()
    {
        State = LoadState.Loading;
        try
        {
            var countries = await _fetcher.DiscoverCountriesAsync().ConfigureAwait(true);
            var items = countries.Select(c => new CountryItem(c)).ToList();
            Countries = new ObservableCollection<CountryItem>(items);

            foreach (var item in items)
            {
                _ = LoadFlagAsync(item);
            }

            var defaultItem =
                items.FirstOrDefault(i => string.Equals(i.Code, DefaultCountryCode, StringComparison.OrdinalIgnoreCase))
                ?? items.FirstOrDefault();

            if (defaultItem is null)
            {
                State = LoadState.Error;
                ErrorMessage = "No countries discovered from the homepage.";
                return;
            }

            await SelectCountryAsync(defaultItem).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            State = LoadState.Error;
            ErrorMessage = $"Failed to initialize: {ex.Message}";
        }
    }

    private async Task LoadFlagAsync(CountryItem item)
    {
        try
        {
            var bmp = await _flagCache.GetFlagAsync(item.Code).ConfigureAwait(true);
            if (bmp is not null)
            {
                item.FlagImage = bmp;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] LoadFlag failed for {item.Code}: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SelectCountryAsync(CountryItem item)
    {
        if (item is null) return;

        _activeCts?.Cancel();
        var cts = new CancellationTokenSource();
        _activeCts = cts;
        var ct = cts.Token;

        if (SelectedCountry is { } prev && !ReferenceEquals(prev, item))
        {
            prev.IsSelected = false;
        }
        item.IsSelected = true;
        SelectedCountry = item;
        var country = item.Country;
        UpdateWindowTitle();
        ErrorMessage = "";

        try
        {
            var cached = await _cache.TryLoadTodayAsync(country.Code, ct).ConfigureAwait(true);
            if (cached is not null)
            {
                if (ct.IsCancellationRequested) return;
                ApplyCached(country, cached.Value.ImageBytes, cached.Value.Metadata);
                return;
            }

            State = LoadState.Loading;

            var link = await _fetcher.GetTodayDetailLinkAsync(country, ct).ConfigureAwait(true);
            if (link is null)
            {
                if (ct.IsCancellationRequested) return;
                State = LoadState.Error;
                ErrorMessage = $"No wallpaper found for {country.Name} today.";
                return;
            }

            var wallpaper = await _fetcher.FetchAndParseDetailAsync(link, ct).ConfigureAwait(true);
            var resolution = BingFetcher.PickBestResolution(wallpaper);
            if (resolution is null || !wallpaper.DownloadUrls.TryGetValue(resolution, out var url) || string.IsNullOrEmpty(url))
            {
                if (ct.IsCancellationRequested) return;
                State = LoadState.Error;
                ErrorMessage = $"No downloadable URL for {country.Name}.";
                return;
            }

            var bytes = await _fetcher.DownloadImageBytesAsync(url, ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested) return;

            await _cache.SaveAsync(wallpaper, bytes, resolution, ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested) return;

            CurrentWallpaper = wallpaper;
            CurrentImage = LoadBitmap(bytes);
            State = LoadState.Loaded;
            UpdateWindowTitle();
        }
        catch (OperationCanceledException)
        {
            // user switched country, ignore
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;
            State = LoadState.Error;
            ErrorMessage = $"Failed to load wallpaper for {country.Name}: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_activeCts, cts))
            {
                _activeCts = null;
            }
            cts.Dispose();
        }
    }

    private void ApplyCached(Country country, byte[] imageBytes, CachedMetadata metadata)
    {
        CurrentWallpaper = new Wallpaper(
            country,
            metadata.Slug,
            metadata.Title,
            metadata.Copyright,
            metadata.DetailUrl,
            metadata.DownloadUrls);
        CurrentImage = LoadBitmap(imageBytes);
        State = LoadState.Loaded;
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        var name = SelectedCountry?.Name ?? "";
        var title = CurrentWallpaper?.Title;
        WindowTitle = string.IsNullOrEmpty(title)
            ? $"Bing Wallpaper - {name}"
            : $"Bing Wallpaper - {name} - {title}";
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
