using System.IO;

namespace WallpaperApp.Services;

public static class HomeDir
{
    private const string DefaultFolderName = ".gnib-wallpaper";

    private static string? _overrideRoot;

    public static string Root
    {
        get
        {
            if (!string.IsNullOrEmpty(_overrideRoot))
            {
                return _overrideRoot;
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, DefaultFolderName);
        }
    }

    public static void SetRoot(string? path)
    {
        _overrideRoot = string.IsNullOrWhiteSpace(path) ? null : path;
    }

    public static string GetPath(params string[] segments)
    {
        if (segments.Length == 0)
        {
            return Root;
        }

        var combined = new string[segments.Length + 1];
        combined[0] = Root;
        Array.Copy(segments, 0, combined, 1, segments.Length);
        return Path.Combine(combined);
    }
}