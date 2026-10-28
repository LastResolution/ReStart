using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ReStart.App.Tablet.Core;

/// <summary>
/// Hides the Windows taskbar in tablet mode: auto-hide (so the work area is the whole
/// screen, minus any other AppBar such as the home bar) plus hidden tray windows (so it
/// never slides in). The user's own auto-hide state is kept in a marker file until it is
/// restored, which makes every exit path recoverable: normal exit, laptop mode, a crash
/// (next start restores), and a kill (a watchdog process restores).
/// </summary>
internal sealed class TaskbarControl : IDisposable
{
    private const uint AbmGetState = 4, AbmSetState = 10;
    private const int AbsAutoHide = 1;
    private const int SwHide = 0, SwShowNa = 8;
    internal const string WatchdogArgument = "--tablet-watchdog", RestoreArgument = "--tablet-restore-taskbar";
    internal static readonly string MarkerPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReStart", "tablet-taskbar.json");

    /// <summary>Saved taskbar state. <paramref name="State"/> is the user's ABM state; <paramref name="Pid"/> the process that hid it.</summary>
    private sealed record Marker(int State, int Pid);

    private readonly Dispatcher _dispatcher;
    private readonly HwndSource _listener;
    private readonly uint _taskbarCreated;
    private readonly DispatcherTimer _guard;
    private Process? _watchdog;
    private bool _disposed;

    /// <summary>Whether tablet mode currently keeps the taskbar hidden.</summary>
    internal bool IsHidden { get; private set; }

    internal TaskbarControl(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        // TaskbarCreated (explorer restart) is broadcast to top-level windows only.
        _listener = new HwndSource(new HwndSourceParameters("ReStartTabletTaskbar") { WindowStyle = 0, Width = 0, Height = 0 });
        _listener.AddHook(Hook);
        // Explorer re-shows its tray on some shell events; the WinEvent hook catches most, this catches the rest.
        _guard = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, (_, _) => { if (IsHidden) Enforce(); }, dispatcher);
    }

    /// <summary>Hides the taskbar, saving the user's state first (or keeping the one saved by a previous run).</summary>
    internal void Hide()
    {
        if (_disposed) return;
        if (!IsHidden)
        {
            var original = ReadMarker()?.State ?? GetState();
            WriteMarker(new(original, Environment.ProcessId));
            IsHidden = true;
            EnsureWatchdog();
            _guard.Start();
            TabletLog.Write("taskbar", $"hide (saved state {original})");
        }
        Enforce();
    }

    /// <summary>Restores the user's taskbar state and shows the tray again.</summary>
    internal void Restore()
    {
        if (!IsHidden) return;
        IsHidden = false;
        _guard.Stop();
        RestoreFromMarker("restore");
    }

    /// <summary>Re-hides tray windows that became visible (WinEvent from the window manager).</summary>
    internal void OnTrayShown(nint hwnd)
    {
        if (IsHidden && IsTrayWindow(hwnd)) _dispatcher.BeginInvoke(Enforce, DispatcherPriority.Send);
    }

    /// <summary>Whether <paramref name="hwnd"/> is a taskbar window (primary or secondary monitor).</summary>
    internal static bool IsTrayWindow(nint hwnd) => AppWindows.ClassName(hwnd) is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";

    private void Enforce()
    {
        if (!IsHidden) return;
        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray == 0) return;
        if ((GetState() & AbsAutoHide) == 0) SetState(tray, AbsAutoHide);
        foreach (var window in TrayWindows()) if (IsWindowVisible(window)) ShowWindow(window, SwHide);
    }

    /// <summary>
    /// Restores the state saved in the marker and deletes it. Used on restore, at startup after
    /// a crash, by the watchdog and by <c>--tablet-restore-taskbar</c>. Returns whether a marker existed.
    /// </summary>
    internal static bool RestoreFromMarker(string reason)
    {
        if (ReadMarker() is not { } marker) return false;
        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray != 0)
        {
            SetState(tray, marker.State);
            foreach (var window in TrayWindows()) ShowWindow(window, SwShowNa);
        }
        try { File.Delete(MarkerPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        TabletLog.Write("taskbar", $"restored state {marker.State} ({reason}{(tray == 0 ? ", no taskbar" : "")})");
        return true;
    }

    /// <summary>At startup: a marker left by a dead process means the taskbar is still hidden. Restores it unless tablet mode keeps it hidden.</summary>
    internal static void RecoverAtStartup(bool tablet)
    {
        if (ReadMarker() is not { } marker || marker.Pid == Environment.ProcessId) return;
        if (tablet) { TabletLog.Write("taskbar", $"marker from pid {marker.Pid} kept (tablet)"); return; }
        RestoreFromMarker("stale marker");
    }

    /// <summary>
    /// Watchdog mode (<c>ReStart.exe --tablet-watchdog pid</c>): waits for the shell process and restores
    /// the taskbar if it died while hiding it (e.g. killed from Task Manager). Exits silently otherwise.
    /// </summary>
    internal static async Task RunWatchdogAsync(int parentPid)
    {
        try { using var self = Process.GetCurrentProcess(); self.PriorityClass = ProcessPriorityClass.Idle; }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
        try
        {
            using var parent = Process.GetProcessById(parentPid);
            await parent.WaitForExitAsync();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception) { }
        // A newer instance rewrites the marker with its own pid; only clean up after our parent.
        if (ReadMarker() is { } marker && marker.Pid == parentPid) RestoreFromMarker($"watchdog, pid {parentPid} died");
    }

    private void EnsureWatchdog()
    {
        if (_watchdog is { HasExited: false } || Environment.ProcessPath is not { } exe) return;
        try
        {
            var info = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true };
            info.ArgumentList.Add(WatchdogArgument);
            info.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            _watchdog = Process.Start(info);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { TabletLog.Write("taskbar", "watchdog: " + ex.Message); }
    }

    private static Marker? ReadMarker()
    {
        try { return File.Exists(MarkerPath) ? JsonSerializer.Deserialize<Marker>(File.ReadAllText(MarkerPath)) : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    private static void WriteMarker(Marker marker)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath)!);
            File.WriteAllText(MarkerPath + ".tmp", JsonSerializer.Serialize(marker));
            File.Move(MarkerPath + ".tmp", MarkerPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { TabletLog.Write("taskbar", "marker: " + ex.Message); }
    }

    private static int GetState()
    {
        var data = new AppBarData { Size = Marshal.SizeOf<AppBarData>() };
        return (int)SHAppBarMessage(AbmGetState, ref data);
    }

    private static void SetState(nint tray, int state)
    {
        var data = new AppBarData { Size = Marshal.SizeOf<AppBarData>(), Window = tray, Param = state };
        SHAppBarMessage(AbmSetState, ref data);
    }

    private static List<nint> TrayWindows()
    {
        var result = new List<nint>();
        var primary = FindWindow("Shell_TrayWnd", null);
        if (primary != 0) result.Add(primary);
        for (var w = FindWindowEx(0, 0, "Shell_SecondaryTrayWnd", null); w != 0; w = FindWindowEx(0, w, "Shell_SecondaryTrayWnd", null)) result.Add(w);
        return result;
    }

    private nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == _taskbarCreated && _taskbarCreated != 0 && IsHidden)
        {
            TabletLog.Write("taskbar", "explorer restarted, hiding again");
            _dispatcher.BeginInvoke(Enforce, DispatcherPriority.Background);
            // The new taskbar finishes its own layout after the broadcast.
            var again = new DispatcherTimer(TimeSpan.FromSeconds(1.5), DispatcherPriority.Background, (s, _) => { ((DispatcherTimer)s!).Stop(); Enforce(); }, _dispatcher);
            again.Start();
        }
        return 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Restore();
        _disposed = true;
        _guard.Stop();
        _listener.RemoveHook(Hook);
        _listener.Dispose();
        // The watchdog exits by itself once this process is gone and the marker is deleted.
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public int Size;
        public nint Window;
        public uint CallbackMessage;
        public uint Edge;
        public Rect Bounds;
        public nint Param;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }

    [DllImport("shell32.dll")] private static extern nuint SHAppBarMessage(uint message, ref AppBarData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string? className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string? className, string? title);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
}
