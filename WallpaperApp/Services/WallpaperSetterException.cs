namespace WallpaperApp.Services;

public sealed class WallpaperSetterException : Exception
{
    public WallpaperSetterErrorKind Kind { get; }

    public WallpaperSetterException(WallpaperSetterErrorKind kind, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
    }
}