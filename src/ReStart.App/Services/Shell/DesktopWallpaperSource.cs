using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ReStart.App.Services;

/// <summary>Reads the current monitor's desktop image and decodes it without retaining file locks or URI-cache entries.</summary>
internal static class DesktopWallpaperSource
{
    internal static string? CurrentPath(System.Drawing.Rectangle monitor)
    {
        object? instance = null;
        try
        {
            instance = Activator.CreateInstance(Type.GetTypeFromCLSID(new("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"))!);
            var desktop = (IDesktopWallpaper)instance!;
            if (desktop.GetMonitorDevicePathCount(out var count) >= 0)
                for (uint i = 0; i < Math.Min(count, 64); i++)
                    if (desktop.GetMonitorDevicePathAt(i, out var id) >= 0 && desktop.GetMonitorRECT(id, out var rect) >= 0
                        && rect.Left == monitor.Left && rect.Top == monitor.Top && rect.Right == monitor.Right && rect.Bottom == monitor.Bottom
                        && desktop.GetWallpaper(id, out var path) >= 0) return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException) { }
        finally { if (instance != null && Marshal.IsComObject(instance)) Marshal.FinalReleaseComObject(instance); }
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        return key?.GetValue("WallPaper") as string;
    }

    internal static BitmapImage Load(string path)
    {
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        image.UriSource = new Uri(path, UriKind.Absolute); image.EndInit(); image.Freeze();
        return image;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorRect { internal int Left, Top, Right, Bottom; }
    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        [PreserveSig] int SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor, [MarshalAs(UnmanagedType.LPWStr)] string path);
        [PreserveSig] int GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor, [MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int GetMonitorDevicePathAt(uint index, [MarshalAs(UnmanagedType.LPWStr)] out string monitor);
        [PreserveSig] int GetMonitorDevicePathCount(out uint count);
        [PreserveSig] int GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitor, out MonitorRect rect);
    }
}
