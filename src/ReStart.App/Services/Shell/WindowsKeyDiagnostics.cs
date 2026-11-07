using System.Diagnostics;
using System.Text;
using System.Windows.Threading;

namespace ReStart.App.Services;

/// <summary>Provides no-op-by-default timing checkpoints for an explicitly requested diagnostic session.</summary>
internal static class WindowsKeyDiagnostics
{
    internal static WindowsKeyTrace? Current { get; set; }
    internal static bool Active => Current?.Active == true;
    internal static void Mark(KeyTraceStage stage, KeyTraceDetail detail = KeyTraceDetail.None, int delayMs = 0) => Current?.Record(stage, detail, delayMs);
    internal static void Flag(KeyTraceStage stage, bool value) => Mark(stage, value ? KeyTraceDetail.Yes : KeyTraceDetail.No);

    internal static void Foreground(KeyTraceStage stage, nint own)
    {
        if (!Active) return;
        var window = Native.GetForegroundWindow();
        var detail = KeyTraceDetail.OtherWindow;
        if (window == 0) detail = KeyTraceDetail.NoWindow;
        else if (window == own) detail = KeyTraceDetail.Owned;
        else
        {
            var name = new StringBuilder(128);
            Native.GetClassName(window, name, name.Capacity);
            detail = name.ToString() switch
            {
                "Windows.UI.Core.CoreWindow" => KeyTraceDetail.CoreWindow,
                "Windows.UI.Composition.DesktopWindowContentBridge" or "XamlExplorerHostIslandWindow" => KeyTraceDetail.XamlWindow,
                "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" => KeyTraceDetail.Taskbar,
                _ => KeyTraceDetail.OtherWindow
            };
        }
        Mark(stage, detail);
    }

    internal static Timer Heartbeats(Dispatcher dispatcher, Action<uint> postHookHeartbeat)
    {
        return new Timer(_ =>
        {
            if (!Active) return;
            var tick = unchecked((uint)Environment.TickCount);
            postHookHeartbeat(tick);
            var queued = Stopwatch.GetTimestamp();
            dispatcher.BeginInvoke(() => Mark(KeyTraceStage.UiHeartbeat, delayMs: (int)Stopwatch.GetElapsedTime(queued).TotalMilliseconds), DispatcherPriority.Background);
        }, null, 1000, 1000);
    }
}
