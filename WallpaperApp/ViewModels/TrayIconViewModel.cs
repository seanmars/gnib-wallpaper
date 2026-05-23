using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using WallpaperApp.Services;

namespace WallpaperApp.ViewModels;

public sealed partial class TrayIconViewModel : ObservableObject
{
    private readonly IWindowCloseService _windowClose;
    private readonly IUserPreferencesService _preferences;

    public TrayIconViewModel(IWindowCloseService windowClose, IUserPreferencesService preferences)
    {
        _windowClose = windowClose;
        _preferences = preferences;
    }

    [RelayCommand]
    private void ToggleWindow() => _windowClose.ToggleWindow();

    [RelayCommand]
    private void RestoreWindow() => _windowClose.RestoreWindow();

    [RelayCommand]
    private async Task ResetPreferenceAsync() => await _preferences.ResetCloseActionAsync().ConfigureAwait(true);

    [RelayCommand]
    private void Exit() => _windowClose.ExitApplication();
}