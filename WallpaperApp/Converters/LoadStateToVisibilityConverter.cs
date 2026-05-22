using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WallpaperApp.Models;

namespace WallpaperApp.Converters;

public sealed class LoadStateToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LoadState state) return Visibility.Collapsed;
        if (parameter is not string expected) return Visibility.Collapsed;
        if (!Enum.TryParse<LoadState>(expected, ignoreCase: true, out var expectedState)) return Visibility.Collapsed;
        return state == expectedState ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
