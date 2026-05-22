using WallpaperApp.Models;

namespace WallpaperApp.Services;

public interface IUserPreferencesService
{
    Task<UserPreferences> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(UserPreferences preferences, CancellationToken ct = default);
    Task ResetCloseActionAsync(CancellationToken ct = default);
}
