using System.Diagnostics;
using System.IO;
using System.Text.Json;

using WallpaperApp.Models;

namespace WallpaperApp.Services;

/// <summary>
/// Reads and writes <see cref="DesktopState"/> to its own JSON file
/// (<c>desktop-state.json</c>), independent of user preferences. Read failures
/// (missing, corrupt, or unparseable) degrade to an empty state without throwing,
/// so a damaged file simply triggers one re-apply on the next reconcile.
/// </summary>
public sealed class DesktopStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public string GetStatePath()
    {
        return HomeDir.GetPath("desktop-state.json");
    }

    public async Task<DesktopState> LoadAsync(CancellationToken ct = default)
    {
        var path = GetStatePath();
        if (!File.Exists(path)) return new DesktopState();

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<DesktopState>(json, JsonOptions) ?? new DesktopState();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[DesktopStateStore] LoadAsync failed: {ex.Message}");
            return new DesktopState();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(DesktopState state, CancellationToken ct = default)
    {
        var path = GetStatePath();
        var dir = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Desktop state path has no directory.");

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(state, JsonOptions);

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
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[DesktopStateStore] SaveAsync failed: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }
}
