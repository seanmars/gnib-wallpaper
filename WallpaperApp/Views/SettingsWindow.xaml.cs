using System.Windows;
using System.Windows.Input;
using WallpaperApp.ViewModels;

namespace WallpaperApp.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += OnLoadedAsync;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedAsync;
        if (DataContext is SettingsViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
