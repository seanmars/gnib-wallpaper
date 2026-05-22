using System.Windows;
using WallpaperApp.Models;
using WallpaperApp.Views;

namespace WallpaperApp.Services;

public sealed class WindowCloseService : IWindowCloseService
{
    private readonly IUserPreferencesService _preferences;
    private readonly Action _onExitRequested;
    private bool _handlingCloseRequest;

    public WindowCloseService(IUserPreferencesService preferences, Action onExitRequested)
    {
        _preferences = preferences;
        _onExitRequested = onExitRequested;
    }

    public async Task HandleCloseRequestAsync(CloseRequestReason reason)
    {
        if (reason == CloseRequestReason.Minimize)
        {
            MinimizeToTray();
            return;
        }

        if (_handlingCloseRequest) return;
        _handlingCloseRequest = true;
        try
        {
            var prefs = await _preferences.LoadAsync().ConfigureAwait(true);
            if (prefs.CloseAction is { } remembered)
            {
                ApplyAction(remembered);
                return;
            }

            var dialog = new CloseConfirmDialog
            {
                Owner = Application.Current.MainWindow,
            };
            dialog.ShowDialog();

            var vm = dialog.ViewModel;
            if (vm.Result is not { } chosen)
            {
                return;
            }

            if (vm.Remember)
            {
                await _preferences.SaveAsync(new UserPreferences { CloseAction = chosen }).ConfigureAwait(true);
            }

            ApplyAction(chosen);
        }
        finally
        {
            _handlingCloseRequest = false;
        }
    }

    public void MinimizeToTray()
    {
        var main = Application.Current.MainWindow;
        if (main is null) return;
        main.Hide();
    }

    public void RestoreWindow()
    {
        var main = Application.Current.MainWindow;
        if (main is null) return;

        if (main.WindowState == WindowState.Minimized)
        {
            main.WindowState = WindowState.Normal;
        }

        if (!main.IsVisible)
        {
            main.Show();
        }

        main.Activate();
        main.Topmost = true;
        main.Topmost = false;
        main.Focus();
    }

    public void ToggleWindow()
    {
        var main = Application.Current.MainWindow;
        if (main is null) return;

        if (main.IsVisible)
        {
            MinimizeToTray();
        }
        else
        {
            RestoreWindow();
        }
    }

    public void ExitApplication()
    {
        _onExitRequested();
    }

    private void ApplyAction(CloseAction action)
    {
        switch (action)
        {
            case CloseAction.MinimizeToTray:
                MinimizeToTray();
                break;
            case CloseAction.Exit:
                ExitApplication();
                break;
        }
    }
}
