using System.Windows;
using WallpaperApp.ViewModels;

namespace WallpaperApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedAsync;
        if (DataContext is MainViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
