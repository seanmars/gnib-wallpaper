using CommunityToolkit.Mvvm.ComponentModel;
using WallpaperApp.Models;

namespace WallpaperApp.ViewModels;

public sealed partial class CloseConfirmViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _remember;

    public CloseAction? Result { get; set; }
}
