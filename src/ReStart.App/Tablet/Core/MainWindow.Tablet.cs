using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ReStart.App.Services;

namespace ReStart.App;

// Tablet edition: in tablet mode the launcher is the Home screen. It covers the whole
// monitor, is not topmost and never hides itself: it is the layer under the apps, which
// open maximized on top of it. "Go home" brings it back to the front.
public partial class MainWindow
{
    private bool _tabletHome, _tabletCovered, _startupSilent;

    /// <summary>Whether the window currently acts as the tablet Home screen.</summary>
    internal bool IsTabletHome => _tabletHome;
    /// <summary>Whether Home is the active window (not covered by an app).</summary>
    internal bool IsHomeInFront => _tabletHome && IsVisible && IsActive;
    /// <summary>Whether the first presentation can skip the startup splash.</summary>
    internal bool IsPrepared => _startupComplete || IsReady && IconsReady && ScriptTileCount == 0;
    // Live tiles run only while the board can actually be seen (Home behind a maximized app cannot).
    private bool BoardVisible => IsVisible && !_tabletCovered;

    /// <summary>Turns the launcher into Home and presents it. The caller drops topmost after arranging apps (see <see cref="SettleTabletHome"/>).</summary>
    internal void EnterTabletHome()
    {
        _tabletHome = true;
        _tabletCovered = false;
        ApplyTabletSearch(true);
        Present();
    }

    /// <summary>Drops the temporary topmost used while apps are maximized behind Home on entry.</summary>
    internal void SettleTabletHome()
    {
        if (!_tabletHome) return;
        Topmost = false; // HWND_NOTOPMOST keeps Home at the top of the normal band
        if (!IsVisible) return;
        // A window maximized behind Home may have taken activation; Home is what the user sees.
        ForegroundActivation.Request(new WindowInteropHelper(this).Handle);
        SetTabletCovered(false);
    }

    /// <summary>Restores the upstream Start-menu behavior (laptop mode).</summary>
    internal void LeaveTabletHome()
    {
        if (!_tabletHome) return;
        var front = IsVisible && IsActive;
        _tabletHome = false;
        _tabletCovered = false;
        ApplyTabletSearch(false);
        Topmost = true;
        if (_startupWindow != null) { CancelStartupPresentation(); _ = WarmUpAsync(); }
        else if (front) Dismiss();
        else if (IsVisible) { StopMotion(); Hide(); }
    }

    /// <summary>iPad "go home": closes overlays and brings Home to the front, reset to the pinned view, without minimizing apps.</summary>
    internal void ShowTabletHome()
    {
        Tablet.TabletShell.CloseOverlays();
        EndTabletEditing(); // swipe up while rearranging = Done
        Present();
        // Present asks politely for the foreground. If a shell surface holds it (Start, search),
        // insist, and at least raise Home above the apps so "go home" never silently fails.
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != 0 && Tablet.AppWindows.GetForegroundWindow() != handle && !Tablet.AppWindows.SetForeground(handle))
        {
            SetWindowPos(handle, 0 /* HWND_TOP */, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040); // NOSIZE|NOMOVE|NOACTIVATE|SHOWWINDOW
            Tablet.TabletLog.Write("home", "foreground refused; raised Home instead");
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

    // A finger-sized search field, always shown on Home; tapping it brings up the touch keyboard.
    private void ApplyTabletSearch(bool tablet)
    {
        if (SearchBox.Parent is System.Windows.Controls.Grid field) field.Width = tablet ? 460 : 280;
        SearchBox.Height = tablet ? 46 : 36;
        if (tablet) SearchBox.FontSize = 16; else SearchBox.ClearValue(FontSizeProperty);
        ViewTitle.Visibility = tablet ? Visibility.Collapsed : Visibility.Visible;
        UpdateSearchChrome(SearchBox.Text.Trim().Length > 0 || _searchRequested);
    }

    /// <summary>Tells Home whether an app covers it, pausing or resuming live tiles accordingly.</summary>
    internal void SetTabletCovered(bool covered)
    {
        if (!_tabletHome || _tabletCovered == covered) return;
        _tabletCovered = covered;
        if (covered) PauseScripts(keepWorkers: true);
        else if (IsVisible) QueueTileResume();
    }

    /// <summary>Keeps Home sized to the monitor after rotation or resolution changes.</summary>
    internal void RefitTabletHome()
    {
        if (!_tabletHome || !IsVisible || _preview) return;
        var screen = System.Windows.Forms.Screen.PrimaryScreen!;
        _wallpaperMonitor = screen.Bounds;
        MonitorPlacement.FitWorkingArea(new WindowInteropHelper(this).Handle, 0, screen.Bounds, 0x0014); // no z-order or activation change
    }

    // Present() helpers: Home always uses the internal (primary) display, full bounds.
    private System.Windows.Forms.Screen? TabletHomeScreen => _tabletHome ? System.Windows.Forms.Screen.PrimaryScreen : null;
    // Home behind an app enters like a hidden launcher, so going home animates.
    private bool TabletHomeBehind => _tabletHome && IsVisible && !IsActive && OwnedWindows.Count == 0;

    private void RaiseTabletHome()
    {
        if (_tabletHome && IsVisible) Native.SetWindowPos(new WindowInteropHelper(this).Handle, Topmost ? -1 : 0, 0, 0, 0, 0, 0x0013); // no move/size/activate
    }

    /// <summary>
    /// Prepares live-tile frames in the background, exactly like the startup splash does but
    /// without showing it, so the first presentation (tablet Home or Start) is instant.
    /// A presentation requested meanwhile takes over and shows the splash only for the remainder.
    /// </summary>
    internal async Task WarmUpAsync()
    {
        await InitializeAsync();
        if (_exit || _startupComplete || _startupWanted || _preview) return;
        if (ScriptTileCount == 0) { _startupComplete = true; return; }
        _startupSilent = true;
        _startupWanted = true;
        _startupPoint = System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Location;
        StartStartupTileWarmup();
        await (_startupPresentation ??= CompleteStartupPresentationAsync());
    }

    /// <summary>Launches a catalog app by display name through the normal tile path (tests and button bindings).</summary>
    internal bool LaunchByName(string name)
    {
        var app = _apps.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? _apps.FirstOrDefault(a => a.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
        if (app == null) return false;
        Launch(app);
        return true;
    }

    /// <summary>Lets the next presentation skip the startup splash while a background warm-up is still filling tiles.</summary>
    internal void SkipStartupSplash()
    {
        if (_startupComplete || _startupWindow != null) return;
        _startupComplete = true; // the warm-up keeps running and ends quietly (see CompleteStartupPresentationAsync)
        MarkStartup("splash skipped");
    }

    // Tablet edition: no idle keyboard focus in the search box on Home, so the touch keyboard never opens by itself.
    private bool SkipIdleSearchFocus()
    {
        if (!_tabletHome || SearchBox.Text.Length > 0 || _searchRequested) return false;
        Keyboard.Focus(this);
        return true;
    }
}
