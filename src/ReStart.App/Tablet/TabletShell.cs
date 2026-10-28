using System.Windows.Threading;

namespace ReStart.App.Tablet;

/// <summary>
/// Process-wide entry points of the tablet edition. Each feature registers its real
/// implementation at startup; until then the navigation calls are harmless no-ops, so
/// features can call each other without referencing each other's types.
/// </summary>
internal static class TabletShell
{
    /// <summary>Posture tracker; null in preview/test runs.</summary>
    internal static TabletMode? Mode { get; private set; }
    /// <summary>Whether tablet behavior is active right now.</summary>
    internal static bool IsTablet => Mode?.IsTablet ?? false;
    /// <summary>Raised on the UI thread after the posture changes (true = tablet).</summary>
    internal static event Action<bool>? TabletChanged;
    /// <summary>UI dispatcher of the launcher.</summary>
    internal static Dispatcher? Dispatcher { get; private set; }

    /// <summary>Bring the Home screen to the front (iPad "go home"). Owner: Home/shell core.</summary>
    internal static Action ShowHome { get; set; } = static () => { };
    /// <summary>Open the app switcher (swipe up and hold). Owner: switcher.</summary>
    internal static Action ShowAppSwitcher { get; set; } = static () => { };
    /// <summary>Open Control Center (swipe down from the top-right). Owner: Control Center.</summary>
    internal static Action ShowControlCenter { get; set; } = static () => { };
    /// <summary>
    /// Show the real Windows desktop while still in tablet posture (Control Center's Desktop
    /// button): Home hides, the taskbar returns and apps keep their own size until the next
    /// <see cref="ShowHome"/>. Owner: shell core.
    /// </summary>
    internal static Action ShowDesktop { get; set; } = static () => { };
    /// <summary>Whether a desktop session (see <see cref="ShowDesktop"/>) is active.</summary>
    internal static bool IsDesktopSession { get; set; }
    /// <summary>Whether Control Center is on screen (the dock and chrome stay under it). Owner: Control Center.</summary>
    internal static Func<bool> IsControlCenterOpen { get; set; } = static () => false;
    /// <summary>Show the dock over the current app (short swipe up). Owner: dock.</summary>
    internal static Action ShowDock { get; set; } = static () => { };
    /// <summary>
    /// Windows currently arranged by Split View. The window manager must not maximize these;
    /// Split View adds/removes them (UI thread only).
    /// </summary>
    internal static HashSet<nint> SplitViewWindows { get; } = new();
    /// <summary>Close transient overlays (dock, Control Center, switcher). Owners register via <see cref="OverlaysClosing"/>.</summary>
    internal static void CloseOverlays() => OverlaysClosing?.Invoke();
    /// <summary>Overlays subscribe and hide themselves.</summary>
    internal static event Action? OverlaysClosing;

    /// <summary>Starts posture tracking. Called once by App startup in normal (non-preview) runs.</summary>
    internal static void Initialize(Dispatcher dispatcher)
    {
        if (Mode != null) return;
        Dispatcher = dispatcher;
        Mode = new TabletMode(dispatcher);
        Mode.Changed += value => TabletChanged?.Invoke(value);
        TabletCommands.Start(dispatcher);
        TabletLog.Write("shell", "initialized, " + (Mode.IsTablet ? "tablet" : "laptop") + (TabletMode.ReadOverride() is { } o ? $" (override {o})" : ""));
    }

    /// <summary>Stops posture tracking on exit.</summary>
    internal static void Shutdown() { TabletCommands.Stop(); Mode?.Dispose(); Mode = null; }
}
