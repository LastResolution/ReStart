using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ReStart.App.Services;
using ReStart.App.Tablet.Gestures;
using ReStart.App.Tablet.Design;

namespace ReStart.App.Tablet.Dock;

/// <summary>
/// The shelf (the iPad dock in Home's language): a flat, full-width band of Home's panel
/// material along the bottom edge, running down behind the home bar so the two read as one
/// strip. Favorites, a hairline, then recent apps; running apps carry an accent underline like
/// the Windows 10 taskbar. Home reserves its height (tiles never slide under it); over apps
/// the same shelf rises with a short home-bar swipe and goes away after a launch, a tap
/// outside it, or a few idle seconds.
/// </summary>
internal sealed class DockWindow : TabletSurface
{
    private const double Cell = TabletStyle.ShelfCell, IconSize = TabletStyle.ShelfIcon; // DIP

    private readonly DockModel _model = new();
    private readonly StackPanel _items = new() { Orientation = Orientation.Horizontal };
    private readonly Border _panel;
    private readonly SolidColorBrush _material = new(TabletStyle.Panel), _hairline = new(TabletStyle.Hairline),
        _pressedBrush = new(TabletStyle.Pressed), _mark = new(TabletStyle.Accent);
    private readonly FrameMeter _frames = new("dock");
    private readonly DockCatcher _catcher;
    private readonly FrameClock _clock;
    // Critically damped: the shelf is flush with the bottom edge, an overshoot would open a gap under it.
    private readonly Spring _y = new(0, TabletMotion.SurfaceResponse, 1);
    private readonly DispatcherTimer _idle;
    private List<DockItem> _current = [];
    private enum State { Hidden, Home, Revealing, Transient }
    private State _state;
    private bool _enabled;
    private FrameworkElement? _pressed;
    private double _shownY, _hiddenY;

    // Opaque window: no layered-window readback, so moving and pressing it stays cheap at 180 Hz.
    internal DockWindow() : base(transparent: false)
    {
        _items.HorizontalAlignment = HorizontalAlignment.Center;
        _items.VerticalAlignment = VerticalAlignment.Top;
        _items.Height = TabletStyle.ShelfHeight;
        _panel = new Border { Background = _material, BorderBrush = _hairline, BorderThickness = new Thickness(0, 1, 0, 0), Child = _items };
        Background = _material;
        Content = _panel;
        _catcher = new DockCatcher(this);
        _clock = new FrameClock(Tick);
        _idle = new DispatcherTimer(TimeSpan.FromSeconds(6), DispatcherPriority.Normal, (_, _) => HideTransient(), Dispatcher);
        _model.Changed += Rebuild;
        Input.Down += OnDown;
        Input.Move += OnMove;
        Input.Up += OnUp;
        Input.Cancel += _ => Press(null);
        TabletNavigation.Watcher.Changed += _ => Dispatcher.BeginInvoke(Refresh, DispatcherPriority.Background);
    }

    internal void Enable()
    {
        _enabled = true;
        _model.EnsureLoaded();
        Refresh();
    }

    internal void Disable()
    {
        _enabled = false;
        _state = State.Hidden;
        _catcher.Hide();
        _clock.Stop();
        Hide();
    }

    internal void Reposition() { if (_enabled) { Layout(); Refresh(); } }

    // ---- placement ------------------------------------------------------------------

    private void Layout()
    {
        var (monitor, _) = TabletNavigation.Screen();
        var scale = NavNative.GetDpiForWindow(Handle) / 96.0;
        var count = Math.Max(1, _current.Count);
        var maxWidth = monitor.Width / scale - 2 * TabletStyle.Gutter - 2 * TabletStyle.Gap;
        var itemWidth = Math.Min(Cell, maxWidth / count);
        foreach (var child in _items.Children.OfType<FrameworkElement>().Where(c => c.Tag is DockItem)) child.Width = itemWidth;
        // The shelf sits on the home bar wherever it ended up (another bottom AppBar can push it up)
        // and runs down behind it to the bottom edge.
        var bar = TabletNavigation.Bar.Bounds;
        var barTop = bar.Height > 0 ? bar.Top : monitor.Bottom - Math.Round(HomeBar.BarHeight * scale);
        _shownY = barTop - Math.Round(TabletStyle.ShelfHeight * scale);
        _hiddenY = monitor.Bottom + 1;
        var height = monitor.Bottom - _shownY;
        var y = _state == State.Hidden ? _hiddenY : Math.Max(_y.Value, _shownY);
        if (_state != State.Hidden) { if (_clock.Running) _y.Target = Math.Max(_y.Target, _shownY); else _y.Snap(y); }
        Place(new Rect(monitor.Left, y, monitor.Width, height), raise: false);
    }

    private bool Tick(double dt)
    {
        _y.Step(dt);
        if (_y.Target < _shownY) _y.Target = _shownY; // the home bar moved meanwhile
        if (_y.Value < _shownY) { _y.Value = _shownY; _y.Velocity = 0; }
        // Done within half a pixel: a critically damped spring otherwise crawls the last pixels for frames.
        if (Math.Abs(_y.Value - _y.Target) < 0.5 && Math.Abs(_y.Velocity) < 30) _y.Snap(_y.Target);
        MoveTo(Bounds.X, _y.Value);
        _frames.Frame(dt);
        if (_y.Settled) _frames.End();
        if (_y.Settled && _y.Target >= _hiddenY - 1 && _state == State.Hidden) { Hide(); TabletNavigation.Bar.Retint(); return false; }
        return !_y.Settled;
    }

    private void SlideTo(double y)
    {
        _y.Target = y;
        if (!_clock.Running) _frames.Begin(y < _hiddenY ? "shelf up" : "shelf down");
        _clock.Start();
        TabletNavigation.Bar.Retint();
    }

    /// <summary>Whether the shelf is up or coming up (the home bar then lets its material through).</summary>
    internal bool ShelfShown => _enabled && IsVisible && _state != State.Hidden;

    /// <summary>The shelf's material, for the home bar over it.</summary>
    internal Color Material => _material.Color;

    // Colors follow Home's theme and accent (read each time the shelf comes up).
    private void ApplyTheme()
    {
        _material.Color = TabletStyle.Panel; _hairline.Color = TabletStyle.Hairline;
        _pressedBrush.Color = TabletStyle.Pressed; _mark.Color = TabletStyle.Accent;
    }

    private void ShowWindow()
    {
        ApplyTheme();
        if (!IsVisible)
        {
            _y.Snap(_hiddenY);
            Layout();
            ShowAt(new Rect(Bounds.X, _hiddenY, Bounds.Width, Bounds.Height));
        }
        Raise();
        TabletNavigation.Bar.Raise();
    }

    internal void RaiseIfVisible() { if (IsVisible && !TabletShell.IsControlCenterOpen()) { if (_catcher.IsVisible) _catcher.Raise(); Raise(); } }

    // ---- state ----------------------------------------------------------------------

    /// <summary>Re-evaluates Home and running apps (foreground changes, overlay closed).</summary>
    internal void Refresh()
    {
        if (!_enabled) return;
        _model.EnsureLoaded();
        Rebuild();
        // Control Center is a tool window: with it in front no app is, yet Home is not what the user sees.
        var home = TabletNavigation.IsHomeFront() && !TabletNavigation.Switcher.IsBusy && !TabletShell.IsControlCenterOpen();
        if (home && _state != State.Home)
        {
            _state = State.Home; _catcher.Hide(); _idle.Stop();
            ShowWindow(); SlideTo(_shownY);
        }
        else if (!home && _state == State.Home) { _state = State.Hidden; SlideTo(_hiddenY); }
        else if (!home && _state == State.Transient && TabletNavigation.Switcher.IsOpen) HideTransient();
    }

    /// <summary>The home bar started a swipe up: get ready to follow the finger.</summary>
    internal void BeginReveal()
    {
        if (!_enabled || _state == State.Home || TabletNavigation.Switcher.IsOpen) return;
        Rebuild();
        if (_state == State.Hidden) { ShowWindow(); _state = State.Revealing; }
    }

    /// <summary>Follows the finger: the dock rises with the first part of the swipe.</summary>
    internal void TrackReveal(double up)
    {
        if (_state != State.Revealing) return;
        // 1:1 with the finger, stopping flush on the home bar.
        _y.Set(Math.Max(_hiddenY - up * Scale, _shownY));
        MoveTo(Bounds.X, _y.Value);
    }

    internal void CancelReveal()
    {
        if (_state is State.Revealing) { _state = State.Hidden; SlideTo(_hiddenY); }
    }

    /// <summary>Shows the dock over the current app until a launch, a tap outside or a timeout.</summary>
    internal void ShowTransient()
    {
        if (!_enabled || _state == State.Home) return;
        Rebuild();
        ShowWindow();
        _state = State.Transient;
        SlideTo(_shownY);
        _catcher.Cover();
        Raise(); TabletNavigation.Bar.Raise();
        _idle.Stop(); _idle.Start();
        TabletLog.Write("dock", "shown over app");
    }

    internal void HideTransient()
    {
        _idle.Stop();
        _catcher.Hide();
        if (_state is State.Transient or State.Revealing) { _state = State.Hidden; SlideTo(_hiddenY); }
    }

    // ---- items ----------------------------------------------------------------------

    private void Rebuild()
    {
        var items = _model.Items();
        var same = items.Count == _current.Count && items.Zip(_current).All(p => p.First.Key == p.Second.Key && p.First.Icon == p.Second.Icon);
        _current = items;
        if (same)
        {
            foreach (var child in _items.Children.OfType<Grid>()) if (child.Tag is DockItem item && child.Children.OfType<Border>().LastOrDefault() is { } mark) mark.Opacity = item.Window != 0 ? 1 : 0;
            return;
        }
        _items.Children.Clear();
        var favorites = true;
        foreach (var item in items)
        {
            if (favorites && !item.Favorite)
            {
                favorites = false;
                _items.Children.Add(new Border { Width = 1, Height = 32, Margin = new Thickness(TabletStyle.Gap, 0, TabletStyle.Gap, 0), VerticalAlignment = VerticalAlignment.Center, Background = _hairline });
            }
            _items.Children.Add(MakeItem(item));
        }
        Layout();
    }

    // One app: a square cell (lit while pressed), the icon centered, an accent underline while it runs.
    private Grid MakeItem(DockItem item)
    {
        var grid = new Grid
        {
            Tag = item, Width = Cell, Height = Cell, Margin = new Thickness(0, (TabletStyle.ShelfHeight - Cell) / 2, 0, 0), VerticalAlignment = VerticalAlignment.Top,
            Background = Brushes.Transparent, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1, 1),
        };
        UIElement face = item.Icon != null
            ? new Image { Source = item.Icon, Width = IconSize, Height = IconSize, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            : new Border
            {
                Width = IconSize, Height = IconSize, Background = _mark, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = item.Name[..1].ToUpperInvariant(), FontFamily = TabletStyle.Text, FontSize = 20, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
        RenderOptions.SetBitmapScalingMode(face, BitmapScalingMode.HighQuality);
        grid.Children.Add(face);
        grid.Children.Add(new Border
        {
            Width = 18, Height = 3, CornerRadius = new CornerRadius(1.5), Background = _mark,
            VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 2),
            Opacity = item.Window != 0 ? 1 : 0,
        });
        System.Windows.Automation.AutomationProperties.SetName(grid, item.Name);
        grid.ToolTip = item.Name;
        return grid;
    }

    private Grid? ItemAt(PointerSample s)
    {
        var p = new Point((s.Screen.X - Bounds.X) / Scale, (s.Screen.Y - Bounds.Y) / Scale);
        foreach (var child in _items.Children.OfType<Grid>())
        {
            var origin = child.TranslatePoint(new Point(), this);
            if (new Rect(origin, child.RenderSize).Contains(p)) return child;
        }
        return null;
    }

    // Windows 10 tile press: the cell lights up and gives a little, then settles back without a bounce.
    private void Press(FrameworkElement? element)
    {
        if (_pressed == element) return;
        if (_pressed is Grid released) { released.Background = Brushes.Transparent; TabletMotion.Scale((ScaleTransform)released.RenderTransform, 1); }
        _pressed = element;
        if (element is Grid pressed) { pressed.Background = _pressedBrush; TabletMotion.Scale((ScaleTransform)pressed.RenderTransform, TabletMotion.PressScale); }
    }

    private void OnDown(PointerSample s) { _idle.Stop(); Press(ItemAt(s)); }

    private void OnMove(PointerSample s)
    {
        var d = (s.Screen - Input.Start.Screen) / Scale;
        if (d.Length > 14) Press(null);
        // Swiping the dock down puts it away (over apps).
        if (_state == State.Transient && d.Y > 30 && Math.Abs(d.Y) > Math.Abs(d.X)) HideTransient();
    }

    private void OnUp(PointerSample s)
    {
        var pressed = _pressed;
        Press(null);
        if (_state == State.Transient) _idle.Start();
        if (pressed?.Tag is DockItem item && ItemAt(s) == pressed) Launch(item);
    }

    private void Launch(DockItem item)
    {
        TabletLog.Write("dock", "launch " + item.Name + (item.Window != 0 ? " (running)" : ""));
        HideTransient();
        TabletNavigation.LeavingHome();
        if (item.Window != 0 && NavNative.IsWindow(item.Window)) { TabletNavigation.ActivateApp(item.Window); return; }
        try
        {
            if (item.Entry != null) ShellLauncher.Launch(item.Entry);
            else TabletLog.Write("dock", item.Name + " is not installed");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException) { TabletLog.Write("dock", "launch failed: " + ex.Message); }
    }

    /// <summary>Full-screen, nearly clear layer under a transient dock: a tap anywhere else puts the dock away.</summary>
    private sealed class DockCatcher : TabletSurface
    {
        private readonly DockWindow _dock;
        internal DockCatcher(DockWindow dock) : base(transparent: true)
        {
            _dock = dock;
            Content = new Border { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
            Input.Down += _ => _dock.HideTransient();
        }

        internal void Cover()
        {
            var (monitor, _) = TabletNavigation.Screen();
            ShowAt(monitor);
        }
    }
}
