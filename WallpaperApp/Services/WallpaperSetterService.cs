using System.IO;
using System.Runtime.InteropServices;
using System.Security;

using Microsoft.Win32;

namespace WallpaperApp.Services;

public sealed class WallpaperSetterService : IWallpaperSetterService
{
    private const uint SPI_SETDESKWALLPAPER = 0x0014;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;

    private const string DesktopRegistryKeyPath = @"Control Panel\Desktop";
    private const string WallpaperStyleFill = "10";
    private const string TileWallpaperOff = "0";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp",
        ".jpg",
        ".jpeg",
        ".png",
    };

    public void SetWallpaperFromFile(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path must not be null or whitespace.", nameof(imagePath));
        }

        var extension = Path.GetExtension(imagePath);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new WallpaperSetterException(
                WallpaperSetterErrorKind.UnsupportedFormat,
                $"Unsupported wallpaper format '{extension}'. Allowed: .bmp, .jpg, .jpeg, .png.");
        }

        var fullPath = Path.GetFullPath(imagePath);
        if (!File.Exists(fullPath))
        {
            throw new WallpaperSetterException(
                WallpaperSetterErrorKind.FileNotFound,
                $"Wallpaper file not found: {fullPath}");
        }

        WriteWallpaperStyleRegistry();
        ApplyWallpaperViaWin32(fullPath);
    }

    private static void WriteWallpaperStyleRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(DesktopRegistryKeyPath, writable: true);
            if (key is null)
            {
                throw new WallpaperSetterException(
                    WallpaperSetterErrorKind.RegistryWriteFailed,
                    $@"Unable to open HKCU\{DesktopRegistryKeyPath} for writing.");
            }

            key.SetValue("WallpaperStyle", WallpaperStyleFill, RegistryValueKind.String);
            key.SetValue("TileWallpaper", TileWallpaperOff, RegistryValueKind.String);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            throw new WallpaperSetterException(
                WallpaperSetterErrorKind.RegistryWriteFailed,
                $@"Failed to write wallpaper style to HKCU\{DesktopRegistryKeyPath}: {ex.Message}",
                ex);
        }
    }

    private static void ApplyWallpaperViaWin32(string fullPath)
    {
        Marshal.SetLastSystemError(0);
        var ok = SystemParametersInfoW(
            SPI_SETDESKWALLPAPER,
            0,
            fullPath,
            SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

        if (!ok)
        {
            var err = Marshal.GetLastWin32Error();
            throw new WallpaperSetterException(
                WallpaperSetterErrorKind.Win32CallFailed,
                $"SystemParametersInfo failed (Win32 error {err}).");
        }
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoW(
        uint uiAction,
        uint uiParam,
        [MarshalAs(UnmanagedType.LPWStr)] string pvParam,
        uint fWinIni);
}
