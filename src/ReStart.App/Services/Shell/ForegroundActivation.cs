namespace ReStart.App.Services;

/// <summary>Activates the launcher's own window on an explicit open request, separating foreground ownership from WPF keyboard focus.</summary>
internal static class ForegroundActivation
{
    internal static bool Request(nint handle)
    {
        WindowsKeyDiagnostics.Foreground(KeyTraceStage.ForegroundBefore, handle);
        WindowsKeyDiagnostics.Mark(KeyTraceStage.ForegroundSetBegin);
        Native.SetForegroundWindow(handle);
        WindowsKeyDiagnostics.Mark(KeyTraceStage.ForegroundSetEnd);
        var previous = Native.GetForegroundWindow();
        if (previous != handle && previous != 0)
        {
            var currentThread = Native.GetCurrentThreadId();
            var foregroundThread = Native.GetWindowThreadProcessId(previous, out _);
            // Do not attach to a known stalled queue. Never synthesize an Alt/Win/mouse event to obtain focus.
            WindowsKeyDiagnostics.Mark(KeyTraceStage.ForegroundProbeBegin);
            var responsive = foregroundThread != 0 && foregroundThread != currentThread && !Native.IsHungAppWindow(previous)
                && Native.SendMessageTimeout(previous, 0, 0, 0, 2, 50, out _) != 0;
            WindowsKeyDiagnostics.Flag(KeyTraceStage.ForegroundProbeEnd, responsive);
            if (responsive)
            {
                WindowsKeyDiagnostics.Mark(KeyTraceStage.ForegroundAttachBegin);
                var attached = Native.AttachThreadInput(currentThread, foregroundThread, true);
                WindowsKeyDiagnostics.Flag(KeyTraceStage.ForegroundAttachEnd, attached);
                if (attached)
                {
                    try
                    {
                        WindowsKeyDiagnostics.Mark(KeyTraceStage.ForegroundRetryBegin);
                        Native.SetForegroundWindow(handle);
                        WindowsKeyDiagnostics.Mark(KeyTraceStage.ForegroundRetryEnd);
                    }
                    finally
                    {
                        WindowsKeyDiagnostics.Mark(KeyTraceStage.ForegroundDetachBegin);
                        Native.AttachThreadInput(currentThread, foregroundThread, false);
                        WindowsKeyDiagnostics.Mark(KeyTraceStage.ForegroundDetachEnd);
                    }
                }
            }
        }
        WindowsKeyDiagnostics.Foreground(KeyTraceStage.ForegroundAfter, handle);
        if (Native.GetForegroundWindow() != handle) return false;
        WindowsKeyDiagnostics.Mark(KeyTraceStage.ForegroundFocusBegin);
        Native.SetFocus(handle);
        WindowsKeyDiagnostics.Mark(KeyTraceStage.ForegroundFocusEnd);
        return true;
    }
}
