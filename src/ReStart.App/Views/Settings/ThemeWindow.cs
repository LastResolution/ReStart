using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using ReStart.App.Services;

namespace ReStart.App;

internal static class ThemeWindow
{
    /// <summary>Gets the Windows shell color mode used by Start and by System-based color families.</summary>
    internal static bool SystemIsLight
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
        }
    }
    public static bool IsLight { get; set; }
    public static void Attach(Window window) => window.SourceInitialized += (_, _) => Apply(window);
    public static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0) return;
        var dark = IsLight || SystemParameters.HighContrast ? 0 : 1;
        Native.DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
    }
}
