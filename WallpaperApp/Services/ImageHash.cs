using System.Security.Cryptography;

namespace WallpaperApp.Services;

/// <summary>
/// Computes a stable content hash for image bytes, used to decide whether the
/// desktop wallpaper actually changed since it was last applied.
/// </summary>
public static class ImageHash
{
    public static string Compute(byte[] imageBytes)
    {
        var hash = SHA256.HashData(imageBytes);
        return Convert.ToHexStringLower(hash);
    }
}
