namespace ReStart.App.Services;

/// <summary>Activates the launcher's own window on an explicit open request, separating foreground ownership from WPF keyboard focus.</summary>
internal static class ForegroundActivation
{
    internal static bool Request(nint handle)
    {
        Native.SetForegroundWindow(handle);
        var previous = Native.GetForegroundWindow();
        if (previous != handle && previous != 0)
        {
            var currentThread = Native.GetCurrentThreadId();
            var foregroundThread = Native.GetWindowThreadProcessId(previous, out _);
            // Do not attach to a known stalled queue. Never synthesize an Alt/Win/mouse event to obtain focus.
            if (foregroundThread != 0 && foregroundThread != currentThread && !Native.IsHungAppWindow(previous)
                && Native.SendMessageTimeout(previous, 0, 0, 0, 2, 50, out _) != 0
                && Native.AttachThreadInput(currentThread, foregroundThread, true))
            {
                try { Native.SetForegroundWindow(handle); }
                finally { Native.AttachThreadInput(currentThread, foregroundThread, false); }
            }
        }
        if (Native.GetForegroundWindow() != handle) return false;
        Native.SetFocus(handle);
        return true;
    }
}
