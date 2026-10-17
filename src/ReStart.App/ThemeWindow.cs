using System.Windows;
using System.Windows.Interop;
using ReStart.App.Services;

namespace ReStart.App;

internal static class ThemeWindow
{
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
