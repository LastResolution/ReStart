using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ReStart.App.Tablet.Design;
using static ReStart.App.Tablet.Gestures.NavNative;

namespace ReStart.App.Tablet.Gestures;

/// <summary>
/// The iPad home indicator: a thin AppBar along the bottom edge (maximized apps end above
/// it) with a centered pill. Gestures, in DIPs on a 13.4" 200% panel (1 DIP ≈ 0.23 mm):
/// short swipe up → dock; long or fast swipe up → Home; swipe up and rest → app switcher;
/// sideways along the bar → previous/next app.
/// </summary>
internal sealed class HomeBar : TabletSurface
{
    internal const double BarHeight = 16;      // DIP
    private const double Slop = 8;              // movement before a gesture picks a direction
    private const double DockRange = 110;       // short swipe: dock
    private const double HomeDistance = 170;    // a swipe this long always goes Home
    private const double HomeVelocity = 0.6;    // DIP/ms upward at release
    private const double HoldDistance = 110;    // the app must be lifted this far before resting counts
    private const double HoldRest = 300;        // ms of (near) stillness that opens the switcher (150 caught slow swipes)

    private readonly Grid _root;
    private readonly Border _pill;
    private readonly SolidColorBrush _background = new(Color.FromArgb(1, 0, 0, 0));
    private readonly SolidColorBrush _pillBrush = new(Color.FromArgb(220, 255, 255, 255));
    private readonly DispatcherTimer _tint, _hold;
    private readonly uint _callbackMessage;
    private bool _registered, _fullscreen, _pillHidden;
    private enum Phase { Idle, Pending, Vertical, Horizontal }
    private Phase _phase;
    private bool _held;
    private nint _app;

    internal HomeBar() : base(transparent: true)
    {
        _pill = new Border
        {
            Width = 128, Height = 4, CornerRadius = new CornerRadius(TabletStyle.ControlRadius), Background = _pillBrush,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = new TranslateTransform(), IsHitTestVisible = false,
        };
        _root = new Grid { Background = _background, Children = { _pill } };
        Content = _root;
        _callbackMessage = RegisterWindowMessage("ReStart.Tablet.HomeBar");
        HwndSource.FromHwnd(Handle)!.AddHook(AppBarHook);
        _tint = new DispatcherTimer(TimeSpan.FromMilliseconds(600), DispatcherPriority.Background, (_, _) => UpdateTint(), Dispatcher);
        _hold = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Input, (_, _) => CheckHold(), Dispatcher);
        Input.Down += OnDown;
        Input.Move += OnMove;
        Input.Up += OnUp;
        Input.Cancel += _ => Abandon();
        TabletNavigation.Watcher.Changed += _ => Dispatcher.BeginInvoke(UpdateTint, DispatcherPriority.Background);
    }

    internal void Enable()
    {
        if (_registered) return;
        var data = AppBar();
        data.CallbackMessage = _callbackMessage;
        _registered = SHAppBarMessage(AbmNew, ref data) != 0;
        Reposition();
        Show();
        Reposition();
        _tint.Start();
        TabletLog.Write("homebar", $"enabled at {Bounds} (appbar {_registered})");
    }

    internal void Disable()
    {
        Input.Abort();
        _tint.Stop();
        if (_registered) { var data = AppBar(); SHAppBarMessage(AbmRemove, ref data); _registered = false; }
        Hide();
    }

    /// <summary>Re-reserves the strip for the current resolution/orientation.</summary>
    internal void Reposition()
    {
        var scale = GetDpiForWindow(Handle) / 96.0;
        var (monitor, _) = TabletNavigation.Screen();
        var height = Math.Round(BarHeight * scale);
        var data = AppBar();
        data.Edge = AbeBottom;
        data.Rc = new((int)monitor.Left, (int)(monitor.Bottom - height), (int)monitor.Right, (int)monitor.Bottom);
        if (_registered)
        {
            SHAppBarMessage(AbmQueryPos, ref data);
            // Keep our height: QUERYPOS may move the rectangle above another bottom bar.
            data.Rc.Top = data.Rc.Bottom - (int)height;
            SHAppBarMessage(AbmSetPos, ref data);
        }
        Place(new Rect(data.Rc.Left, data.Rc.Top, data.Rc.Width, data.Rc.Height));
        TabletNavigation.Dock?.Reposition();
    }

    private AppBarData AppBar() => new() { Size = Marshal.SizeOf<AppBarData>(), Hwnd = Handle };

    private nint AppBarHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (_callbackMessage != 0 && msg == (int)_callbackMessage)
        {
            switch ((int)wParam)
            {
                case AbnPosChanged: Dispatcher.BeginInvoke(Reposition); break;
                case AbnFullScreenApp: _fullscreen = lParam != 0; Dispatcher.BeginInvoke(UpdateTint); break;
            }
            handled = true;
        }
        return 0;
    }

    protected override void OnDisplayChanged()
    {
        base.OnDisplayChanged();
        if (_registered) Reposition();
    }

    /// <summary>Re-evaluates the bar's color now (the shelf came up or went away).</summary>
    internal void Retint() => Dispatcher.BeginInvoke(UpdateTint, DispatcherPriority.Render);

    // Blend with what's above: the bar takes the color of the app's bottom row, and the
    // pill contrasts with it. Over full-screen video it turns clear and the pill fades out.
    // With the shelf up the bar turns clear: the shelf runs behind it, so both read as one band.
    private void UpdateTint()
    {
        if (!IsVisible) return;
        if (TabletNavigation.Dock is { ShelfShown: true } dock)
        {
            _background.Color = Color.FromArgb(1, 0, 0, 0);
            _pillBrush.Color = TabletStyle.WithAlpha(TabletStyle.InkOn(dock.Material), 0xB0);
            return;
        }
        var overlay = TabletNavigation.Switcher.IsShown;
        var clear = _fullscreen || overlay || ForegroundCoversScreen();
        Color below;
        if (clear) below = overlay ? Colors.Black : SampleRow(Bounds.Bottom - Bounds.Height / 2);
        else below = SampleRow(Bounds.Top - 2);
        var luminance = (0.2126 * below.R + 0.7152 * below.G + 0.0722 * below.B) / 255;
        _background.Color = clear ? Color.FromArgb(1, 0, 0, 0) : Color.FromRgb(below.R, below.G, below.B);
        _pillBrush.Color = luminance > 0.6 ? Color.FromArgb(200, 0, 0, 0) : Color.FromArgb(225, 255, 255, 255);
        var hide = clear && !overlay && !Input.Active;
        if (hide != _pillHidden)
        {
            _pillHidden = hide;
            _pill.BeginAnimation(OpacityProperty, new DoubleAnimation(hide ? 0 : 1, TimeSpan.FromMilliseconds(hide ? 900 : 150)) { BeginTime = TimeSpan.FromMilliseconds(hide ? 1800 : 0) });
        }
    }

    private bool ForegroundCoversScreen()
    {
        var app = TabletNavigation.CurrentApp();
        if (app == 0) return false;
        var r = VisibleBounds(app);
        var (monitor, _) = TabletNavigation.Screen();
        return r.Left <= monitor.Left && r.Top <= monitor.Top && r.Right >= monitor.Right && r.Bottom >= monitor.Bottom;
    }

    private Color SampleRow(double y)
    {
        var width = (int)Bounds.Width;
        if (width <= 0) return Colors.Black;
        var screen = GetDC(0);
        var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, width, 1);
        var old = SelectObject(memory, bitmap);
        try
        {
            BitBlt(memory, 0, 0, width, 1, screen, (int)Bounds.Left, (int)y, 0x00CC0020 /* SRCCOPY */);
            SelectObject(memory, old);
            var info = new BitmapInfoHeader { Size = Marshal.SizeOf<BitmapInfoHeader>(), Width = width, Height = 1, Planes = 1, BitCount = 32 };
            var bits = new byte[width * 4];
            if (GetDIBits(memory, bitmap, 0, 1, bits, ref info, 0) == 0) return Colors.Black;
            long r = 0, g = 0, b = 0, n = 0;
            for (var i = 0; i < width; i += 8) { b += bits[i * 4]; g += bits[i * 4 + 1]; r += bits[i * 4 + 2]; n++; }
            return n == 0 ? Colors.Black : Color.FromRgb((byte)(r / n), (byte)(g / n), (byte)(b / n));
        }
        finally { DeleteObject(bitmap); DeleteDC(memory); ReleaseDC(0, screen); }
    }

    // ---- gestures -------------------------------------------------------------------

    private Vector Delta(PointerSample s) => (s.Screen - Input.Start.Screen) / Scale;

    private void OnDown(PointerSample s)
    {
        _phase = Phase.Pending; _held = false;
        _app = TabletNavigation.CurrentApp();
        TabletLog.Write("homebar", $"{s.Kind} down at {s.Screen.X:0},{s.Screen.Y:0}, app {_app:X}");
        _pill.BeginAnimation(OpacityProperty, null); _pill.Opacity = 1;
        _hold.Start();
    }

    private void OnMove(PointerSample s)
    {
        var d = Delta(s);
        var up = -d.Y;
        if (_phase == Phase.Pending)
        {
            if (up > Slop && up > Math.Abs(d.X) * 0.7) { _phase = Phase.Vertical; TabletNavigation.Switcher.BeginGesture(_app); TabletNavigation.Dock.BeginReveal(); }
            else if (Math.Abs(d.X) > Slop * 1.5 && Math.Abs(d.X) > up * 1.4) { _phase = Phase.Horizontal; TabletNavigation.Switcher.BeginQuickSwitch(_app); }
            else return;
        }
        PillFollow(d);
        if (_phase == Phase.Vertical)
        {
            TabletNavigation.Switcher.TrackGesture(d);
            TabletNavigation.Dock.TrackReveal(up);
            if (_held && up < HoldDistance * 0.6) { _held = false; TabletNavigation.Switcher.SetHold(false); }
        }
        else if (_phase == Phase.Horizontal) TabletNavigation.Switcher.TrackQuickSwitch(d.X);
    }

    private void CheckHold()
    {
        if (!Input.Active) { _hold.Stop(); return; }
        if (_phase != Phase.Vertical || _held) return;
        var up = -Delta(Input.Last).Y;
        if (up > HoldDistance && Input.RestingFor(6 * Scale) > HoldRest)
        {
            _held = true;
            TabletNavigation.Switcher.SetHold(true);
            TabletNavigation.Dock.HideTransient();
        }
    }

    private void OnUp(PointerSample s)
    {
        _hold.Stop();
        var d = Delta(s);
        var v = Input.Velocity() / Scale; // DIP per ms
        var up = -d.Y;
        PillReturn();
        switch (_phase)
        {
            case Phase.Vertical:
                var flickUp = -v.Y;
                if (_held && flickUp < HomeVelocity) { TabletNavigation.Dock.HideTransient(); TabletNavigation.Switcher.CommitSwitcher(); }
                else if (up > HomeDistance || flickUp > HomeVelocity && up > 40) { TabletNavigation.Switcher.CommitHome(); }
                else if (up > 18 && flickUp > -0.3 && up <= HomeDistance)
                {
                    TabletNavigation.Switcher.CancelGesture();
                    if (up < DockRange || flickUp < HomeVelocity) TabletNavigation.Dock.ShowTransient();
                }
                else { TabletNavigation.Switcher.CancelGesture(); TabletNavigation.Dock.CancelReveal(); }
                TabletLog.Write("homebar", $"swipe up {up:0} dip, v {flickUp:0.00} dip/ms, held {_held}");
                break;
            case Phase.Horizontal:
                TabletNavigation.Switcher.CommitQuickSwitch(d.X, v.X);
                TabletLog.Write("homebar", $"swipe sideways {d.X:0} dip, v {v.X:0.00} dip/ms");
                break;
        }
        _phase = Phase.Idle;
    }

    private void Abandon()
    {
        _hold.Stop();
        PillReturn();
        if (_phase == Phase.Vertical) { TabletNavigation.Switcher.CancelGesture(); TabletNavigation.Dock.CancelReveal(); }
        else if (_phase == Phase.Horizontal) TabletNavigation.Switcher.CommitQuickSwitch(0, 0);
        _phase = Phase.Idle;
    }

    // The pill leans toward the finger a little, then springs back (visual "grab" feedback).
    private void PillFollow(Vector d)
    {
        var t = (TranslateTransform)_pill.RenderTransform;
        t.BeginAnimation(TranslateTransform.XProperty, null);
        t.BeginAnimation(TranslateTransform.YProperty, null);
        t.X = Math.Clamp(d.X * 0.3, -60, 60);
        t.Y = Math.Clamp(d.Y * 0.04, -3, 0);
    }

    private void PillReturn()
    {
        var t = (TranslateTransform)_pill.RenderTransform;
        t.BeginAnimation(TranslateTransform.XProperty, TabletMotion.To(0, TabletMotion.Enter, TabletMotion.Decelerate));
        t.BeginAnimation(TranslateTransform.YProperty, TabletMotion.To(0, TabletMotion.Short, TabletMotion.Decelerate));
        Dispatcher.BeginInvoke(UpdateTint, DispatcherPriority.Background);
    }
}
