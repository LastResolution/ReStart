using System.Windows.Interop;
using ReStart.App.Services;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Resolves this launcher's physical available area after a scoped temporary taskbar effect; null follows Windows.</summary>
    internal Func<System.Windows.Forms.Screen, System.Drawing.Rectangle>? NativeWorkingArea { get; set; }

    /// <summary>Refits a presented native launcher without activating it or changing other monitors.</summary>
    internal void RefitNativeBounds()
    {
        if (_preview || !IsPresenting) return;
        var handle = new WindowInteropHelper(this).Handle;
        var screen = System.Windows.Forms.Screen.FromHandle(handle);
        var area = NativeWorkingArea?.Invoke(screen) ?? screen.WorkingArea;
        MonitorPlacement.FitWorkingArea(handle, 0, area, 0x0014, NativeWorkingArea);
    }
}
