using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using WallpaperApp.Models;
using WallpaperApp.Services;
using WallpaperApp.Views;

namespace WallpaperApp.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const string FallbackCountryCode = "us";
    private const int MaxConcurrentDownloads = 4;

    private readonly BingFetcher _fetcher;
    private readonly WallpaperCache _cache;
    private readonly FlagCache _flagCache;
    private readonly IUserPreferencesService _preferences;
    private readonly IWallpaperSetterService _wallpaperSetter;
    private readonly IWallpaperRefreshScheduler? _refreshScheduler;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _inFlight =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _httpGate =
        new(initialCount: MaxConcurrentDownloads, maxCount: MaxConcurrentDownloads);

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
        : this(new BingFetcher(), new WallpaperCache(), new FlagCache(), new UserPreferencesService(), new WallpaperSetterService(), refreshScheduler: null)
    {
    }

    public MainViewModel(
        BingFetcher fetcher,
        WallpaperCache cache,
        FlagCache flagCache,
        IUserPreferencesService preferences,
        IWallpaperSetterService wallpaperSetter,
        IWallpaperRefreshScheduler? refreshScheduler)
    {
        _fetcher = fetcher;
        _cache = cache;
        _flagCache = flagCache;
        _preferences = preferences;
        _wallpaperSetter = wallpaperSetter;
        _refreshScheduler = refreshScheduler;

        if (_refreshScheduler is not null)
        {
            _refreshScheduler.WallpaperRefreshed += OnWallpaperRefreshed;
        }
    }

    public IWallpaperRefreshScheduler? RefreshScheduler => _refreshScheduler;

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

            var prefs = await _preferences.LoadAsync().ConfigureAwait(true);
            var preferred = prefs.DefaultCountryCode;

            var defaultItem =
                (!string.IsNullOrEmpty(preferred)
                    ? items.FirstOrDefault(i => string.Equals(i.Code, preferred, StringComparison.OrdinalIgnoreCase))
                    : null)
                ?? items.FirstOrDefault(i => string.Equals(i.Code, FallbackCountryCode, StringComparison.OrdinalIgnoreCase))
                ?? items.FirstOrDefault();

            if (defaultItem is null)
            {
                State = LoadState.Error;
                ErrorMessage = "No countries discovered from the homepage.";
                return;
            }

            SelectCountry(defaultItem);

            foreach (var item in items)
            {
                EnsureLoadStarted(item);
            }
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
    private void SetAsDesktop()
    {
        var path = SelectedCountry?.CachedImagePath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            _wallpaperSetter.SetWallpaperFromFile(path);
        }
        catch (WallpaperSetterException ex)
        {
            MessageBox.Show(
                $"Failed to set desktop wallpaper: {ex.Message}",
                "Set Wallpaper",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var vm = new SettingsViewModel(_preferences, Countries.ToList());
        var window = new SettingsWindow
        {
            DataContext = vm,
            Owner = Application.Current.MainWindow,
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void SelectCountry(CountryItem item)
    {
        if (item is null) return;

        if (SelectedCountry is { } prev && !ReferenceEquals(prev, item))
        {
            prev.IsSelected = false;
        }
        item.IsSelected = true;
        SelectedCountry = item;

        if (item.LoadState == LoadState.Error)
        {
            EnsureLoadStarted(item);
            return;
        }

        if (item.LoadState != LoadState.Loaded && !_inFlight.ContainsKey(item.Code))
        {
            EnsureLoadStarted(item);
        }
    }

    partial void OnSelectedCountryChanged(CountryItem? oldValue, CountryItem? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnSelectedCountryPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnSelectedCountryPropertyChanged;
            SyncStateFromSelected(newValue);
        }
        else
        {
            State = LoadState.Loading;
            ErrorMessage = "";
            CurrentImage = null;
            CurrentWallpaper = null;
            UpdateWindowTitle();
        }
    }

    private void OnSelectedCountryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not CountryItem item) return;
        if (!ReferenceEquals(item, SelectedCountry)) return;

        switch (e.PropertyName)
        {
            case nameof(CountryItem.LoadState):
            case nameof(CountryItem.ErrorMessage):
            case nameof(CountryItem.CachedImage):
            case nameof(CountryItem.CachedWallpaper):
                SyncStateFromSelected(item);
                break;
        }
    }

    private void SyncStateFromSelected(CountryItem item)
    {
        State = item.LoadState;
        ErrorMessage = item.ErrorMessage;
        CurrentImage = item.CachedImage;
        CurrentWallpaper = item.CachedWallpaper;
        UpdateWindowTitle();
    }

    private void EnsureLoadStarted(CountryItem item)
    {
        if (item.LoadState == LoadState.Loaded && item.CachedImage is not null) return;
        if (_inFlight.ContainsKey(item.Code) && item.LoadState == LoadState.Loading) return;

        var cts = new CancellationTokenSource();
        if (!_inFlight.TryAdd(item.Code, cts))
        {
            cts.Dispose();
            return;
        }

        _ = LoadCountryAsync(item, cts);
    }

    private async Task LoadCountryAsync(CountryItem item, CancellationTokenSource cts)
    {
        var ct = cts.Token;
        item.LoadState = LoadState.Loading;
        item.ErrorMessage = "";

        var country = item.Country;
        var gateTaken = false;

        try
        {
            var cached = await _cache.TryLoadTodayAsync(country.Code, ct).ConfigureAwait(true);
            if (cached is not null)
            {
                if (ct.IsCancellationRequested) return;
                ApplyResultToItem(item, country, cached.Value.ImageBytes, cached.Value.Metadata);
                return;
            }

            await _httpGate.WaitAsync(ct).ConfigureAwait(true);
            gateTaken = true;

            var link = await _fetcher.GetTodayDetailLinkAsync(country, ct).ConfigureAwait(true);
            if (link is null)
            {
                if (ct.IsCancellationRequested) return;
                item.LoadState = LoadState.Error;
                item.ErrorMessage = $"No wallpaper found for {country.Name} today.";
                return;
            }

            var wallpaper = await _fetcher.FetchAndParseDetailAsync(link, ct).ConfigureAwait(true);
            var resolution = BingFetcher.PickBestResolution(wallpaper);
            if (resolution is null || !wallpaper.DownloadUrls.TryGetValue(resolution, out var url) || string.IsNullOrEmpty(url))
            {
                if (ct.IsCancellationRequested) return;
                item.LoadState = LoadState.Error;
                item.ErrorMessage = $"No downloadable URL for {country.Name}.";
                return;
            }

            var bytes = await _fetcher.DownloadImageBytesAsync(url, ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested) return;

            var saved = await _cache.SaveAsync(wallpaper, bytes, resolution, ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested) return;

            ApplyDownloadedToItem(item, wallpaper, bytes, saved?.ImagePath);
        }
        catch (OperationCanceledException)
        {
            // ct is per-country; cancellation means an explicit reset — stay silent.
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;
            item.LoadState = LoadState.Error;
            item.ErrorMessage = $"Failed to load wallpaper for {country.Name}: {ex.Message}";
        }
        finally
        {
            if (gateTaken)
            {
                _httpGate.Release();
            }
            if (_inFlight.TryGetValue(item.Code, out var current) && ReferenceEquals(current, cts))
            {
                _inFlight.TryRemove(item.Code, out _);
                cts.Dispose();
            }
        }
    }

    private static void ApplyResultToItem(CountryItem item, Country country, byte[] imageBytes, CachedMetadata metadata)
    {
        var wallpaper = new Wallpaper(
            country,
            metadata.Slug,
            metadata.Title,
            metadata.Copyright,
            metadata.DetailUrl,
            metadata.DownloadUrls);
        item.CachedWallpaper = wallpaper;
        item.CachedImage = LoadBitmap(imageBytes);
        item.CachedImagePath = metadata.ImagePath;
        item.LoadState = LoadState.Loaded;
    }

    private static void ApplyDownloadedToItem(CountryItem item, Wallpaper wallpaper, byte[] bytes, string? imagePath)
    {
        item.CachedWallpaper = wallpaper;
        item.CachedImage = LoadBitmap(bytes);
        item.CachedImagePath = imagePath;
        item.LoadState = LoadState.Loaded;
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

    private void OnWallpaperRefreshed(object? sender, WallpaperRefreshedEventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        if (dispatcher.CheckAccess())
        {
            ApplyRefresh(e);
        }
        else
        {
            dispatcher.BeginInvoke(new Action(() => ApplyRefresh(e)));
        }
    }

    private void ApplyRefresh(WallpaperRefreshedEventArgs e)
    {
        var item = Countries.FirstOrDefault(c =>
            string.Equals(c.Code, e.CountryCode, StringComparison.OrdinalIgnoreCase));
        if (item is null) return;

        item.CachedWallpaper = e.Wallpaper;
        item.CachedImage = LoadBitmap(e.ImageBytes);
        item.CachedImagePath = e.ImagePath;
        item.LoadState = LoadState.Loaded;
        item.ErrorMessage = "";
    }

    public void Dispose()
    {
        if (_refreshScheduler is not null)
        {
            _refreshScheduler.WallpaperRefreshed -= OnWallpaperRefreshed;
        }
    }
}