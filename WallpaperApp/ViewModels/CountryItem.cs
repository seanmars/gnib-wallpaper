using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using WallpaperApp.Models;

namespace WallpaperApp.ViewModels;

public sealed partial class CountryItem : ObservableObject
{
    public Country Country { get; }
    public string Code => Country.Code;
    public string Name => Country.Name;

    [ObservableProperty]
    private BitmapImage? _flagImage;

    [ObservableProperty]
    private bool _isSelected;

    public CountryItem(Country country)
    {
        Country = country;
    }
}
