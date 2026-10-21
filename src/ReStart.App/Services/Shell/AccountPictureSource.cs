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
            foreach (var size in new[] { 96, 64, 48, 32, 192 })
                if (key?.GetValue("Image" + size) is string path && Read(path) is { } image) return image;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
        return null;
    }

    private static BitmapSource? Read(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 4 * 1024 * 1024) return null;
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache; image.DecodePixelWidth = 96;
            image.UriSource = new Uri(path, UriKind.Absolute); image.EndInit(); image.Freeze(); return image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FileFormatException) { return null; }
    }
}
