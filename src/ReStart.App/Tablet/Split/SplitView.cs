using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ReStart.App.Tablet.Gestures;
using static ReStart.App.Tablet.Gestures.NavNative;

namespace ReStart.App.Tablet.Split;

/// <summary>
/// Two apps side by side in the work area (top/bottom in portrait), separated by a
/// draggable divider that snaps to 1/3, 1/2 and 2/3; dragging it to an edge ends the split
/// and gives the remaining app the whole screen. The pair is registered in
/// <see cref="TabletShell.SplitViewWindows"/> so the window manager leaves it alone.
/// </summary>
internal sealed class SplitView
{
    internal const double Gap = 12; // DIP between the apps (the divider's visible width)
    private static readonly double[] Snaps = [1 / 3.0, 0.5, 2 / 3.0];
    private const double EndZone = 0.12;

    private readonly SplitDivider _divider;
    private readonly NavNative.WinEventProc _proc;
    private readonly List<nint> _hooks = [];
    private nint _first, _second; // left/top, right/bottom
    private double _ratio = 0.5;
    private bool _vertical;       // true: side by side; false: stacked (portrait)
    private DispatcherTimer? _resize;

    internal SplitView()
    {
        _divider = new SplitDivider(this);
        _proc = OnWinEvent;
        TabletNavigation.Watcher.Changed += OnForeground;
    }

    internal bool Active => _first != 0;
    internal bool Contains(nint hwnd) => hwnd != 0 && (hwnd == _first || hwnd == _second);

    /// <summary>Arranges <paramref name="first"/> (left/top) and <paramref name="second"/> and shows the divider.</summary>
    internal void Start(nint first, nint second)
    {
        if (first == second || !IsWindow(first) || !IsWindow(second)) return;
        if (Active) End(null, maximize: false);
        _first = first; _second = second; _ratio = 0.5;
        TabletShell.SplitViewWindows.Add(first);
        TabletShell.SplitViewWindows.Add(second);
        // Watch both windows: closing, hiding or minimizing one ends the split.
        _hooks.Add(SetWinEventHook(EventSystemMinimizeStart, EventSystemMinimizeStart, 0, _proc, 0, 0, 0));
        foreach (var pid in new[] { first, second }.Select(h => { GetWindowThreadProcessId(h, out var p); return p; }).Distinct())
            _hooks.Add(SetWinEventHook(EventObjectDestroy, EventObjectHide, 0, _proc, pid, 0, 0));
        Apply(restore: true);
        AppWindows.SetForeground(second);
        AppWindows.SetForeground(first);
        _divider.Present();
        TabletLog.Write("split", $"start {AppWindows.Title(first)} | {AppWindows.Title(second)}");
    }

    /// <summary>Ends the split; <paramref name="keep"/> (if given and alive) gets maximized.</summary>
    internal void End(nint? keep, bool maximize = true)
    {
        if (!Active) return;
        var (a, b) = (_first, _second);
        _first = _second = 0;
        TabletShell.SplitViewWindows.Remove(a);
        TabletShell.SplitViewWindows.Remove(b);
        foreach (var hook in _hooks) UnhookWinEvent(hook);
        _hooks.Clear();
        _resize?.Stop();
        _divider.Hide();
        if (maximize && TabletNavigation.Active)
            foreach (var hwnd in keep is { } k ? [k] : new[] { a, b })
                if (IsWindow(hwnd) && AppWindows.CanMaximize(hwnd)) AppWindows.Maximize(hwnd);
        TabletLog.Write("split", "end" + (keep is { } kept && kept != 0 ? " → " + AppWindows.Title(kept) : ""));
    }

    /// <summary>Re-lays out after rotation or resolution changes.</summary>
    internal void Relayout() { if (Active) { Apply(restore: false); _divider.Present(); } }

    internal void RaiseDivider() { if (Active && _divider.IsVisible) _divider.Raise(); }

    // ---- geometry -------------------------------------------------------------------

    private (double Scale, Rect Work) Area()
    {
        var (_, work) = TabletNavigation.Screen();
        return (GetDpiForWindow(_divider.Handle) / 96.0, work);
    }

    /// <summary>The divider's rectangle (physical pixels) for the current ratio.</summary>
    internal Rect DividerRect(double? ratio = null)
    {
        var (scale, work) = Area();
        var gap = Gap * scale;
        var r = ratio ?? _ratio;
        return _vertical
            ? new Rect(work.Left + (work.Width - gap) * r, work.Top, gap, work.Height)
            : new Rect(work.Left, work.Top + (work.Height - gap) * r, work.Width, gap);
    }

    internal bool Vertical => _vertical;

    private void Apply(bool restore)
    {
        var (scale, work) = Area();
        _vertical = work.Width >= work.Height;
        var gap = Gap * scale;
        Rect a, b;
        if (_vertical)
        {
            var w = (work.Width - gap) * _ratio;
            a = new Rect(work.Left, work.Top, w, work.Height);
            b = new Rect(work.Left + w + gap, work.Top, work.Right - (work.Left + w + gap), work.Height);
        }
        else
        {
            var h = (work.Height - gap) * _ratio;
            a = new Rect(work.Left, work.Top, work.Width, h);
            b = new Rect(work.Left, work.Top + h + gap, work.Width, work.Bottom - (work.Top + h + gap));
        }
        Position(_first, a, restore);
        Position(_second, b, restore);
    }

    // Places a window so its visible frame (not its invisible resize borders) fills the rectangle.
    private static void Position(nint hwnd, Rect target, bool restore)
    {
        if (restore && (AppWindows.IsZoomed(hwnd) || AppWindows.IsIconic(hwnd))) ShowWindow(hwnd, SwRestore);
        GetWindowRect(hwnd, out var outer);
        var visible = VisibleBounds(hwnd);
        double left = visible.Left - outer.Left, top = visible.Top - outer.Top, right = outer.Right - visible.Right, bottom = outer.Bottom - visible.Bottom;
        SetWindowPos(hwnd, 0, (int)Math.Round(target.Left - left), (int)Math.Round(target.Top - top),
            (int)Math.Round(target.Width + left + right), (int)Math.Round(target.Height + top + bottom), SwpNoActivate | SwpNoZOrder | SwpNoOwnerZOrder);
    }

    // ---- divider drag ---------------------------------------------------------------

    /// <summary>Divider moved to <paramref name="ratio"/> (0..1 of the work area); resizes the apps at a bounded rate.</summary>
    internal void Drag(double ratio)
    {
        _ratio = Math.Clamp(ratio, 0.05, 0.95);
        if (_resize == null)
        {
            _resize = new DispatcherTimer(TimeSpan.FromMilliseconds(60), DispatcherPriority.Render, (_, _) => { _resize!.Stop(); Apply(restore: false); }, _divider.Dispatcher);
        }
        if (!_resize.IsEnabled) _resize.Start();
    }

    /// <summary>Released: snap, or end the split when dropped near an edge.</summary>
    internal void Release(double ratio)
    {
        if (ratio < EndZone) { End(_second); return; }
        if (ratio > 1 - EndZone) { End(_first); return; }
        _ratio = Snaps.MinBy(s => Math.Abs(s - ratio));
        _resize?.Stop();
        Apply(restore: false);
        _divider.Present();
        TabletLog.Write("split", $"ratio {_ratio:0.00}");
    }

    // ---- window tracking ------------------------------------------------------------

    private void OnWinEvent(nint hook, uint type, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != 0 || type == 0x8002 /* SHOW */ || !Contains(hwnd)) return;
        var other = hwnd == _first ? _second : _first;
        _divider.Dispatcher.BeginInvoke(() => { if (Contains(hwnd)) End(other); });
    }

    private void OnForeground(nint hwnd)
    {
        if (!Active) return;
        if (Contains(hwnd))
        {
            // Keep the pair together: the partner sits right behind the focused one.
            var other = hwnd == _first ? _second : _first;
            SetWindowPos(other, hwnd, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpNoOwnerZOrder);
            _divider.Present();
            return;
        }
        GetWindowThreadProcessId(hwnd, out var pid);
        var root = GetAncestor(hwnd, 3 /* GA_ROOTOWNER */);
        if (Contains(root)) return; // a dialog of one of the pair
        if (pid == Environment.ProcessId || !AppWindows.IsAppWindow(hwnd) && !AppWindows.IsAppWindow(root))
        {
            // Home, shell or our own surfaces: the pair stays arranged underneath.
            _divider.Hide();
            return;
        }
        End(null, maximize: false); // a third app came to the front, full screen
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
}

/// <summary>The handle between Split View apps: a thin topmost strip with a grabber pill.</summary>
internal sealed class SplitDivider : TabletSurface
{
    private readonly SplitView _split;
    private readonly Border _pill;
    private readonly Border _line;
    private double _startRatio;

    internal SplitDivider(SplitView split) : base(transparent: false)
    {
        _split = split;
        Background = Brushes.Black;
        _pill = new Border { CornerRadius = new CornerRadius(Design.TabletStyle.ControlRadius), Background = new SolidColorBrush(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _line = new Border { Background = Brushes.Black };
        Content = new Grid { Children = { _line, _pill } };
        Input.Down += _ => { _startRatio = Ratio(Input.Start); Grow(true); };
        Input.Move += s => { var r = Ratio(s); Place(_split.DividerRect(r)); _split.Drag(r); };
        Input.Up += s => { Grow(false); _split.Release(Ratio(s)); };
        Input.Cancel += _ => { Grow(false); _split.Release(_startRatio); };
    }

    private double Ratio(PointerSample s)
    {
        var (_, work) = TabletNavigation.Screen();
        return _split.Vertical ? (s.Screen.X - work.Left) / work.Width : (s.Screen.Y - work.Top) / work.Height;
    }

    private void Grow(bool pressed)
    {
        var length = pressed ? 72 : 48;
        if (_split.Vertical) { _pill.Width = 5; _pill.Height = length; } else { _pill.Width = length; _pill.Height = 5; }
        _pill.Background = new SolidColorBrush(pressed ? Colors.White : Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF));
    }

    internal void Present()
    {
        Grow(false);
        ShowAt(_split.DividerRect());
        TabletNavigation.Bar.Raise();
    }
}
