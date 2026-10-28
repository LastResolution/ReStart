using System.Windows;
using Microsoft.Win32;
using ReStart.App.Tablet.Dock;
using ReStart.App.Tablet.Split;
using ReStart.App.Tablet.Switcher;

namespace ReStart.App.Tablet.Gestures;

/// <summary>
/// Installs the iPad-style navigation (home bar, Control Center edge, app switcher, dock,
/// Split View), shows it only in tablet mode, and holds the helpers the pieces share.
/// </summary>
internal static class TabletNavigation
{
    internal static ForegroundWatcher Watcher { get; private set; } = null!;
    internal static HomeBar Bar { get; private set; } = null!;
    internal static ControlCenterEdge Edge { get; private set; } = null!;
    internal static SwitcherOverlay Switcher { get; private set; } = null!;
    internal static DockWindow Dock { get; private set; } = null!;
    internal static SplitView Split { get; private set; } = null!;
    internal static bool Active { get; private set; }

    /// <summary>Creates the surfaces (hidden) and follows the posture. Call once after <see cref="TabletShell.Initialize"/>.</summary>
    internal static void Install() => Guard("install", InstallCore);

    private static void InstallCore()
    {
        if (TabletShell.Dispatcher is not { } dispatcher || Watcher != null) return;
        Design.TabletMotion.UpdateFrameRate();
        Watcher = new ForegroundWatcher();
        Switcher = new SwitcherOverlay();
        Dock = new DockWindow();
        Split = new SplitView();
        Bar = new HomeBar();
        Edge = new ControlCenterEdge();
        TabletShell.ShowAppSwitcher = () => { if (Active) Switcher.Open(); };
        TabletShell.ShowDock = () => { if (Active) Dock.ShowTransient(); };
        TabletShell.OverlaysClosing += () => { Switcher.Dismiss(); Dock.HideTransient(); };
        TabletShell.TabletChanged += Apply;
        SystemEvents.DisplaySettingsChanged += (_, _) => dispatcher.BeginInvoke(DisplayChanged);
        TabletCommands.Register("nav-dump", _ => Dump());
        TabletCommands.Register("quick-switch", arg => Switcher.QuickSwitchNow(arg == "next" ? -1 : 1));
        TabletCommands.Register("split", arg => SplitCommand(arg));
        TabletCommands.Register("split-end", _ => Split.End(null));
        TabletCommands.Register("dock-hide", _ => Dock.HideTransient());
        TabletCommands.Register("nav-trace", arg => PointerInput.Trace = arg != "off");
        // Test helper: "nav-activate WindowsTerminal.exe" restores that app as it was (no maximize).
        TabletCommands.Register("nav-activate", arg =>
        {
            if (Recent().FirstOrDefault(w => AppInfo.ExeName(w).Equals(arg, StringComparison.OrdinalIgnoreCase)) is { } window) AppWindows.Activate(window.Handle, maximize: false);
        });
        Apply(TabletShell.IsTablet);
    }

    private static void Apply(bool tablet)
    {
        if (tablet == Active) return;
        Active = tablet;
        TabletLog.Write("nav", tablet ? "enabled" : "disabled");
        Guard("apply", () =>
        {
            if (tablet) { Bar.Enable(); Edge.Enable(); Dock.Enable(); }
            else { Switcher.Dismiss(immediately: true); Split.End(null); Dock.Disable(); Edge.Disable(); Bar.Disable(); }
        });
    }

    /// <summary>Runs <paramref name="action"/> once after <paramref name="delay"/> on the UI thread.</summary>
    internal static void Later(TimeSpan delay, Action action)
    {
        var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Normal, TabletShell.Dispatcher!) { Interval = delay };
        timer.Tick += (_, _) => { timer.Stop(); Guard("timer", action); };
        timer.Start();
    }

    /// <summary>Navigation must never take the shell down: failures are logged instead.</summary>
    internal static void Guard(string what, Action action)
    {
        try { action(); }
        catch (Exception ex) { TabletLog.Write("nav", $"{what} failed: {ex}"); }
    }

    private static void DisplayChanged()
    {
        if (!Active) return;
        Design.TabletMotion.UpdateFrameRate();
        Bar.Reposition(); Edge.Reposition(); Dock.Reposition(); Split.Relayout(); Switcher.Dismiss(immediately: true);
    }

    /// <summary>Monitor bounds and work area of the tablet panel, in physical pixels.</summary>
    internal static (Rect Monitor, Rect Work) Screen()
    {
        var (monitor, work) = NavNative.PrimaryMonitor();
        return (new Rect(monitor.Left, monitor.Top, monitor.Width, monitor.Height), new Rect(work.Left, work.Top, work.Width, work.Height));
    }

    /// <summary>The app in front (the owner of a focused dialog counts), or 0 when Home or shell UI is in front.</summary>
    internal static nint CurrentApp()
    {
        var foreground = AppWindows.GetForegroundWindow();
        if (AppWindows.IsAppWindow(foreground)) return foreground;
        var root = NavNative.GetAncestor(foreground, 3 /* GA_ROOTOWNER */);
        return root != 0 && AppWindows.IsAppWindow(root) ? root : 0;
    }

    /// <summary>App windows, most recently used first.</summary>
    internal static List<AppWindow> Recent() => Watcher.Recent();

    /// <summary>Whether Home (ReStart's own window or the bare desktop) is what the user sees.</summary>
    // With no app in front (all minimized, shell or ReStart focused), what's visible is Home.
    // A desktop session shows the real desktop, which is not Home (no dock there).
    internal static bool IsHomeFront() => !TabletShell.IsDesktopSession && CurrentApp() == 0;

    /// <summary>Goes Home through the core shell hook, falling back to presenting ReStart until Home is installed.</summary>
    internal static void GoHome()
    {
        if (IsDefault(TabletShell.ShowHome)) (Application.Current?.MainWindow as MainWindow)?.Present();
        else TabletShell.ShowHome();
        RaiseChrome();
        Dock.Refresh();
    }

    /// <summary>
    /// An app is about to come to the front from Home. Until the core Home lands, Home is the
    /// topmost Start window, which would cover the app, so the stand-in dismisses it.
    /// </summary>
    internal static void LeavingHome()
    {
        if (IsDefault(TabletShell.ShowHome) && Application.Current?.MainWindow is MainWindow { IsVisible: true } main) main.Dismiss(immediately: true);
    }

    /// <summary>Opens Control Center (owned by the Control Center feature).</summary>
    internal static void OpenControlCenter()
    {
        if (IsDefault(TabletShell.ShowControlCenter)) TabletLog.Write("nav", "control center not installed");
        else TabletShell.ShowControlCenter();
    }

    /// <summary>Brings an app to the front full screen, ending Split View unless it belongs to the pair being restored.</summary>
    internal static void ActivateApp(nint hwnd, bool keepSplit = false)
    {
        LeavingHome();
        if (!keepSplit && Split.Contains(hwnd)) Split.End(hwnd);
        AppWindows.Activate(hwnd, maximize: !Split.Contains(hwnd));
        Watcher.Touch(hwnd);
    }

    /// <summary>Keeps the system chrome stacked above apps and Home: dock and divider, then the bars.</summary>
    internal static void RaiseChrome()
    {
        if (!Active) return;
        Split.RaiseDivider();
        Dock.RaiseIfVisible();
        Bar.Raise();
        Edge.Raise();
    }

    // The foundation's placeholders are static lambdas declared in TabletShell.
    private static bool IsDefault(Action action) => action.Method.DeclaringType is { } type && (type == typeof(TabletShell) || type.DeclaringType == typeof(TabletShell));

    private static void SplitCommand(string? argument)
    {
        // "split" pairs the two most recent apps; "split <i> <j>" picks by recent index.
        var recent = Recent();
        var parts = (argument ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int a = parts.Length > 0 && int.TryParse(parts[0], out var x) ? x : 0, b = parts.Length > 1 && int.TryParse(parts[1], out var y) ? y : 1;
        if (a < recent.Count && b < recent.Count && a != b) Split.Start(recent[a].Handle, recent[b].Handle);
    }

    private static void Dump()
    {
        var (monitor, work) = Screen();
        var foreground = AppWindows.GetForegroundWindow();
        TabletLog.Write("nav", $"monitor {monitor} work {work} scale {Bar.Scale} home-front {IsHomeFront()} current {CurrentApp():X} foreground {foreground:X} [{AppWindows.ClassName(foreground)}] \"{AppWindows.Title(foreground)}\" bar {Bar.Bounds} dock {Dock.Bounds} visible {Dock.IsVisible}");
        foreach (var w in Recent()) TabletLog.Write("nav", $"  {w.Handle:X} {AppInfo.Name(w)} [{AppInfo.ExeName(w)}] \"{w.Title}\" min={w.IsMinimized} split={Split.Contains(w.Handle)} rect={NavNative.VisibleBounds(w.Handle)}");
    }
}
