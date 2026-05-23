namespace WallpaperApp.Services;

public interface IWallpaperRefreshScheduler : IDisposable
{
    event EventHandler<WallpaperRefreshedEventArgs>? WallpaperRefreshed;

    bool Started { get; }

    void Start();

    void Stop();

    Task RefreshAsync(IReadOnlyList<string>? countryCodes, CancellationToken ct = default);
}
