namespace WallpaperApp.Services;

public enum CloseRequestReason
{
    UserCloseButton,
    Minimize,
}

public interface IWindowCloseService
{
    Task HandleCloseRequestAsync(CloseRequestReason reason);
    void MinimizeToTray();
    void RestoreWindow();
    void ToggleWindow();
    void ExitApplication();
}
