namespace ReStart.App.Services;

/// <summary>Activates the launcher's own window on an explicit open request, separating foreground ownership from WPF keyboard focus.</summary>
internal static class ForegroundActivation
{
    internal static bool Request(nint handle)
    {
        if (handle == 0) return false;
        Native.SetForegroundWindow(handle);
        var previous = Native.GetForegroundWindow();
        if (previous != handle && previous != 0)
        {
            var name = new System.Text.StringBuilder(128);
            Native.GetClassName(previous, name, name.Capacity);
            // Shell-hosted Start/Search queues can stall after a successful
            // liveness probe. Never attach our UI to those queues; the next
            // SetForegroundWindow would no longer have a bounded wait.
            if (CanAttachQueue(name.ToString()))
            {
                var currentThread = Native.GetCurrentThreadId();
                var foregroundThread = Native.GetWindowThreadProcessId(previous, out _);
                if (foregroundThread != 0 && foregroundThread != currentThread && !Native.IsHungAppWindow(previous)
                    && Native.SendMessageTimeout(previous, 0, 0, 0, 2, 50, out _) != 0
                    && Native.AttachThreadInput(currentThread, foregroundThread, true))
                {
                    try { Native.SetForegroundWindow(handle); }
                    finally { Native.AttachThreadInput(currentThread, foregroundThread, false); }
                }
            }
        }
        if (Native.GetForegroundWindow() != handle) return false;
        Native.SetFocus(handle);
        return true;
    }

    internal static bool CanAttachQueue(string className) => !string.IsNullOrEmpty(className)
        && className is not ("Windows.UI.Core.CoreWindow" or "XamlExplorerHostIslandWindow");
}
