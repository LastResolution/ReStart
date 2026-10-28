using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using static ReStart.App.Tablet.Gestures.NavNative;

namespace ReStart.App.Tablet.Gestures;

/// <summary>
/// Base for the navigation surfaces: a borderless, topmost tool window that never takes
/// the foreground (the app underneath keeps focus, like iPad system UI) and receives raw
/// touch/pen/mouse through <see cref="Input"/>. Placed in physical pixels.
/// </summary>
internal class TabletSurface : Window
{
    /// <summary>Raw pointer stream of this window.</summary>
    internal PointerInput Input { get; } = new();
    /// <summary>Native handle (valid after construction).</summary>
    internal nint Handle { get; }
    /// <summary>Physical pixels per DIP.</summary>
    internal double Scale { get; private set; } = 2;
    /// <summary>Last placement in physical screen pixels.</summary>
    internal Rect Bounds { get; private set; }

    internal TabletSurface(bool transparent)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        UseLayoutRounding = true;
        AllowsTransparency = transparent;
        Background = transparent ? Brushes.Transparent : Brushes.Black;
        Focusable = false;
        // WPF's own touch affordances (press-and-hold ring, flicks) fight custom gestures.
        Stylus.SetIsPressAndHoldEnabled(this, false);
        Stylus.SetIsFlicksEnabled(this, false);
        Stylus.SetIsTapFeedbackEnabled(this, false);
        Stylus.SetIsTouchFeedbackEnabled(this, false);
        Width = 1; Height = 1; Left = -32000; Top = -32000;
        Handle = new WindowInteropHelper(this).EnsureHandle();
        var ex = GetWindowLongPtr(Handle, GwlExStyle).ToInt64() | WsExNoActivate | WsExToolWindow;
        SetWindowLongPtr(Handle, GwlExStyle, (nint)ex);
        // TABLET_DISABLE_PRESSANDHOLD | PENTAPFEEDBACK | PENBARRELFEEDBACK | FLICKS | SMOOTHSCROLLING | FLICKFALLBACKKEYS
        SetProp(Handle, "MicrosoftTabletPenServiceProperty", 0x1 | 0x8 | 0x10 | 0x10000 | 0x80000 | 0x100000);
        var off = 0;
        foreach (var feedback in new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11 }) SetWindowFeedbackSetting(Handle, feedback, 0, sizeof(int), ref off);
        var source = HwndSource.FromHwnd(Handle)!;
        source.AddHook(Hook);
        Scale = GetDpiForWindow(Handle) / 96.0;
    }

    private nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmMouseActivate) { handled = true; return MaNoActivate; }
        if (msg == 0x024B /* WM_POINTERACTIVATE */) { handled = true; return 3; /* PA_NOACTIVATE */ }
        if (msg == WmDpiChanged) { Scale = (wParam.ToInt64() & 0xFFFF) / 96.0; Dispatcher.BeginInvoke(OnDisplayChanged); }
        if (msg == WmDisplayChange) Dispatcher.BeginInvoke(OnDisplayChanged);
        return Input.Process(hwnd, msg, wParam, lParam, ref handled);
    }

    /// <summary>Called after a resolution, rotation or DPI change.</summary>
    protected virtual void OnDisplayChanged() => Scale = GetDpiForWindow(Handle) / 96.0;

    /// <summary>Moves/resizes in physical pixels, optionally re-asserting topmost.</summary>
    internal void Place(Rect bounds, bool raise = true)
    {
        Bounds = bounds;
        SetWindowPos(Handle, raise ? HwndTopmost : 0, (int)Math.Round(bounds.X), (int)Math.Round(bounds.Y),
            Math.Max(1, (int)Math.Round(bounds.Width)), Math.Max(1, (int)Math.Round(bounds.Height)),
            SwpNoActivate | SwpNoOwnerZOrder | (raise ? 0 : SwpNoZOrder));
    }

    /// <summary>Moves without resizing (cheap: no re-layout or re-render).</summary>
    internal void MoveTo(double x, double y)
    {
        Bounds = new Rect(x, y, Bounds.Width, Bounds.Height);
        SetWindowPos(Handle, 0, (int)Math.Round(x), (int)Math.Round(y), 0, 0, SwpNoActivate | SwpNoZOrder | SwpNoSize | SwpNoOwnerZOrder);
    }

    /// <summary>Re-asserts this window at the top of the topmost band.</summary>
    internal void Raise() => SetWindowPos(Handle, HwndTopmost, 0, 0, 0, 0, SwpNoActivate | SwpNoMove | SwpNoSize | SwpNoOwnerZOrder);

    /// <summary>Shows without activation at <paramref name="bounds"/> (also brings a parked surface back).</summary>
    internal void ShowAt(Rect bounds)
    {
        Place(bounds);
        if (!IsVisible) Show();
        Place(bounds);
        Unpark();
    }

    private bool _parked;

    /// <summary>Visible to the user: shown and not parked.</summary>
    internal bool IsShown => IsVisible && !_parked;

    /// <summary>
    /// Takes the surface away without hiding it: cloaked by DWM and moved off-screen. Hiding a
    /// WPF window releases its swap chain, and the GPU driver (seen with AMD's) keeps a thread
    /// for every one created afterwards, so surfaces that come and go often were leaking a
    /// thread per appearance. Parked surfaces keep theirs and cost nothing while idle.
    /// </summary>
    internal void Park()
    {
        if (!IsVisible || _parked) return;
        _parked = true;
        Input.Abort();
        SetCloak(true);
        SetWindowPos(Handle, 0, -32000, -32000, 0, 0, SwpNoActivate | SwpNoZOrder | SwpNoSize | SwpNoOwnerZOrder);
        Bounds = new Rect(-32000, -32000, Bounds.Width, Bounds.Height);
    }

    private void Unpark()
    {
        if (!_parked) return;
        _parked = false;
        SetCloak(false);
    }

    private void SetCloak(bool cloak)
    {
        var value = cloak ? 1 : 0;
        DwmSetWindowAttribute(Handle, 13 /* DWMWA_CLOAK */, ref value, sizeof(int));
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
