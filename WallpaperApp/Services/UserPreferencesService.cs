using System.Diagnostics;
using System.IO;
using System.Text.Json;
using WallpaperApp.Models;

namespace WallpaperApp.Services;

public sealed class UserPreferencesService : IUserPreferencesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public string GetPreferencesPath()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "WallpaperApp", "preferences.json");
    }

    public async Task<UserPreferences> LoadAsync(CancellationToken ct = default)
    {
        var path = GetPreferencesPath();
        if (!File.Exists(path)) return new UserPreferences();

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var prefs = JsonSerializer.Deserialize<UserPreferences>(json, JsonOptions);
            return prefs ?? new UserPreferences();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[UserPreferencesService] LoadAsync failed: {ex.Message}");
            return new UserPreferences();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(UserPreferences preferences, CancellationToken ct = default)
    {
        await TrySaveAsync(preferences, ct).ConfigureAwait(false);
    }

    private async Task<bool> TrySaveAsync(UserPreferences preferences, CancellationToken ct)
    {
        var path = GetPreferencesPath();
        var dir = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Preferences path has no directory.");

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(preferences, JsonOptions);

            var tempPath = path + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, path);
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[UserPreferencesService] TrySaveAsync failed: {ex.Message}");
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ResetCloseActionAsync(CancellationToken ct = default)
    {
        var prefs = await LoadAsync(ct).ConfigureAwait(false);
        prefs.CloseAction = null;
        await SaveAsync(prefs, ct).ConfigureAwait(false);
    }

    public async Task<bool> SetDefaultCountryAsync(string code, CancellationToken ct = default)
    {
        var prefs = await LoadAsync(ct).ConfigureAwait(false);
        prefs.DefaultCountryCode = string.IsNullOrWhiteSpace(code) ? null : code.ToLowerInvariant();
        return await TrySaveAsync(prefs, ct).ConfigureAwait(false);
    }
}
