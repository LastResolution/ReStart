using System.Diagnostics;
using System.Windows;
using static ReStart.App.Tablet.Gestures.NavNative;

namespace ReStart.App.Tablet.Gestures;

/// <summary>Input device of a pointer sample.</summary>
internal enum PointerKind { Touch, Pen, Mouse }

/// <summary>One pointer position in physical screen pixels, with a <see cref="Stopwatch"/> timestamp in milliseconds.</summary>
internal readonly record struct PointerSample(Point Screen, double Time, PointerKind Kind);

/// <summary>
/// Raw single-pointer tracking from window messages: WM_POINTER for touch and pen (the
/// contact stays captured by the window it started on, so a swipe can leave it), plain
/// mouse messages with capture for the mouse. Handling the pointer messages also stops
/// Windows from promoting them to mouse input or press-and-hold right-clicks.
/// </summary>
internal sealed class PointerInput
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private uint? _pointer;
    private bool _mouse;
    private PointerKind _mouseKind;
    private readonly List<PointerSample> _history = [];

    internal event Action<PointerSample>? Down, Move, Up, Cancel;
    /// <summary>Whether a contact is in progress.</summary>
    internal bool Active => _pointer != null || _mouse;
    /// <summary>First sample of the current contact.</summary>
    internal PointerSample Start { get; private set; }
    /// <summary>Latest sample of the current contact.</summary>
    internal PointerSample Last { get; private set; }

    internal static double Now => Clock.Elapsed.TotalMilliseconds;

    /// <summary>Velocity in physical pixels per millisecond over the last ~80 ms (zero when the pointer has rested).</summary>
    internal Vector Velocity()
    {
        var now = Now;
        if (_history.Count == 0 || now - _history[^1].Time > 70) return default;
        var newest = _history[^1];
        var oldest = newest;
        for (var i = _history.Count - 1; i >= 0 && newest.Time - _history[i].Time <= 80; i--) oldest = _history[i];
        var dt = newest.Time - oldest.Time;
        return dt < 8 ? default : (newest.Screen - oldest.Screen) / dt;
    }

    /// <summary>Milliseconds since the pointer last moved more than <paramref name="radius"/> pixels.</summary>
    internal double RestingFor(double radius)
    {
        if (_history.Count == 0) return 0;
        var latest = _history[^1].Screen;
        for (var i = _history.Count - 1; i >= 0; i--)
            if ((_history[i].Screen - latest).Length > radius) return Now - _history[i].Time;
        return Now - _history[0].Time;
    }

    /// <summary>Ends the contact as if it were cancelled (e.g. the surface hid).</summary>
    internal void Abort()
    {
        if (!Active) return;
        if (_mouse) ReleaseCapture();
        _pointer = null; _mouse = false;
        Fire(Cancel, Last);
    }

    /// <summary>Logs raw input messages (command <c>nav-trace on|off</c>), for diagnosing injected or real input.</summary>
    internal static bool Trace { get; set; }

    internal nint Process(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (Trace && msg is >= 0x0200 and <= 0x0202 or >= 0x0241 and <= 0x024F or WmCaptureChanged)
        {
            GetPointerType((uint)(wParam.ToInt64() & 0xFFFF), out var t);
            TabletLog.Write("trace", $"{hwnd:X} msg 0x{msg:X4} w 0x{wParam.ToInt64():X} type {t} extra 0x{GetMessageExtraInfo():X8} pointer {_pointer?.ToString() ?? "-"} mouse {_mouse}");
        }
        switch (msg)
        {
            case WmPointerDown or WmPointerUpdate or WmPointerUp or WmPointerCaptureChanged:
            {
                var id = (uint)(wParam.ToInt64() & 0xFFFF);
                GetPointerType(id, out var type);
                if (type == PtMouse) return 0; // mouse arrives as mouse messages
                handled = true;
                var kind = type == PtPen ? PointerKind.Pen : PointerKind.Touch;
                var point = new Point((short)(lParam.ToInt64() & 0xFFFF), (short)((lParam.ToInt64() >> 16) & 0xFFFF));
                if (msg == WmPointerDown)
                {
                    if (Active) return 0; // one contact at a time; extra fingers are ignored
                    _pointer = id;
                    Begin(new(point, Now, kind));
                }
                else if (_pointer == id)
                {
                    if (msg == WmPointerUpdate) Track(new(point, Now, kind));
                    else if (msg == WmPointerUp) { Track(new(point, Now, kind)); _pointer = null; Fire(Up, Last); }
                    else { _pointer = null; Fire(Cancel, Last); }
                }
                return 0;
            }
            // Mouse, and touch/pen that Windows promoted to mouse: once WPF's stylus stack (WISP)
            // has attached to a window, later contacts arrive only this way (WM_POINTERDOWN is
            // never sent), so promoted messages are real input here, not duplicates.
            case WmLButtonDown:
                if (Active) return 0;
                _mouse = true; _mouseKind = KindOfMouseMessage(); SetCapture(hwnd); handled = true;
                Begin(new(Screen(hwnd, lParam), Now, _mouseKind));
                return 0;
            case WmMouseMove:
                if (_mouse) { Track(new(Screen(hwnd, lParam), Now, _mouseKind)); handled = true; }
                return 0;
            case WmLButtonUp:
                if (!_mouse) return 0;
                Track(new(Screen(hwnd, lParam), Now, _mouseKind));
                _mouse = false; ReleaseCapture(); handled = true;
                Fire(Up, Last);
                return 0;
            case WmCaptureChanged:
                if (_mouse && lParam != hwnd) { _mouse = false; Fire(Cancel, Last); }
                return 0;
        }
        return 0;
    }

    private void Begin(PointerSample sample)
    {
        _history.Clear(); _history.Add(sample);
        Start = Last = sample;
        Fire(Down, sample);
    }

    private void Track(PointerSample sample)
    {
        _history.Add(sample);
        if (_history.Count > 256) _history.RemoveRange(0, 128);
        Last = sample;
        Fire(Move, sample);
    }

    // A gesture handler failing must not take the shell down with it.
    private static void Fire(Action<PointerSample>? handler, PointerSample sample) => TabletNavigation.Guard("pointer", () => handler?.Invoke(sample));

    // Mouse messages synthesized from touch/pen carry the MI_WP_SIGNATURE; bit 0x80 marks touch.
    private static PointerKind KindOfMouseMessage()
    {
        var extra = GetMessageExtraInfo();
        if ((extra & 0xFFFFFF00) != 0xFF515700) return PointerKind.Mouse;
        return (extra & 0x80) != 0 ? PointerKind.Touch : PointerKind.Pen;
    }

    // Mouse lParam is client coordinates (signed); convert to screen pixels.
    private static Point Screen(nint hwnd, nint lParam)
    {
        var p = new NPoint { X = (short)(lParam.ToInt64() & 0xFFFF), Y = (short)((lParam.ToInt64() >> 16) & 0xFFFF) };
        ClientToScreen(hwnd, ref p);
        return new(p.X, p.Y);
    }
}
