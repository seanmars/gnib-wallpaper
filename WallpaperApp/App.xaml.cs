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

    public bool IsExiting { get; private set; }

    public IUserPreferencesService Preferences { get; private set; } = default!;

    public IWindowCloseService WindowClose { get; private set; } = default!;

    public static App CurrentApp => (App)Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Preferences = new UserPreferencesService();
        WindowClose = new WindowCloseService(Preferences, RequestShutdown);
        _trayViewModel = new TrayIconViewModel(WindowClose, Preferences);
        _trayIcon = CreateTrayIcon(_trayViewModel);

        var mainWindow = new MainWindow
        {
            DataContext = new MainViewModel(new BingFetcher(), new WallpaperCache(), new FlagCache(), Preferences),
        };
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        base.OnExit(e);
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
