using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WallpaperApp.Services;

namespace WallpaperApp.ViewModels;

public sealed partial class SettingsCountryItem : ObservableObject
{
    public string Code { get; }
    public string Name { get; }
    public BitmapImage? FlagImage { get; }

    [ObservableProperty]
    private bool _isDefault;

    public SettingsCountryItem(string code, string name, BitmapImage? flagImage)
    {
        Code = code;
        Name = name;
        FlagImage = flagImage;
    }
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private const string FallbackCountryCode = "us";

    private readonly IUserPreferencesService _preferences;

    [ObservableProperty]
    private ObservableCollection<SettingsCountryItem> _countries = new();

    [ObservableProperty]
    private SettingsCountryItem? _selectedDefault;

    public SettingsViewModel(IUserPreferencesService preferences, IReadOnlyList<CountryItem> countries)
    {
        _preferences = preferences;
        Countries = new ObservableCollection<SettingsCountryItem>(
            countries.Select(c => new SettingsCountryItem(c.Code, c.Name, c.FlagImage)));
    }

    public async Task InitializeAsync()
    {
        var prefs = await _preferences.LoadAsync().ConfigureAwait(true);
        var preferred = prefs.DefaultCountryCode;

        var match =
            (!string.IsNullOrEmpty(preferred)
                ? Countries.FirstOrDefault(i => string.Equals(i.Code, preferred, StringComparison.OrdinalIgnoreCase))
                : null)
            ?? Countries.FirstOrDefault(i => string.Equals(i.Code, FallbackCountryCode, StringComparison.OrdinalIgnoreCase))
            ?? Countries.FirstOrDefault();

        ApplySelection(match);
    }

    [RelayCommand]
    private async Task SelectDefaultAsync(SettingsCountryItem item)
    {
        if (item is null) return;
        if (ReferenceEquals(SelectedDefault, item)) return;

        var saved = await _preferences.SetDefaultCountryAsync(item.Code).ConfigureAwait(true);
        if (!saved)
        {
            Debug.WriteLine($"[SettingsViewModel] SetDefaultCountryAsync returned false; leaving UI selection unchanged.");
            return;
        }

        ApplySelection(item);
    }

    private void ApplySelection(SettingsCountryItem? item)
    {
        foreach (var c in Countries)
        {
            c.IsDefault = ReferenceEquals(c, item);
        }
        SelectedDefault = item;
    }
}
