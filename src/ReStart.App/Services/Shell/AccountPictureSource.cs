using System.Security.Principal;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ReStart.App.Services;

/// <summary>Reads the current Windows account's locally registered picture without writing account data or contacting a service.</summary>
internal static class AccountPictureSource
{
    internal static BitmapSource? Current()
    {
        try
        {
            var sid = WindowsIdentity.GetCurrent().User?.Value;
            using var key = sid == null ? null : Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\" + sid);
            if (key == null) return null;
            return FromCandidates(key.GetValueNames().Where(name => name.StartsWith("Image", StringComparison.Ordinal)
                && int.TryParse(name.AsSpan(5), out _)).Select(name => (Size: int.Parse(name.AsSpan(5)), Path: key.GetValue(name) as string ?? "")));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
        return null;
    }

    /// <summary>Chooses the largest usable registered image, falling back when a file is missing or invalid.</summary>
    /// <param name="candidates">Declared pixel sizes and local paths; original files are read without retaining handles or metadata paths.</param>
    /// <returns>A frozen image decoded to at most 384 pixels without upscaling, or null when no candidate can be read.</returns>
    internal static BitmapSource? FromCandidates(IEnumerable<(int Size, string Path)> candidates)
    {
        foreach (var candidate in candidates.Where(value => value.Size > 0).OrderByDescending(value => value.Size))
            if (Read(candidate.Path) is { } image) return image;
        return null;
    }

    private static BitmapSource? Read(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 4 * 1024 * 1024) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var header = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
            if (header.PixelWidth is < 1 or > 8192 || header.PixelHeight is < 1 or > 8192) return null;
            var width = Math.Min(header.PixelWidth, 384); stream.Position = 0;
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = width;
            image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FileFormatException) { return null; }
    }
}
