using System.Windows;
using System.Windows.Input;
using WallpaperApp.Models;
using WallpaperApp.ViewModels;

namespace WallpaperApp.Views;

public partial class CloseConfirmDialog : Window
{
    public CloseConfirmDialog()
    {
        InitializeComponent();
        DataContext = new CloseConfirmViewModel();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public CloseConfirmViewModel ViewModel => (CloseConfirmViewModel)DataContext;

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ViewModel.Result = null;
            Close();
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Result = CloseAction.MinimizeToTray;
        Close();
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Result = CloseAction.Exit;
        Close();
    }
}
