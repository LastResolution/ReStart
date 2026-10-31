using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ReStart.App.Services;

/// <summary>Applies temporary window effects only to revalidated Explorer taskbars; never writes auto-hide configuration or injects Explorer code.</summary>
internal sealed class TaskbarNativeBackend(Func<IEnumerable<TaskbarWindowState>>? observe = null) : ITaskbarLeaseBackend
{
    internal TaskbarBounds Scope { get; set; }
    internal nint LauncherWindow { get; set; }
    /// <summary>Reports whether a top-level window precedes another in the current bounded desktop stacking order.</summary>
    /// <param name="window">Existing top-level window expected to be in front.</param>
    /// <param name="other">Existing top-level window expected to be behind it.</param>
    /// <returns>True only when both distinct handles are found and window is encountered first; false for missing or uncertain ordering.</returns>
    internal static bool IsAbove(nint window, nint other)
    {
        if (window == 0 || other == 0 || window == other) return false;
        var first = 0;
        var current = GetTopWindow(0);
        for (var count = 0; current != 0 && count < 4096; count++, current = GetWindow(current, 2))
        {
            if (current == window) { if (first == 2) return false; first = 1; }
            if (current == other) { if (first == 1) return true; first = 2; }
        }
        return false;
    }
    private static readonly int ParentId = Environment.ProcessId;
    private static readonly long ParentStarted = CurrentStarted();
    private static long CurrentStarted() { using var process = Process.GetCurrentProcess(); return process.StartTime.ToUniversalTime().Ticks; }

    public TaskbarLeaseState? Capture(TaskbarLeaseMode mode)
    {
        var windows = new List<TaskbarWindowState>();
        foreach (var item in (observe ?? ObserveWindows)())
        {
            if (item.Monitor != Scope) continue;
            windows.Add(item);
        }
        return windows.Count == 0 ? null : new(1, Guid.NewGuid().ToString("N"), ParentId, ParentStarted, mode, windows.ToArray());
    }

    private static IEnumerable<TaskbarWindowState> ObserveWindows()
    {
        var monitors = MonitorBounds();
        if (monitors.Length == 0) yield break;
        foreach (var window in Taskbars())
        {
            if (!Identity(window, out var pid, out var started) || !Bounds(window, out var original) || !Monitor(window, out var monitor)) continue;
            // An auto-hidden rectangle can sit mostly on a neighboring display.
            // Its appbar registration identifies the owner more reliably than
            // MonitorFromWindow's geometric overlap in that state.
            var resolved = ResolveMonitor(monitor, monitors, bounds => AutoHideEdge(window, bounds));
            monitor = resolved.Monitor; var edge = resolved.Edge;
            var target = edge == null ? original : VisibleBounds(monitor, original, edge.Value);
            yield return new(window, pid, started, IsWindowVisible(window), edge != null, monitor, original, target, IsTopmost(window));
        }
    }

    /// <summary>Prefers appbar ownership over a hidden window's overlap with a neighboring display.</summary>
    /// <param name="geometric">Monitor selected by the current window rectangle, used only for an unregistered fixed taskbar.</param>
    /// <param name="monitors">Current bounded physical monitor inventory.</param>
    /// <param name="registeredEdge">Read-only monitor registration lookup returning the edge, or null when this taskbar is not registered there.</param>
    /// <returns>The registered owner and edge, or the geometric monitor and null for a fixed taskbar.</returns>
    internal static (TaskbarBounds Monitor, int? Edge) ResolveMonitor(TaskbarBounds geometric, IEnumerable<TaskbarBounds> monitors, Func<TaskbarBounds, int?> registeredEdge)
    {
        foreach (var monitor in monitors)
            if (registeredEdge(monitor) is { } edge) return (monitor, edge);
        return (geometric, null);
    }

    private static TaskbarBounds[] MonitorBounds()
    {
        var result = new List<TaskbarBounds>();
        bool Add(nint monitor, nint hdc, ref Rect rect, nint data)
        {
            if (rect.Right > rect.Left && rect.Bottom > rect.Top) result.Add(new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top));
            return result.Count < 32;
        }
        return EnumDisplayMonitors(0, 0, Add, 0) ? result.ToArray() : [];
    }

    public bool IsCurrent(TaskbarLeaseState state)
    {
        var current = Capture(state.Mode);
        return current != null && current.Windows.Length == state.Windows.Length
            && state.Windows.All(saved => current.Windows.Any(observed => observed.Window == saved.Window && observed.ProcessId == saved.ProcessId
                && observed.ProcessStarted == saved.ProcessStarted && observed.Monitor == saved.Monitor
                && (state.Mode != TaskbarLeaseMode.Reveal || observed.Target == saved.Target)));
    }

    public void Apply(TaskbarLeaseState state)
    {
        foreach (var item in state.Windows)
        {
            if (!Matches(item)) throw new IOException("Taskbar identity changed.");
            var window = (nint)item.Window;
            if (state.Mode == TaskbarLeaseMode.Hide)
            {
                if (IsWindowVisible(window)) ShowWindow(window, 0);
                if (IsWindowVisible(window)) throw new IOException("Taskbar visibility change failed.");
            }
            else
            {
                if ((AutoHideEdge(window, item.Monitor) != null) != item.AutoHide) throw new IOException("Taskbar preference changed.");
                if (LauncherWindow == 0) throw new IOException("Taskbar reveal requires a launcher window.");
                if (!Bounds(window, out var current) || current != item.Target || !IsWindowVisible(window) || !IsAbove(window, LauncherWindow))
                    Position(window, item.Target, show: true, topmost: true);
                if (!Bounds(window, out var applied) || applied != item.Target) throw new IOException("Taskbar reveal geometry was not applied.");
                if (!IsWindowVisible(window)) throw new IOException("Taskbar reveal visibility was not applied.");
                if (!IsAbove(window, LauncherWindow)) throw new IOException("Taskbar reveal stacking was not applied.");
            }
        }
    }

    public void Restore(TaskbarLeaseState state)
    {
        foreach (var item in state.Windows)
        {
            if (!Matches(item)) continue; // Dead Explorer / reused HWND is not owned.
            var window = (nint)item.Window;
            if (state.Mode == TaskbarLeaseMode.Hide)
            {
                if (item.Visible && !IsWindowVisible(window)) ShowWindow(window, 8);
                if (item.Visible && !IsWindowVisible(window)) throw new IOException("Taskbar visibility recovery failed.");
            }
            else if ((AutoHideEdge(window, item.Monitor) != null) == item.AutoHide && Monitor(window, out var monitor) && monitor == item.Monitor
                && Bounds(window, out var current) && current == item.Target)
            {
                // A changed Windows preference/monitor/rectangle belongs to the
                // user or Explorer. Restore only the exact geometry we installed.
                Position(window, item.Original, show: item.Visible, topmost: RestoreTopmost(item.Topmost, IsTopmost(window)));
                if (!item.Visible && IsWindowVisible(window)) ShowWindow(window, 0);
            }
        }
    }

    /// <summary>Preserves the existing launcher area when revealing a taskbar; hiding expands only its own reserved edge.</summary>
    internal static System.Drawing.Rectangle AvailableArea(System.Drawing.Rectangle working, TaskbarLeaseState? state, TaskbarBounds monitor)
    {
        if (state == null || state.Mode == TaskbarLeaseMode.Reveal) return working;
        var item = state.Windows.FirstOrDefault(window => window.Monitor == monitor);
        if (item == null) return working;
        var bar = item.Target;
        if (state.Mode == TaskbarLeaseMode.Hide)
        {
            // Expand only across our taskbar's own strip. An unrelated appbar
            // still reserves its area and must not be covered by this feature.
            var expanded = working;
            if (bar.Width >= monitor.Width / 2)
            {
                if (bar.Y <= monitor.Y && working.Top <= bar.Y + bar.Height)
                { expanded.Height += expanded.Y - monitor.Y; expanded.Y = monitor.Y; }
                else if (bar.Y > monitor.Y && working.Bottom >= bar.Y) expanded.Height = monitor.Y + monitor.Height - expanded.Y;
            }
            else if (bar.X <= monitor.X && working.Left <= bar.X + bar.Width)
            { expanded.Width += expanded.X - monitor.X; expanded.X = monitor.X; }
            else if (bar.X > monitor.X && working.Right >= bar.X) expanded.Width = monitor.X + monitor.Width - expanded.X;
            return expanded;
        }
        return working;
    }

    /// <summary>Reverts only an owned promotion from the ordinary window tier; old records and external demotion retain their current tier.</summary>
    /// <param name="original">Captured tier, or null for a recovery record created before tier capture was supported.</param>
    /// <param name="current">Current topmost tier after identity and geometry validation.</param>
    /// <returns>False to undo an owned promotion, or null to leave the current tier unchanged.</returns>
    internal static bool? RestoreTopmost(bool? original, bool current) => original == false && current ? false : null;

    private static bool IsTopmost(nint window) => (GetWindowLongPtr(window, -20).ToInt64() & 8) != 0;

    private static IEnumerable<nint> Taskbars()
    {
        var primary = FindWindow("Shell_TrayWnd", null);
        if (primary != 0) yield return primary;
        var previous = (nint)0;
        for (var count = 0; count < 32; count++)
        {
            var next = FindWindowEx(0, previous, "Shell_SecondaryTrayWnd", null);
            if (next == 0) yield break;
            yield return next; previous = next;
        }
    }
    private static bool Identity(nint window, out int pid, out long started)
    {
        pid = 0; started = 0;
        var name = new StringBuilder(64); GetClassName(window, name, name.Capacity);
        if (name.ToString() is not ("Shell_TrayWnd" or "Shell_SecondaryTrayWnd")) return false;
        GetWindowThreadProcessId(window, out var id);
        try
        {
            using var process = Process.GetProcessById((int)id);
            using var self = Process.GetCurrentProcess();
            if (!process.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) || process.SessionId != self.SessionId) return false;
            pid = (int)id; started = process.StartTime.ToUniversalTime().Ticks;
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }
    private static bool Matches(TaskbarWindowState state) => Identity((nint)state.Window, out var pid, out var started)
        && pid == state.ProcessId && started == state.ProcessStarted;
    private static bool Bounds(nint window, out TaskbarBounds bounds)
    {
        var success = GetWindowRect(window, out var rect);
        bounds = new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return success && bounds.Width > 0 && bounds.Height > 0;
    }
    private static bool Monitor(nint window, out TaskbarBounds bounds)
    {
        var data = new MonitorData { Size = (uint)Marshal.SizeOf<MonitorData>() };
        var success = GetMonitorInfo(MonitorFromWindow(window, 2), ref data);
        var rect = data.Monitor; bounds = new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return success;
    }
    private static int? AutoHideEdge(nint window, TaskbarBounds monitor)
    {
        for (uint edge = 0; edge < 4; edge++)
        {
            var query = new AppBarData
            {
                Size = (uint)Marshal.SizeOf<AppBarData>(),
                Edge = edge,
                Rect = new() { Left = monitor.X, Top = monitor.Y, Right = monitor.X + monitor.Width, Bottom = monitor.Y + monitor.Height }
            };
            if ((nint)SHAppBarMessage(11, ref query) == window) return (int)edge;
        }
        return null;
    }
    private static TaskbarBounds VisibleBounds(TaskbarBounds monitor, TaskbarBounds bar, int edge) => edge switch
    {
        0 => new(monitor.X, monitor.Y, bar.Width, monitor.Height),
        1 => new(monitor.X, monitor.Y, monitor.Width, bar.Height),
        2 => new(monitor.X + monitor.Width - bar.Width, monitor.Y, bar.Width, monitor.Height),
        _ => new(monitor.X, monitor.Y + monitor.Height - bar.Height, monitor.Width, bar.Height)
    };
    /// <summary>Applies a lease rectangle without activation or position coercion; callers must first validate Explorer identity or own the fixture window.</summary>
    /// <param name="window">Revalidated taskbar HWND or a native verification window owned by this process.</param>
    /// <param name="bounds">Exact physical screen rectangle, also used by conditional recovery.</param>
    /// <param name="show">Whether to show without activating; false retains current visibility.</param>
    /// <param name="topmost">True raises above the launcher, false returns to the ordinary tier; null retains stacking order.</param>
    internal static void Position(nint window, TaskbarBounds bounds, bool show, bool? topmost = null)
    {
        // The temporary lease supplies both target and recovery rectangles.
        // Suppress WINDOWPOSCHANGING so Explorer cannot coerce the requested
        // visible rectangle back to its auto-hidden edge during this call.
        var after = topmost.HasValue ? new nint(topmost.Value ? -1 : -2) : 0;
        var flags = (uint)(0x0410 | (topmost.HasValue ? 0 : 0x0004) | (show ? 0x0040 : 0));
        if (!SetWindowPos(window, after, bounds.X, bounds.Y, bounds.Width, bounds.Height, flags))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorData { public uint Size; public Rect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct AppBarData { public uint Size; public nint Window; public uint Callback, Edge; public Rect Rect; public nint Parameter; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string? className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string? className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder name, int length);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    private delegate bool MonitorCallback(nint monitor, nint hdc, ref Rect rect, nint data);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorCallback callback, nint data);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern nint GetTopWindow(nint parent);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorData data);
    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("shell32.dll")] private static extern nuint SHAppBarMessage(uint message, ref AppBarData data);
}
