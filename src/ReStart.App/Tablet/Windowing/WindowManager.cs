using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace ReStart.App.Tablet.Windowing;

/// <summary>
/// Tablet-mode window policy. Apps run maximized (when they come to the foreground, and
/// all at once when tablet mode starts), and the Windows desktop is never a dead end:
/// minimizing the active app, closing an app that was opened from Home, or ending up on the
/// desktop/taskbar with nothing on screen, shows Home. Everything is driven by out-of-context WinEvents on the UI thread, debounced
/// and rate-limited so the shell never fights the user or loops.
/// </summary>
internal sealed class WindowManager : IDisposable
{
    private const uint EventSystemForeground = 0x0003, EventSystemMinimizeStart = 0x0016, EventSystemMinimizeEnd = 0x0017;
    private const uint EventObjectDestroy = 0x8001, EventObjectShow = 0x8002, EventObjectHide = 0x8003;
    private const uint SkipOwnProcess = 0x0002;
    private const long WsCaption = 0x00C00000, WsExTopmost = 0x00000008;
    private const int GwlStyle = -16, GwlExStyle = -20, SwShowNoActivate = 4;

    // Shell surfaces that mean "the user is looking at the desktop" when they hold the foreground.
    private static readonly HashSet<string> DesktopClasses = new(StringComparer.Ordinal)
    { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };

    private readonly Dispatcher _dispatcher;
    private readonly Func<nint> _home;
    private readonly WinEventProc _callback;
    private readonly List<nint> _hooks = [];
    private readonly HashSet<nint> _known = [];
    // Windows this policy maximized, restored to their own size when tablet mode ends.
    private readonly HashSet<nint> _maximized = [];
    private readonly DispatcherTimer _deadEndTimer, _recheckTimer;
    private readonly int _ownProcess = Environment.ProcessId;
    // Apps entered from Home: closing one returns to Home rather than to whatever app Windows
    // activates next (Windows never picks Home, an owned window, as the next window itself).
    private readonly HashSet<nint> _fromHome = [];
    private readonly Dictionary<nint, long> _firstSeen = [];
    private nint _foregroundApp, _previousApp, _recheck;
    private long _foregroundAppSince, _previousAppSince, _lastAutoHome;
    private bool _homeWasForeground;
    private bool _disposed;

    /// <summary>Foreground changes (any process, including this one) while active.</summary>
    internal event Action<nint>? ForegroundChanged;
    /// <summary>A taskbar window became visible.</summary>
    internal event Action<nint>? TrayShown;
    /// <summary>Whether the hooks are installed (tablet mode).</summary>
    internal bool IsActive => _hooks.Count > 0;

    /// <param name="dispatcher">UI dispatcher; hooks and callbacks run on its thread.</param>
    /// <param name="home">Handle of the Home window (0 while it has none).</param>
    internal WindowManager(Dispatcher dispatcher, Func<nint> home)
    {
        _dispatcher = dispatcher;
        _home = home;
        _callback = OnEvent;
        _deadEndTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(250) };
        _deadEndTimer.Tick += (_, _) => { _deadEndTimer.Stop(); CheckDeadEnd(); };
        // Some apps restore their saved bounds right after activation; maximize once more.
        _recheckTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(450) };
        _recheckTimer.Tick += (_, _) =>
        {
            _recheckTimer.Stop();
            if (_recheck != 0 && AppWindows.GetForegroundWindow() == _recheck) TryMaximize(_recheck, "recheck");
            _recheck = 0;
        };
    }

    /// <summary>Installs the hooks (entering tablet mode).</summary>
    internal void Start()
    {
        if (_disposed || IsActive) return;
        // Foreground/minimize include this process, so Home activation is observed too.
        Add(EventSystemForeground, EventSystemForeground, 0);
        Add(EventSystemMinimizeStart, EventSystemMinimizeEnd, 0);
        Add(EventObjectDestroy, EventObjectHide, SkipOwnProcess);
        _foregroundApp = _previousApp = 0;
        _homeWasForeground = AppWindows.GetForegroundWindow() == _home();
        TabletLog.Write("windows", "window manager on");

        void Add(uint first, uint last, uint flags)
        {
            var hook = SetWinEventHook(first, last, 0, _callback, 0, 0, flags);
            if (hook != 0) _hooks.Add(hook);
        }
    }

    /// <summary>Removes the hooks (leaving tablet mode) and gives the windows it maximized their own size back.</summary>
    internal void Stop()
    {
        if (!IsActive) return;
        foreach (var hook in _hooks) UnhookWinEvent(hook);
        _hooks.Clear(); _known.Clear(); _fromHome.Clear(); _firstSeen.Clear();
        _deadEndTimer.Stop(); _recheckTimer.Stop();
        var restored = 0;
        foreach (var hwnd in _maximized)
        {
            // Untouched since: still maximized, not minimized, not taken over by Split View.
            if (!AppWindows.IsZoomed(hwnd) || AppWindows.IsIconic(hwnd) || TabletShell.SplitViewWindows.Contains(hwnd) || !AppWindows.IsAppWindow(hwnd)) continue;
            ShowWindowAsync(hwnd, SwShowNoActivate); // restores the normal bounds without activating
            restored++;
        }
        _maximized.Clear();
        TabletLog.Write("windows", $"window manager off, {restored} window(s) restored to their own size");
    }

    /// <summary>
    /// Maximizes every eligible, non-minimized app window, bottom of the z-order first so their
    /// order is mostly kept. Minimized windows are maximized when restored. Returns the count.
    /// </summary>
    internal int MaximizeAll()
    {
        var count = 0;
        var windows = AppWindows.List();
        for (var i = windows.Count - 1; i >= 0; i--)
        {
            Track(windows[i].Handle);
            if (!windows[i].IsMinimized && TryMaximize(windows[i].Handle, "tablet entry")) count++;
        }
        return count;
    }

    private void OnEvent(nint hook, uint eventId, nint hwnd, int objectId, int childId, uint thread, uint time)
    {
        if (objectId != 0 || childId != 0 || hwnd == 0 || _disposed) return;
        switch (eventId)
        {
            case EventSystemForeground: OnForeground(hwnd); break;
            case EventSystemMinimizeStart: OnMinimize(hwnd); break;
            case EventSystemMinimizeEnd:
                if (AppWindows.IsAppWindow(hwnd)) TryMaximize(hwnd, "restored");
                break;
            case EventObjectShow:
                if (TaskbarProbe(hwnd)) TrayShown?.Invoke(hwnd);
                break;
            case EventObjectDestroy or EventObjectHide: OnGone(hwnd); break;
        }
    }

    private void Track(nint hwnd) { if (_known.Add(hwnd)) _firstSeen[hwnd] = Environment.TickCount64; }

    // An app window closed or hid itself (to the tray, say).
    private void OnGone(nint hwnd)
    {
        if (!_known.Remove(hwnd)) return;
        _firstSeen.Remove(hwnd);
        var fromHome = _fromHome.Remove(hwnd);
        // It was the app on screen: either still the foreground, or Windows just activated
        // an older window in its place (not a successor it created, like splash -> main).
        var active = hwnd == _foregroundApp || hwnd == _previousApp && Environment.TickCount64 - _foregroundAppSince < 600
            && _firstSeen.TryGetValue(_foregroundApp, out var next) && next < _previousAppSince;
        if (fromHome && active) RequestHome("closed an app opened from Home", TimeSpan.FromMilliseconds(30));
        else QueueDeadEndCheck(); // last app closed: Windows may leave the desktop in front
        if (hwnd == _foregroundApp) _foregroundApp = 0;
    }

    // Cheap pre-filter: only top-level windows of explorer can be the taskbar.
    private static bool TaskbarProbe(nint hwnd) => GetParent(hwnd) == 0 && Core.TaskbarControl.IsTrayWindow(hwnd);

    private void OnForeground(nint hwnd)
    {
        ForegroundChanged?.Invoke(hwnd);
        AppWindows.GetWindowThreadProcessId(hwnd, out var pid);
        // Our overlays (switcher, dock, Control Center) are transparent to "where the user came from".
        if (pid == _ownProcess) { if (hwnd == _home()) _homeWasForeground = true; return; }
        var fromHome = _homeWasForeground;
        _homeWasForeground = false;
        if (DesktopClasses.Contains(AppWindows.ClassName(hwnd))) { QueueDeadEndCheck(); return; }
        if (!AppWindows.IsAppWindow(hwnd)) return;
        Track(hwnd);
        if (fromHome) _fromHome.Add(hwnd);
        else if (hwnd != _foregroundApp) _fromHome.Remove(hwnd);
        if (hwnd != _foregroundApp) { _previousApp = _foregroundApp; _previousAppSince = _foregroundAppSince; _foregroundAppSince = Environment.TickCount64; }
        _foregroundApp = hwnd;
        TryMaximize(hwnd, "foreground");
        KeepHomeBelow(hwnd);
        _recheck = hwnd; _recheckTimer.Stop(); _recheckTimer.Start();
    }

    // Home must never sit above the app the user switched to. Windows normally raises the new
    // foreground window, but when Home took the foreground without input (posture change,
    // WinEvent) the activation can leave Home on top; our own window we can always move.
    private void KeepHomeBelow(nint app)
    {
        var home = _home();
        if (home == 0 || (AppWindows.GetWindowLongPtr(app, GwlExStyle).ToInt64() & WsExTopmost) != 0) return;
        for (var w = GetTopWindow(0); w != 0; w = GetWindow(w, 2 /* GW_HWNDNEXT */))
        {
            if (w == app) return; // already above Home
            if (w != home) continue;
            SetWindowPos(home, app, 0, 0, 0, 0, 0x0013); // no move, size or activation
            TabletLog.Write("windows", "moved Home below " + Describe(app));
            return;
        }
    }

    private void OnMinimize(nint hwnd)
    {
        AppWindows.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == _ownProcess) return;
        // Only the app the user is looking at: apps minimizing themselves in the background
        // (to the tray at startup, say) must not pull Home over what the user is doing.
        if (hwnd != _foregroundApp && hwnd != AppWindows.GetForegroundWindow()) return;
        if (!AppWindows.IsAppWindow(hwnd)) return;
        // Let Windows activate the next window first, then cover it with Home.
        RequestHome("minimized " + Describe(hwnd), TimeSpan.FromMilliseconds(60));
    }

    private void QueueDeadEndCheck() { _deadEndTimer.Stop(); _deadEndTimer.Start(); }

    private void CheckDeadEnd()
    {
        if (!IsActive) return;
        var foreground = AppWindows.GetForegroundWindow();
        if (foreground != 0)
        {
            AppWindows.GetWindowThreadProcessId(foreground, out var pid);
            if (pid == _ownProcess || !DesktopClasses.Contains(AppWindows.ClassName(foreground))) return;
        }
        // The shell holds the foreground. If an app still covers the screen (a flyout returned
        // focus to the taskbar, say) leave it; if the desktop itself shows, go Home.
        var top = TopWindow();
        if (top != 0 && top != _home()) return;
        RequestHome(foreground == 0 ? "no foreground window" : "desktop in front", TimeSpan.Zero);
    }

    /// <summary>First visible, non-minimized window in z-order that is Home or an app (topmost overlays ignored).</summary>
    private nint TopWindow()
    {
        var home = _home();
        nint found = 0;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || AppWindows.IsIconic(hwnd) || AppWindows.IsCloaked(hwnd)) return true;
            if ((AppWindows.GetWindowLongPtr(hwnd, GwlExStyle).ToInt64() & WsExTopmost) != 0) return true;
            if (hwnd == home || AppWindows.IsAppWindow(hwnd)) { found = hwnd; return false; }
            return true;
        }, 0);
        return found;
    }

    private void RequestHome(string reason, TimeSpan delay)
    {
        // One automatic Home per 700 ms at most: a shell that keeps re-taking the
        // foreground can never turn this into a loop.
        if (Environment.TickCount64 - _lastAutoHome < 700) return;
        _lastAutoHome = Environment.TickCount64;
        var timer = new DispatcherTimer(DispatcherPriority.Input, _dispatcher) { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!IsActive || !TabletShell.IsTablet) return;
            TabletLog.Write("windows", "home: " + reason);
            TabletShell.ShowHome();
        };
        timer.Start();
    }

    /// <summary>Maximizes <paramref name="hwnd"/> when it is an ordinary resizable app window the policy applies to.</summary>
    private bool TryMaximize(nint hwnd, string? reason)
    {
        if (!TabletShell.IsTablet || TabletShell.SplitViewWindows.Contains(hwnd)) return false;
        if (AppWindows.IsIconic(hwnd) || AppWindows.IsZoomed(hwnd) || !AppWindows.CanMaximize(hwnd)) return false;
        var ex = AppWindows.GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        if ((ex & WsExTopmost) != 0) return false; // picture-in-picture, mini players, sticky notes
        if (AppWindows.ClassName(hwnd) == "#32770") return false; // dialogs that look like apps
        if (IsFullscreen(hwnd)) return false; // borderless games and fullscreen video
        if (AppWindows.Describe(hwnd) == null) return false;
        AppWindows.Maximize(hwnd);
        _maximized.Add(hwnd);
        if (reason != null) TabletLog.Write("windows", $"maximized {Describe(hwnd)} ({reason})");
        return true;
    }

    private static bool IsFullscreen(nint hwnd)
    {
        if ((AppWindows.GetWindowLongPtr(hwnd, GwlStyle).ToInt64() & WsCaption) == WsCaption) return false;
        if (!GetWindowRect(hwnd, out var rect)) return false;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref info)) return false;
        return rect.Left <= info.Monitor.Left && rect.Top <= info.Monitor.Top && rect.Right >= info.Monitor.Right && rect.Bottom >= info.Monitor.Bottom;
    }

    private static string Describe(nint hwnd)
    {
        var title = AppWindows.Title(hwnd);
        return $"{(title.Length > 40 ? title[..40] : title)} [{AppWindows.ClassName(hwnd)}]";
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    private delegate void WinEventProc(nint hook, uint eventId, nint hwnd, int objectId, int childId, uint thread, uint time);
    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetTopWindow(nint parent);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindowAsync(nint hwnd, int command);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}
