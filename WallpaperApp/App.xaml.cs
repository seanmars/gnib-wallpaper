using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

using Hardcodet.Wpf.TaskbarNotification;

using WallpaperApp.Services;
using WallpaperApp.ViewModels;

namespace WallpaperApp;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private TrayIconViewModel? _trayViewModel;
    private IWallpaperRefreshScheduler? _refreshScheduler;

    public bool IsExiting { get; private set; }

    public IUserPreferencesService Preferences { get; private set; } = default!;

    public IWindowCloseService WindowClose { get; private set; } = default!;

    public IWallpaperSetterService WallpaperSetter { get; private set; } = default!;

    public static App CurrentApp => (App)Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Preferences = new UserPreferencesService();
        WindowClose = new WindowCloseService(Preferences, RequestShutdown);
        WallpaperSetter = new WallpaperSetterService();
        _trayViewModel = new TrayIconViewModel(WindowClose, Preferences);
        _trayIcon = CreateTrayIcon(_trayViewModel);

        var fetcher = new BingFetcher();
        var cache = new WallpaperCache();
        var desktopState = new DesktopStateStore();
        _refreshScheduler = new WallpaperRefreshScheduler(fetcher, cache, Preferences, WallpaperSetter, desktopState);

        var mainWindow = new MainWindow
        {
            DataContext = new MainViewModel(fetcher, cache, new FlagCache(), Preferences, WallpaperSetter, _refreshScheduler),
        };
        MainWindow = mainWindow;
        mainWindow.Show();

        StartRefreshSchedulerIfEnabled();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _refreshScheduler?.Dispose();
        _refreshScheduler = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        base.OnExit(e);
    }

    private async void StartRefreshSchedulerIfEnabled()
    {
        try
        {
            var prefs = await Preferences.LoadAsync().ConfigureAwait(true);
            if (prefs.AutoRefreshEnabled)
            {
                _refreshScheduler?.Start();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] StartRefreshSchedulerIfEnabled failed: {ex.Message}");
        }
    }

    private void RequestShutdown()
    {
        IsExiting = true;
        Shutdown();
    }

    private static TaskbarIcon CreateTrayIcon(TrayIconViewModel vm)
    {
        var icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute)),
            ToolTipText = "GNIB Wallpaper",
            DataContext = vm,
            DoubleClickCommand = vm.RestoreWindowCommand,
            LeftClickCommand = vm.RestoreWindowCommand,
        };

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "Show / Hide", Command = vm.ToggleWindowCommand });
        menu.Items.Add(new MenuItem { Header = "Reset close preference", Command = vm.ResetPreferenceCommand });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Exit", Command = vm.ExitCommand });
        icon.ContextMenu = menu;

        return icon;
    }
}