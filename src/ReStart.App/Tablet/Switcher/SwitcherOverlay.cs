using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ReStart.App.Services;
using ReStart.App.Tablet.Gestures;

namespace ReStart.App.Tablet.Switcher;

/// <summary>
/// Full-screen layer above apps that renders everything the home-bar gestures animate:
/// the current app shrinking under the finger, the app switcher (live cards, flick up to
/// close, tap to open, split button), and the sideways quick switch between apps.
/// Cards are ordered like iPad: the most recent app on the right, older apps to the left.
/// </summary>
internal sealed class SwitcherOverlay : TabletSurface
{
    private enum Mode { Hidden, Gesture, Switcher, QuickSwitch, Leaving }
    private enum Drag { None, Lift, Pan, Dismiss }

    private const double Gap = 44;           // DIP between cards
    private const double CloseDistance = 110; // DIP of upward flick that closes an app
    private const double CloseVelocity = 0.6; // DIP/ms

    private readonly Canvas _cardsLayer = new() { IsHitTestVisible = false };
    private readonly Image _backdrop = new() { Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
    private readonly Border _dim = new() { Background = Brushes.Black, Opacity = 0.25, IsHitTestVisible = false };
    private readonly Border _homeButton;
    private readonly TextBlock _hint;
    private readonly Border _hintBox;
    private readonly List<SwitcherCard> _cards = [];   // slot order: 0 = most recent (rightmost)
    private readonly List<SwitcherCard> _leaving = []; // flicked away, animating out
    private readonly FrameClock _clock;
    private readonly Spring _scroll = new(0, 0.45, 1);
    private readonly Spring _dimSpring = new(0.25, 0.3, 1);
    private readonly Spring _chrome = new(0, 0.3, 1);
    private Mode _mode;
    private Drag _drag;
    private bool _inertia;
    private double _scrollStart;
    private SwitcherCard? _live, _pressed, _splitFirst, _neighbor;
    private bool _pressedSplit, _pressedHome, _hold;
    private nint _origin;
    private Rect _liveFull;
    private Size _screen;
    private string? _wallpaperKey;
    // Quick switch session (repeated sideways swipes walk the same recent order, like iPad).
    private List<AppWindow>? _qsOrder;
    private int _qsIndex, _qsDir;
    private double _qsTime;
    private DispatcherTimer? _pending;

    internal SwitcherOverlay() : base(transparent: false)
    {
        Background = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x18));
        RenderOptions.SetBitmapScalingMode(_backdrop, BitmapScalingMode.HighQuality);
        _homeButton = new Border
        {
            // A square Home tile, like everything else the tablet chrome draws.
            Width = 60, Height = 60, CornerRadius = new CornerRadius(Design.TabletStyle.TileRadius), Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)), BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 28),
            Child = new TextBlock { Text = "", FontFamily = Design.TabletStyle.Icons, FontSize = 24, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            IsHitTestVisible = false,
        };
        _hint = new TextBlock { Foreground = Brushes.White, FontFamily = Design.TabletStyle.Text, FontSize = Design.TabletStyle.Subtitle, TextAlignment = TextAlignment.Center };
        _hintBox = new Border
        {
            CornerRadius = new CornerRadius(Design.TabletStyle.TileRadius), Padding = new Thickness(18, 8, 18, 8), Background = new SolidColorBrush(Color.FromArgb(0x99, 0x1A, 0x1A, 0x1A)),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 48, 0, 0),
            Child = _hint, Visibility = Visibility.Collapsed, IsHitTestVisible = false,
        };
        Content = new Grid { Children = { _backdrop, _dim, _cardsLayer, _homeButton, _hintBox } };
        _clock = new FrameClock(Tick);
        Input.Down += OnDown;
        Input.Move += OnMove;
        Input.Up += OnUp;
        Input.Cancel += _ => { _drag = Drag.None; _pressed = null; };
    }

    /// <summary>Whether the interactive switcher is showing.</summary>
    internal bool IsOpen => _mode == Mode.Switcher;
    /// <summary>Whether a gesture, the switcher or a transition is on screen.</summary>
    internal bool IsBusy => _mode != Mode.Hidden;

    // ---- geometry -------------------------------------------------------------------

    private void Measure()
    {
        var (monitor, _) = TabletNavigation.Screen();
        _screen = new Size(monitor.Width / Scale, monitor.Height / Scale);
        // The Home button sits just above the home bar, wherever the bar is.
        var bar = TabletNavigation.Bar.Bounds;
        _homeBottom = bar.Height > 0 ? (monitor.Bottom - bar.Top) / Scale + 18 : 34;
        _homeButton.Margin = new Thickness(0, 0, 0, _homeBottom);
    }
    private double _homeBottom = 34;

    private bool Portrait => _screen.Height > _screen.Width;
    // Landscape fits three whole cards (the previous app centered); portrait shows one and a half.
    private double CardWidth => Math.Round(Portrait ? _screen.Width * 0.5 : Math.Min(_screen.Width * 0.34, (_screen.Width - 72 - 2 * Gap) / 3));
    private double Step => CardWidth + Gap;
    private double CenterY => _screen.Height * (Portrait ? 0.47 : 0.48) + SwitcherCard.HeaderHeight / 2;

    // Right-to-left: slot 0 sits right of center so the previous app (slot 1) is in the middle.
    private double Anchor => _cards.Count <= 1 ? _screen.Width / 2 : _cards.Count == 2 ? _screen.Width / 2 + Step / 2 : _screen.Width / 2 + Step;
    // Slots are scroll-free; the scroll is added when drawing, so panning moves cards 1:1 with the finger.
    private double SlotX(int index) => Anchor - index * Step;
    private double ScrollOffset => _mode == Mode.Switcher ? _scroll.Value : 0;

    // Leaving the switcher: fold the scroll into the cards so absolute targets (full screen) line up.
    private void BakeScroll()
    {
        var offset = _scroll.Value;
        if (offset == 0) return;
        foreach (var card in _cards.Concat(_leaving)) { card.X.Value += offset; card.X.Target += offset; }
        _scroll.Snap(0); _inertia = false;
    }
    private double MaxScroll => Math.Max(0, (_cards.Count - 1) * Step - (Anchor - CardWidth / 2 - 24));

    private Rect FullRect(nint hwnd)
    {
        var (monitor, _) = TabletNavigation.Screen();
        var r = NavNative.VisibleBounds(hwnd);
        return new Rect((r.Left - monitor.Left) / Scale, (r.Top - monitor.Top) / Scale, r.Width / Scale, r.Height / Scale);
    }

    private Rect WorkRect()
    {
        var (monitor, work) = TabletNavigation.Screen();
        return new Rect((work.Left - monitor.Left) / Scale, (work.Top - monitor.Top) / Scale, work.Width / Scale, work.Height / Scale);
    }

    private static void SetRect(SwitcherCard card, Rect r, bool snap)
    {
        if (snap) { card.X.Snap(r.X + r.Width / 2); card.Y.Snap(r.Y + r.Height / 2); card.W.Snap(r.Width); }
        else { card.X.Target = r.X + r.Width / 2; card.Y.Target = r.Y + r.Height / 2; card.W.Target = r.Width; }
    }

    private void Slot(SwitcherCard card, int index)
    {
        card.X.Target = SlotX(index);
        card.Y.Target = CenterY;
        card.W.Target = CardWidth;
    }

    private void Reslot() { for (var i = 0; i < _cards.Count; i++) if (_cards[i] != _live || _mode == Mode.Switcher) Slot(_cards[i], i); }

    // ---- lifecycle ------------------------------------------------------------------

    private void Present()
    {
        CancelPending();
        Measure();
        LoadBackdrop();
        if (!IsVisible)
        {
            var (monitor, _) = TabletNavigation.Screen();
            ShowAt(monitor);
        }
        else Raise();
        TabletNavigation.RaiseChrome();
        _clock.Start();
    }

    private SwitcherCard AddCard(AppWindow window, int index)
    {
        var aspect = WorkRect() is var work && work.Height > 0 ? work.Width / work.Height : 1.6;
        var card = new SwitcherCard(window, Handle, aspect);
        _cardsLayer.Children.Add(card.Root);
        _cards.Insert(Math.Min(index, _cards.Count), card);
        return card;
    }

    private void ClearCards()
    {
        foreach (var card in _cards.Concat(_leaving)) card.Dispose();
        _cards.Clear(); _leaving.Clear();
        _cardsLayer.Children.Clear();
        _live = _neighbor = _pressed = _splitFirst = null;
    }

    /// <summary>Hides immediately (posture/display change, CloseOverlays).</summary>
    internal void Dismiss(bool immediately = false)
    {
        if (_mode == Mode.Hidden) return;
        if (immediately || _mode != Mode.Switcher) HideNow();
        else Back();
    }

    private void HideNow()
    {
        CancelPending();
        _mode = Mode.Hidden;
        _clock.Stop();
        Input.Abort();
        ClearCards();
        _hintBox.Visibility = Visibility.Collapsed;
        _homeButton.Opacity = 0;
        Hide();
        TabletNavigation.Dock.Refresh();
    }

    private void After(int milliseconds, Action action)
    {
        CancelPending();
        _pending = new DispatcherTimer(TimeSpan.FromMilliseconds(milliseconds), DispatcherPriority.Normal, (_, _) => { CancelPending(); action(); }, Dispatcher);
        _pending.Start();
    }

    private void CancelPending() { _pending?.Stop(); _pending = null; }

    private void LoadBackdrop()
    {
        try
        {
            var (monitor, _) = TabletNavigation.Screen();
            var path = DesktopWallpaperSource.CurrentPath(new System.Drawing.Rectangle((int)monitor.X, (int)monitor.Y, (int)monitor.Width, (int)monitor.Height));
            if (path == null || !File.Exists(path)) { _backdrop.Source = null; return; }
            var key = path + File.GetLastWriteTimeUtc(path).Ticks;
            if (key == _wallpaperKey) return;
            // Decoding tiny and stretching back up is a free, smooth blur.
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.DecodePixelWidth = 48; image.UriSource = new Uri(path); image.EndInit(); image.Freeze();
            _backdrop.Source = image; _wallpaperKey = key;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FileFormatException)
        { _backdrop.Source = null; }
    }

    // ---- frame loop -----------------------------------------------------------------

    private bool Tick(double dt)
    {
        if (_inertia)
        {
            // Free scrolling decelerates like a UIScrollView, then rubber-bands at the ends.
            var velocity = _scroll.Velocity;
            if (_scroll.Value < 0 || _scroll.Value > MaxScroll)
            {
                _inertia = false;
                _scroll.Target = Math.Clamp(_scroll.Value, 0, MaxScroll);
            }
            else
            {
                _scroll.Value += velocity * dt;
                _scroll.Velocity = velocity * Math.Pow(0.06, dt);
                _scroll.Target = _scroll.Value;
                if (Math.Abs(_scroll.Velocity) < 8) { _inertia = false; _scroll.Velocity = 0; }
            }
        }
        else _scroll.Step(dt);
        if (_mode == Mode.Switcher) Reslot();
        _dimSpring.Step(dt); _chrome.Step(dt);
        _dim.Opacity = _dimSpring.Value;
        _homeButton.Opacity = Math.Clamp(_chrome.Value, 0, 1);
        var busy = _inertia || !_scroll.Settled || !_dimSpring.Settled || !_chrome.Settled || Input.Active;
        var offset = ScrollOffset;
        foreach (var card in _cards) { busy |= card.Step(dt); card.Render(Scale, offset); }
        for (var i = _leaving.Count - 1; i >= 0; i--)
        {
            var card = _leaving[i];
            busy |= card.Step(dt); card.Render(Scale, offset);
            if (card.Alpha.Value <= 0.01 || card.Frame.Bottom < -40) { card.Thumb?.Hide(); }
        }
        return busy || _mode is Mode.Gesture or Mode.QuickSwitch or Mode.Leaving;
    }

    // ---- home-bar gesture (vertical) --------------------------------------------------

    /// <summary>A swipe up started on the home bar; <paramref name="app"/> is the app in front (0 on Home).</summary>
    internal void BeginGesture(nint app)
    {
        if (_mode == Mode.Switcher) return; // a swipe up over the switcher means Home, decided on release
        HideNow();
        _mode = Mode.Gesture; _hold = false;
        _origin = app;
        _dimSpring.Snap(0); _chrome.Snap(0);
        if (app == 0 || AppWindows.Describe(app) is not { } window || window.IsMinimized) return; // on Home: nothing moves until the hold
        Measure();
        _live = AddCard(window, 0);
        _liveFull = FullRect(app);
        SetRect(_live, _liveFull, snap: true);
        _live.Alpha.Snap(1); _live.Chrome.Snap(0); _live.ShowSplit = false;
        Present();
        _live.Render(Scale, 0);
    }

    /// <summary>Finger moved by <paramref name="d"/> DIP from where it touched the bar.</summary>
    internal void TrackGesture(Vector d)
    {
        if (_mode == Mode.Switcher) return;
        if (_mode != Mode.Gesture || _live == null) return;
        var up = Math.Max(0, -d.Y);
        // Shrinks quickly at first, then ever more slowly (rubber-band feel), never below ~30%.
        var progress = 1 - Math.Exp(-2.4 * up / _screen.Height);
        var width = _liveFull.Width * (1 - 0.7 * progress);
        if (_hold) width = Math.Max(CardWidth * 1.04, width);
        var height = width / _live.Aspect;
        var bottom = _liveFull.Bottom - up * 1.05;
        _live.X.Set(_liveFull.X + _liveFull.Width / 2 + d.X * 0.9);
        _live.Y.Set(bottom - height / 2);
        if (_hold) _live.W.Target = width; else _live.W.Set(width);
        _dimSpring.Target = Math.Min(0.3, up / 400);
        _clock.Start();
    }

    /// <summary>The finger rested mid-swipe: other apps slide in (switcher preview), or slide away again.</summary>
    internal void SetHold(bool hold)
    {
        if (_mode != Mode.Gesture || _hold == hold) return;
        _hold = hold;
        if (hold)
        {
            Measure();
            var start = _live == null ? 0 : 1;
            foreach (var window in TabletNavigation.Recent().Where(w => _live == null || w.Handle != _live.Window.Handle).Take(12))
            {
                var card = AddCard(window, _cards.Count);
                var index = _cards.Count - 1;
                card.X.Snap(SlotX(index) - 80); card.Y.Snap(CenterY); card.W.Snap(CardWidth * 0.92);
                card.Alpha.Snap(0); card.Alpha.Target = 1; card.Chrome.Target = 1;
                Slot(card, index);
            }
            _live?.Chrome.Target = 1;
            if (_live == null && _cards.Count > 0) Present();
            _dimSpring.Target = 0.3;
        }
        else
        {
            foreach (var card in _cards.Where(c => c != _live)) card.Alpha.Target = 0;
            _live?.Chrome.Target = 0;
        }
        _clock.Start();
    }

    /// <summary>Released after resting: the switcher stays open.</summary>
    internal void CommitSwitcher()
    {
        if (_mode == Mode.Switcher) return;
        if (!_hold) SetHold(true);
        if (_cards.Count == 0) { HideNow(); return; }
        EnterSwitcher();
        TabletLog.Write("switcher", $"opened from gesture, {_cards.Count} app(s)");
    }

    /// <summary>Released after a long or fast swipe: the app shrinks away and Home appears.</summary>
    internal void CommitHome()
    {
        if (_mode == Mode.Switcher) { LeaveHome(); return; }
        if (_live == null) { HideNow(); TabletNavigation.GoHome(); return; }
        _mode = Mode.Leaving;
        foreach (var card in _cards.Where(c => c != _live)) card.Alpha.Target = 0;
        _live.X.Response = _live.Y.Response = _live.W.Response = 0.3;
        _live.X.Target = _screen.Width / 2; _live.Y.Target = _screen.Height * 0.62;
        _live.W.Target = _liveFull.Width * 0.16;
        _live.Alpha.Target = 0;
        _clock.Start();
        After(230, () => { TabletNavigation.GoHome(); HideNow(); });
        TabletLog.Write("switcher", "home");
    }

    /// <summary>Released early (dock range) or cancelled: the app springs back to full size.</summary>
    internal void CancelGesture()
    {
        if (_mode == Mode.Switcher) return;
        if (_live == null) { HideNow(); return; }
        _mode = Mode.Leaving;
        foreach (var card in _cards.Where(c => c != _live)) card.Alpha.Target = 0;
        SetRect(_live, _liveFull, snap: false);
        _live.Chrome.Target = 0;
        _dimSpring.Target = 0;
        _clock.Start();
        After(300, HideNow);
    }

    // ---- switcher ---------------------------------------------------------------------

    /// <summary>Opens the switcher directly (command, side button): cards zoom out from the current app.</summary>
    internal void Open()
    {
        if (_mode == Mode.Switcher) return;
        HideNow();
        _origin = TabletNavigation.CurrentApp();
        Measure();
        var recent = TabletNavigation.Recent().Take(12).ToList();
        if (recent.Count == 0) { TabletLog.Write("switcher", "no apps"); return; }
        _scroll.Snap(0);
        foreach (var window in recent)
        {
            var card = AddCard(window, _cards.Count);
            var index = _cards.Count - 1;
            if (window.Handle == _origin && !window.IsMinimized) { SetRect(card, FullRect(window.Handle), snap: true); card.Alpha.Snap(1); }
            else { card.X.Snap(SlotX(index)); card.Y.Snap(CenterY + 30); card.W.Snap(CardWidth * 0.9); card.Alpha.Snap(0); }
        }
        _dimSpring.Snap(0);
        _mode = Mode.Gesture; // so Present renders before the switcher springs start
        Present();
        EnterSwitcher();
        TabletLog.Write("switcher", $"opened, {_cards.Count} app(s)");
    }

    private void EnterSwitcher()
    {
        _mode = Mode.Switcher;
        _live = null; _splitFirst = null;
        foreach (var card in _cards) { card.Alpha.Target = 1; card.Chrome.Target = 1; card.ShowSplit = true; card.Highlighted = false; }
        _dimSpring.Target = 0.3;
        _chrome.Target = 1;
        _hintBox.Visibility = Visibility.Collapsed;
        Reslot();
        _clock.Start();
        TabletNavigation.Bar.Raise();
    }

    private (SwitcherCard? Card, bool Split) HitTest(Point p)
    {
        foreach (var card in _cards)
        {
            if (card.CanSplit && card.SplitButton.Contains(p)) return (card, true);
            if (card.HitArea.Contains(p)) return (card, false);
        }
        return (null, false);
    }

    private Point Dip(PointerSample s) => new((s.Screen.X - Bounds.X) / Scale, (s.Screen.Y - Bounds.Y) / Scale);

    private void OnDown(PointerSample s)
    {
        if (_mode != Mode.Switcher) return;
        var p = Dip(s);
        (_pressed, _pressedSplit) = HitTest(p);
        _pressedHome = new Rect(_screen.Width / 2 - 40, _screen.Height - _homeBottom - 64 - 8, 80, 80).Contains(p);
        _drag = Drag.None;
        _inertia = false;
        _scroll.Set(_scroll.Value);
        _scrollStart = _scroll.Value;
        _clock.Start();
    }

    private void OnMove(PointerSample s)
    {
        if (_mode != Mode.Switcher) return;
        var d = (s.Screen - Input.Start.Screen) / Scale;
        if (_drag == Drag.None)
        {
            if (d.Length < 10) return;
            if (_pressed != null && -d.Y > Math.Abs(d.X)) _drag = Drag.Lift;
            else if (Math.Abs(d.X) >= Math.Abs(d.Y)) _drag = Drag.Pan;
            else if (d.Y > 0) _drag = Drag.Dismiss;
            else _drag = Drag.Pan;
        }
        switch (_drag)
        {
            case Drag.Lift:
                _pressed!.Lift.Set(d.Y < 0 ? d.Y : d.Y * 0.25);
                break;
            case Drag.Pan:
                var value = _scrollStart + d.X;
                // Rubber band past either end.
                if (value < 0) value *= 0.35;
                else if (value > MaxScroll) value = MaxScroll + (value - MaxScroll) * 0.35;
                _scroll.Set(value);
                break;
            case Drag.Dismiss:
                foreach (var card in _cards) card.Lift.Set(d.Y * 0.3);
                _dimSpring.Set(Math.Max(0.05, 0.3 - d.Y / 600));
                break;
        }
    }

    private void OnUp(PointerSample s)
    {
        if (_mode != Mode.Switcher) return;
        var d = (s.Screen - Input.Start.Screen) / Scale;
        var v = Input.Velocity() / Scale;
        var drag = _drag; _drag = Drag.None;
        var pressed = _pressed; _pressed = null;
        switch (drag)
        {
            case Drag.None:
                if (_pressedHome && pressed == null) LeaveHome();
                else if (pressed != null && _pressedSplit) BeginSplitPick(pressed);
                else if (pressed != null) Choose(pressed);
                else if (_splitFirst != null) CancelSplitPick();
                else Back();
                break;
            case Drag.Lift:
                if (d.Y < -CloseDistance || v.Y < -CloseVelocity) CloseCard(pressed!, v.Y);
                else { pressed!.Lift.Target = 0; pressed.Lift.Velocity = v.Y * 1000; }
                break;
            case Drag.Pan:
                if (_scroll.Value < 0 || _scroll.Value > MaxScroll) { _scroll.Target = Math.Clamp(_scroll.Value, 0, MaxScroll); _scroll.Velocity = v.X * 1000; }
                else { _scroll.Velocity = v.X * 1000; _inertia = Math.Abs(_scroll.Velocity) > 40; }
                break;
            case Drag.Dismiss:
                if (d.Y > 70 || v.Y > 0.5) Back();
                else { foreach (var card in _cards) card.Lift.Target = 0; _dimSpring.Target = 0.3; }
                break;
        }
        _clock.Start();
    }

    private void Choose(SwitcherCard card)
    {
        if (_splitFirst != null)
        {
            if (card == _splitFirst) { CancelSplitPick(); return; }
            if (!card.CanSplit) { _hint.Text = AppInfo.Name(card.Window) + " can't be resized for Split View"; return; }
            var first = _splitFirst.Window.Handle;
            TabletLog.Write("switcher", $"split {AppInfo.Name(_splitFirst.Window)} + {AppInfo.Name(card.Window)}");
            ZoomOut(() => TabletNavigation.Split.Start(first, card.Window.Handle));
            return;
        }
        Open(card);
    }

    /// <summary>The card grows back to full screen, then the real window takes its place.</summary>
    private void Open(SwitcherCard card)
    {
        BakeScroll();
        _mode = Mode.Leaving;
        _chrome.Target = 0;
        foreach (var other in _cards.Where(c => c != card)) other.Alpha.Target = 0;
        card.Chrome.Target = 0;
        var target = card.Window.IsMinimized || AppWindows.CanMaximize(card.Window.Handle) ? WorkRect() : FullRect(card.Window.Handle);
        foreach (var spring in new[] { card.X, card.Y, card.W }) spring.Response = 0.32;
        SetRect(card, target, snap: false);
        _clock.Start();
        var hwnd = card.Window.Handle;
        After(240, () =>
        {
            TabletNavigation.ActivateApp(hwnd);
            After(90, HideNow); // let the real window paint before the card disappears
        });
        TabletLog.Write("switcher", "open " + AppInfo.Name(card.Window));
    }

    /// <summary>Tap on empty space or swipe down: back to the app that was in front.</summary>
    private void Back()
    {
        var origin = _cards.FirstOrDefault(c => c.Window.Handle == _origin);
        if (origin != null) Open(origin);
        else LeaveHome();
    }

    private void LeaveHome() => ZoomOut(TabletNavigation.GoHome);

    private void ZoomOut(Action then)
    {
        BakeScroll();
        _mode = Mode.Leaving;
        _chrome.Target = 0;
        foreach (var card in _cards) { card.Alpha.Target = 0; card.W.Target = card.W.Value * 0.86; }
        _clock.Start();
        After(200, () => { then(); HideNow(); });
    }

    private void CloseCard(SwitcherCard card, double velocity)
    {
        var index = _cards.IndexOf(card);
        if (index < 0) return;
        card.Closing = true;
        card.Lift.Response = 0.5; card.Lift.Damping = 1;
        card.Lift.Target = -_screen.Height; card.Lift.Velocity = Math.Min(velocity * 1000, -1500);
        card.Alpha.Target = 0;
        _cards.RemoveAt(index); _leaving.Add(card);
        if (_scroll.Value > MaxScroll) _scroll.Target = MaxScroll;
        var hwnd = card.Window.Handle;
        TabletLog.Write("switcher", "close " + AppInfo.Name(card.Window));
        AppWindows.Close(hwnd);
        _clock.Start();
        if (_cards.Count == 0) After(350, LeaveHome);
        // Apps may ask to save (show their dialog right away) or refuse (bring the card back).
        var checks = 0;
        var check = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromMilliseconds(200) };
        check.Tick += (_, _) =>
        {
            var alive = NavNative.IsWindow(hwnd) && NavNative.IsWindowVisible(hwnd) && !AppWindows.IsCloaked(hwnd);
            var popup = alive ? NavNative.GetLastActivePopup(hwnd) : 0;
            var asking = popup != hwnd && popup != 0 && NavNative.IsWindowVisible(popup);
            if (alive && !asking && ++checks < 8) return;
            check.Stop();
            _leaving.Remove(card);
            _cardsLayer.Children.Remove(card.Root);
            card.Dispose();
            if (!alive) return;
            if (asking)
            {
                TabletLog.Write("switcher", "close asked a question; showing the app");
                HideNow();
                TabletNavigation.ActivateApp(hwnd);
                AppWindows.SetForeground(popup);
                return;
            }
            if (_mode != Mode.Switcher || AppWindows.Describe(hwnd) is not { } window) return;
            TabletLog.Write("switcher", "app did not close; card restored");
            var restored = AddCard(window, Math.Min(index, _cards.Count));
            restored.X.Snap(SlotX(Math.Min(index, _cards.Count - 1))); restored.Y.Snap(CenterY - _screen.Height * 0.6); restored.W.Snap(CardWidth);
            restored.Alpha.Snap(0); restored.Alpha.Target = 1; restored.Chrome.Snap(1); restored.ShowSplit = true;
            CancelPending();
            _clock.Start();
        };
        check.Start();
    }

    private void BeginSplitPick(SwitcherCard card)
    {
        if (!card.CanSplit) return;
        if (_cards.Count < 2) { ShowHint("Open another app to use Split View"); return; }
        _splitFirst = card;
        foreach (var c in _cards) { c.Highlighted = c == card; c.ShowSplit = false; }
        ShowHint("Choose an app to show next to " + AppInfo.Name(card.Window));
        TabletLog.Write("switcher", "split pick " + AppInfo.Name(card.Window));
    }

    private void CancelSplitPick()
    {
        _splitFirst = null;
        foreach (var c in _cards) { c.Highlighted = false; c.ShowSplit = true; }
        _hintBox.Visibility = Visibility.Collapsed;
    }

    private void ShowHint(string text) { _hint.Text = text; _hintBox.Visibility = Visibility.Visible; }

    // ---- quick switch (sideways along the bar) ------------------------------------------

    internal void BeginQuickSwitch(nint app)
    {
        if (_mode == Mode.Switcher) return;
        HideNow();
        Measure();
        // Keep walking the same order while switches follow each other (like iPad); otherwise start fresh.
        var now = PointerInput.Now;
        var fresh = _qsOrder == null || now - _qsTime > 4000 || (app != 0 && !NavNative.IsWindow(app))
            || (_qsIndex < 0 ? app != 0 : _qsIndex >= _qsOrder.Count || _qsOrder[_qsIndex].Handle != app);
        if (fresh)
        {
            _qsOrder = TabletNavigation.Recent();
            _qsIndex = app == 0 ? -1 : _qsOrder.FindIndex(w => w.Handle == app);
            if (app != 0 && _qsIndex < 0) { _qsOrder.Insert(0, AppWindows.Describe(app)!); _qsIndex = 0; }
        }
        _mode = Mode.QuickSwitch;
        _qsDir = 0; _neighbor = null; _live = null;
        _origin = app;
        _dimSpring.Snap(0); _chrome.Snap(0);
        if (_qsIndex >= 0 && !_qsOrder![_qsIndex].IsMinimized)
        {
            _live = AddCard(_qsOrder[_qsIndex], 0);
            _liveFull = FullRect(_live.Window.Handle);
            SetRect(_live, _liveFull, snap: true);
            _live.Alpha.Snap(1);
        }
        else _liveFull = WorkRect();
    }

    internal void TrackQuickSwitch(double dx)
    {
        if (_mode != Mode.QuickSwitch || _qsOrder == null) return;
        var dir = dx > 0 ? 1 : -1; // finger moving right reveals the previous (older) app from the left
        var index = _qsIndex + dir;
        if (dir != _qsDir)
        {
            _qsDir = dir;
            if (_neighbor != null) { _cards.Remove(_neighbor); _cardsLayer.Children.Remove(_neighbor.Root); _neighbor.Dispose(); _neighbor = null; }
            if (index >= 0 && index < _qsOrder.Count && NavNative.IsWindow(_qsOrder[index].Handle))
            {
                _neighbor = AddCard(_qsOrder[index], _cards.Count);
                _neighbor.Alpha.Snap(1);
            }
            if (_neighbor != null || _live != null) Present();
        }
        var travel = _neighbor == null ? dx * 0.2 : dx;
        var shrink = 1 - 0.07 * Math.Min(1, Math.Abs(travel) / 220);
        var work = WorkRect();
        if (_live != null) { _live.X.Set(_liveFull.X + _liveFull.Width / 2 + travel); _live.Y.Set(_liveFull.Y + _liveFull.Height / 2); _live.W.Set(_liveFull.Width * shrink); }
        if (_neighbor != null)
        {
            _neighbor.X.Set(work.X + work.Width / 2 - dir * (_screen.Width + 24) + travel);
            _neighbor.Y.Set(work.Y + work.Height / 2);
            _neighbor.W.Set(work.Width * shrink);
        }
        _clock.Start();
    }

    internal void CommitQuickSwitch(double dx, double vx)
    {
        if (_mode != Mode.QuickSwitch) return;
        var work = WorkRect();
        var go = _neighbor != null && Math.Sign(dx) == _qsDir && (Math.Abs(dx) > _screen.Width * 0.18 || vx * _qsDir > 0.45);
        _mode = Mode.Leaving;
        if (!go)
        {
            if (_live != null) SetRect(_live, _liveFull, snap: false);
            if (_neighbor != null) { _neighbor.X.Target = work.X + work.Width / 2 - _qsDir * (_screen.Width + 24); _neighbor.W.Target = work.Width; }
            _clock.Start();
            After(260, HideNow);
            return;
        }
        var target = _neighbor!;
        foreach (var spring in new[] { target.X, target.W }) { spring.Response = 0.3; spring.Damping = 1; }
        SetRect(target, work, snap: false);
        if (_live != null) { _live.X.Response = 0.3; _live.X.Damping = 1; _live.X.Target = _liveFull.X + _liveFull.Width / 2 + _qsDir * (_screen.Width + 24); _live.W.Target = _liveFull.Width; }
        _qsIndex += _qsDir;
        _qsTime = PointerInput.Now;
        _clock.Start();
        var hwnd = target.Window.Handle;
        After(200, () => { TabletNavigation.ActivateApp(hwnd, keepSplit: true); After(80, HideNow); });
        TabletLog.Write("switcher", $"quick switch {(_qsDir > 0 ? "back" : "forward")} to {AppInfo.Name(target.Window)}");
    }

    /// <summary>Command-driven quick switch (dir 1 = previous app, -1 = next).</summary>
    internal void QuickSwitchNow(int dir)
    {
        BeginQuickSwitch(TabletNavigation.CurrentApp());
        TrackQuickSwitch(dir * 1);
        CommitQuickSwitch(dir * _screen.Width, dir);
    }
}
