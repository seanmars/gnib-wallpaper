using System.ComponentModel;
using System.Windows;

using WallpaperApp.Services;
using WallpaperApp.ViewModels;

namespace WallpaperApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoadedAsync;
        Closing += OnClosing;
        Closed += OnClosed;
        StateChanged += OnStateChanged;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedAsync;
        if (DataContext is MainViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (App.CurrentApp.IsExiting)
        {
            return;
        }

        e.Cancel = true;
        await App.CurrentApp.WindowClose.HandleCloseRequestAsync(CloseRequestReason.UserCloseButton);
    }

    private async void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
        {
            return;
        }

        WindowState = WindowState.Normal;
        await App.CurrentApp.WindowClose.HandleCloseRequestAsync(CloseRequestReason.Minimize);
    }
}