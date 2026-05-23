namespace WallpaperApp.Services;

public enum WallpaperSetterErrorKind
{
    FileNotFound,
    UnsupportedFormat,
    Win32CallFailed,
    RegistryWriteFailed,
}
