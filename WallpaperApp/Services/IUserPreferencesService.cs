using WallpaperApp.Models;

namespace WallpaperApp.Services;

public interface IUserPreferencesService
{
    event EventHandler<UserPreferences>? PreferencesChanged;

    Task<UserPreferences> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(UserPreferences preferences, CancellationToken ct = default);
    Task ResetCloseActionAsync(CancellationToken ct = default);
    Task<bool> SetDefaultCountryAsync(string code, CancellationToken ct = default);
    Task<bool> SetAutoRefreshEnabledAsync(bool enabled, CancellationToken ct = default);
    Task<bool> SetAutoRefreshIntervalMinutesAsync(int minutes, CancellationToken ct = default);
}