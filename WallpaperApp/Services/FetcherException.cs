namespace WallpaperApp.Services;

public sealed class FetcherException : Exception
{
    public FetcherException(string message) : base(message) { }
    public FetcherException(string message, Exception inner) : base(message, inner) { }
}