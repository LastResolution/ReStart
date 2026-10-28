// Touch/pen injection for remote tablet-edition tests (C# 5, .NET Framework csc; see rog.sh `touch`).
// Runs in the user's desktop session as a WinExe, so no console window steals the foreground.
//
//   touch.exe <steps-file> [done-file]
//
// One step per line (rog.sh turns ';' into new lines). Coordinates are physical screen pixels.
//   tap x y [ms=60]                     touch down, hold ms, up
//   swipe x1 y1 x2 y2 [ms=300] [hold=0] down, move over ms, keep still for hold ms, up
//   down x y | move x y [ms=200] | hold ms | up      single-contact building blocks
//   pen <tap|swipe|down|move|hold|up> ...            same, with a synthetic pen
//   wait ms                             no contact
//   shot path                           PNG of the primary screen (works mid-gesture)
// The done file receives "ok" or the error, which rog.sh prints.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

internal static class TouchTool
{
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    struct POINTER_INFO
    {
        public uint pointerType, pointerId, frameId, pointerFlags;
        public IntPtr sourceDevice, hwndTarget;
        public POINT ptPixelLocation, ptHimetricLocation, ptPixelLocationRaw, ptHimetricLocationRaw;
        public uint dwTime, historyCount;
        public int InputData;
        public uint dwKeyStates;
        public ulong PerformanceCount;
        public int ButtonChangeType;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct POINTER_TOUCH_INFO
    {
        public POINTER_INFO pointerInfo;
        public uint touchFlags, touchMask;
        public RECT rcContact, rcContactRaw;
        public uint orientation, pressure;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct POINTER_PEN_INFO
    {
        public POINTER_INFO pointerInfo;
        public uint penFlags, penMask, pressure, rotation;
        public int tiltX, tiltY;
    }
    // POINTER_TYPE_INFO with the pen arm of its union; padded to the touch arm's size (152 bytes on x64).
    [StructLayout(LayoutKind.Sequential)]
    struct POINTER_TYPE_INFO_PEN { public uint type; public POINTER_PEN_INFO pen; public long pad0, pad1, pad2; }

    const uint PT_TOUCH = 2, PT_PEN = 3;
    const uint FLAG_INRANGE = 0x2, FLAG_INCONTACT = 0x4, FLAG_FIRSTBUTTON = 0x10, FLAG_DOWN = 0x10000, FLAG_UPDATE = 0x20000, FLAG_UP = 0x40000;

    [DllImport("user32.dll", SetLastError = true)] static extern bool InitializeTouchInjection(uint maxCount, uint mode);
    [DllImport("user32.dll", SetLastError = true)] static extern bool InjectTouchInput(uint count, [In] POINTER_TOUCH_INFO[] contacts);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr CreateSyntheticPointerDevice(uint type, uint maxCount, uint feedback);
    [DllImport("user32.dll", SetLastError = true)] static extern bool InjectSyntheticPointerInput(IntPtr device, [In] POINTER_TYPE_INFO_PEN[] info, uint count);
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);

    static bool _touchReady;
    static IntPtr _pen;
    static bool _penMode, _inContact;
    static int _x, _y;

    static int Main(string[] args)
    {
        string done = args.Length > 1 ? args[1] : null;
        try
        {
            if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware();
            timeBeginPeriod(1);
            foreach (var raw in File.ReadAllLines(args[0]))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                Run(line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            }
            if (_inContact) Up();
            if (done != null) File.WriteAllText(done, "ok");
            return 0;
        }
        catch (Exception ex)
        {
            try { if (_inContact) Up(); } catch (Exception) { }
            if (done != null) File.WriteAllText(done, "error: " + ex.Message);
            return 1;
        }
    }

    static int I(string[] a, int i, int fallback) { return a.Length > i ? int.Parse(a[i]) : fallback; }

    static void Run(string[] a)
    {
        var verb = a[0].ToLowerInvariant();
        if (verb == "pen") { _penMode = true; var rest = new string[a.Length - 1]; Array.Copy(a, 1, rest, 0, rest.Length); Run(rest); _penMode = false; return; }
        switch (verb)
        {
            case "tap": Down(I(a, 1, 0), I(a, 2, 0)); Hold(I(a, 3, 60)); Up(); break;
            case "swipe":
                Down(I(a, 1, 0), I(a, 2, 0)); Move(I(a, 3, 0), I(a, 4, 0), I(a, 5, 300));
                if (I(a, 6, 0) > 0) Hold(I(a, 6, 0));
                Up(); break;
            case "down": Down(I(a, 1, 0), I(a, 2, 0)); break;
            case "move": Move(I(a, 1, 0), I(a, 2, 0), I(a, 3, 200)); break;
            case "hold": Hold(I(a, 1, 100)); break;
            case "up": Up(); break;
            case "wait": Thread.Sleep(I(a, 1, 100)); break;
            case "shot": Shot(string.Join(" ", a, 1, a.Length - 1)); break;
            default: throw new ArgumentException("unknown step: " + string.Join(" ", a));
        }
    }

    static void Down(int x, int y) { _x = x; _y = y; Send(FLAG_DOWN | FLAG_INRANGE | FLAG_INCONTACT, 1); _inContact = true; }
    static void Up() { Send(FLAG_UP | (_penMode ? FLAG_INRANGE : 0), 2); _inContact = false; if (_penMode) Send(FLAG_UPDATE, 0); }

    // Linear motion at ~120 Hz, timed by the clock so the gesture speed is what the step says.
    static void Move(int x, int y, int ms)
    {
        int x0 = _x, y0 = _y;
        var clock = Stopwatch.StartNew();
        while (true)
        {
            var t = ms <= 0 ? 1.0 : Math.Min(1.0, clock.Elapsed.TotalMilliseconds / ms);
            _x = (int)Math.Round(x0 + (x - x0) * t); _y = (int)Math.Round(y0 + (y - y0) * t);
            Send(FLAG_UPDATE | FLAG_INRANGE | FLAG_INCONTACT, 0);
            if (t >= 1.0) break;
            Thread.Sleep(8);
        }
    }

    // A still contact must keep reporting, or the system treats it as lost.
    static void Hold(int ms)
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < ms) { Send(FLAG_UPDATE | FLAG_INRANGE | FLAG_INCONTACT, 0); Thread.Sleep(16); }
    }

    static void Send(uint flags, int buttonChange)
    {
        // Physical pixels only while the thread is per-monitor aware; keep it so (GDI/WinForms calls may change it).
        SetThreadDpiAwarenessContext(new IntPtr(-4));
        if (_penMode) { SendPen(flags, buttonChange); return; }
        if (!_touchReady)
        {
            if (!InitializeTouchInjection(1, 1 /* TOUCH_FEEDBACK_DEFAULT */)) throw new InvalidOperationException("InitializeTouchInjection failed: " + Marshal.GetLastWin32Error());
            _touchReady = true;
        }
        var info = new POINTER_TOUCH_INFO();
        info.pointerInfo.pointerType = PT_TOUCH;
        info.pointerInfo.pointerFlags = flags;
        info.pointerInfo.ptPixelLocation.X = _x; info.pointerInfo.ptPixelLocation.Y = _y;
        info.touchMask = 0x1 | 0x2 | 0x4; // contact area, orientation, pressure
        info.rcContact.Left = _x - 4; info.rcContact.Right = _x + 4; info.rcContact.Top = _y - 4; info.rcContact.Bottom = _y + 4;
        info.orientation = 90; info.pressure = 32000;
        if (!InjectTouchInput(1, new[] { info })) throw new InvalidOperationException("InjectTouchInput failed: " + Marshal.GetLastWin32Error() + " at " + _x + "," + _y);
    }

    static void SendPen(uint flags, int buttonChange)
    {
        if (_pen == IntPtr.Zero)
        {
            _pen = CreateSyntheticPointerDevice(PT_PEN, 1, 1 /* POINTER_FEEDBACK_DEFAULT */);
            if (_pen == IntPtr.Zero) throw new InvalidOperationException("CreateSyntheticPointerDevice failed: " + Marshal.GetLastWin32Error());
        }
        var info = new POINTER_TYPE_INFO_PEN();
        info.type = PT_PEN;
        info.pen.pointerInfo.pointerType = PT_PEN;
        info.pen.pointerInfo.pointerFlags = flags | ((flags & FLAG_INCONTACT) != 0 ? FLAG_FIRSTBUTTON : 0);
        info.pen.pointerInfo.ptPixelLocation.X = _x; info.pen.pointerInfo.ptPixelLocation.Y = _y;
        info.pen.pointerInfo.ButtonChangeType = buttonChange;
        info.pen.penMask = 0x1; info.pen.pressure = (flags & FLAG_INCONTACT) != 0 ? 512u : 0u;
        if (!InjectSyntheticPointerInput(_pen, new[] { info }, 1)) throw new InvalidOperationException("InjectSyntheticPointerInput failed: " + Marshal.GetLastWin32Error());
    }

    static void Shot(string path)
    {
        // Not WinForms' Screen: it switches the thread's DPI awareness and later touches would land at scaled coordinates.
        SetThreadDpiAwarenessContext(new IntPtr(-4));
        var size = new Size(GetSystemMetrics(0), GetSystemMetrics(1));
        using (var bitmap = new Bitmap(size.Width, size.Height))
        {
            using (var g = Graphics.FromImage(bitmap)) g.CopyFromScreen(Point.Empty, Point.Empty, size);
            bitmap.Save(path + ".tmp", ImageFormat.Png);
        }
        if (File.Exists(path)) File.Delete(path);
        File.Move(path + ".tmp", path);
    }
}
