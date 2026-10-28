using System.Runtime.InteropServices;

namespace ReStart.App.Tablet.Gestures;

/// <summary>Win32/DWM interop shared by the tablet navigation surfaces (home bar, switcher, dock, Split View).</summary>
internal static class NavNative
{
    internal const int GwlExStyle = -20;
    internal const long WsExNoActivate = 0x08000000, WsExToolWindow = 0x80, WsExTopmost = 0x8, WsExLayered = 0x80000;
    internal const uint SwpNoSize = 0x1, SwpNoMove = 0x2, SwpNoZOrder = 0x4, SwpNoActivate = 0x10, SwpShowWindow = 0x40, SwpHideWindow = 0x80, SwpNoOwnerZOrder = 0x200, SwpFrameChanged = 0x20;
    internal static readonly nint HwndTopmost = -1, HwndNoTopmost = -2, HwndTop = 0, HwndBottom = 1;
    internal const int SwHide = 0, SwShowNoActivate = 4, SwRestore = 9, SwMinimize = 6, SwShowNa = 8, SwMaximize = 3;

    internal const int WmMouseActivate = 0x0021, MaNoActivate = 3;
    internal const int WmPointerUpdate = 0x0245, WmPointerDown = 0x0246, WmPointerUp = 0x0247, WmPointerCaptureChanged = 0x024C;
    internal const int WmMouseMove = 0x0200, WmLButtonDown = 0x0201, WmLButtonUp = 0x0202, WmCaptureChanged = 0x0215;
    internal const int WmDisplayChange = 0x007E, WmDpiChanged = 0x02E0, WmSettingChange = 0x001A;
    internal const uint PtTouch = 2, PtPen = 3, PtMouse = 4;

    [StructLayout(LayoutKind.Sequential)] internal struct NPoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct NRect
    {
        public int Left, Top, Right, Bottom;
        public NRect(int left, int top, int right, int bottom) { Left = left; Top = top; Right = right; Bottom = bottom; }
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
        public override readonly string ToString() => $"{Left},{Top} {Width}x{Height}";
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PointerInfo
    {
        public uint PointerType, PointerId, FrameId, PointerFlags;
        public nint SourceDevice, HwndTarget;
        public NPoint PixelLocation, HimetricLocation, PixelLocationRaw, HimetricLocationRaw;
        public uint Time, HistoryCount;
        public int InputData;
        public uint KeyStates;
        public ulong PerformanceCount;
        public int ButtonChangeType;
    }

    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetPointerInfo(uint pointerId, out PointerInfo info);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetPointerType(uint pointerId, out uint type);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowFeedbackSetting(nint hwnd, int feedback, uint flags, uint size, ref int value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetProp(nint hwnd, string name, nint data);
    [DllImport("user32.dll")] internal static extern uint GetMessageExtraInfo();

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] internal static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetWindowRect(nint hwnd, out NRect rect);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowEnabled(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetLastActivePopup(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(NPoint point);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint SetCapture(nint hwnd);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool ReleaseCapture();
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetCursorPos(out NPoint point);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool ClientToScreen(nint hwnd, ref NPoint point);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetLayeredWindowAttributes(nint hwnd, uint key, byte alpha, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo { public int Size; public NRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] internal static extern nint MonitorFromPoint(NPoint point, uint flags);
    [DllImport("user32.dll")] internal static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    /// <summary>Bounds and work area of the primary monitor (the Z13 panel), in physical pixels.</summary>
    internal static (NRect Monitor, NRect Work) PrimaryMonitor()
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        GetMonitorInfo(MonitorFromPoint(default, 1 /* MONITOR_DEFAULTTOPRIMARY */), ref info);
        return (info.Monitor, info.Work);
    }

    // AppBar
    [StructLayout(LayoutKind.Sequential)]
    internal struct AppBarData { public int Size; public nint Hwnd; public uint CallbackMessage; public uint Edge; public NRect Rc; public nint LParam; }
    internal const uint AbmNew = 0, AbmRemove = 1, AbmQueryPos = 2, AbmSetPos = 3, AbmActivate = 6, AbmWindowPosChanged = 9;
    internal const uint AbeBottom = 3;
    internal const int AbnPosChanged = 1, AbnFullScreenApp = 2;
    [DllImport("shell32.dll")] internal static extern nint SHAppBarMessage(uint message, ref AppBarData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string name);

    // DWM
    internal const int DwmwaExtendedFrameBounds = 9, DwmwaCloaked = 14, DwmwaWindowCornerPreference = 33, DwmwaSystemBackdropType = 38, DwmwaUseImmersiveDarkMode = 20, DwmwaTransitionsForceDisabled = 3;
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out NRect value, int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
    [StructLayout(LayoutKind.Sequential)] internal struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] internal static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins margins);

    internal const uint TnpRectDestination = 0x1, TnpRectSource = 0x2, TnpOpacity = 0x4, TnpVisible = 0x8, TnpSourceClientAreaOnly = 0x10;
    [StructLayout(LayoutKind.Sequential)]
    internal struct ThumbnailProperties
    {
        public uint Flags;
        public NRect Destination, Source;
        public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool SourceClientAreaOnly;
    }
    [DllImport("dwmapi.dll")] internal static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);
    [DllImport("dwmapi.dll")] internal static extern int DwmUnregisterThumbnail(nint thumbnail);
    [DllImport("dwmapi.dll")] internal static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref ThumbnailProperties properties);
    [DllImport("dwmapi.dll")] internal static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out NPoint size);

    /// <summary>The visible frame of a window (without the invisible resize borders), in screen pixels.</summary>
    internal static NRect VisibleBounds(nint hwnd)
    {
        if (DwmGetWindowAttribute(hwnd, DwmwaExtendedFrameBounds, out var rect, Marshal.SizeOf<NRect>()) == 0 && rect.Width > 0) return rect;
        GetWindowRect(hwnd, out rect);
        return rect;
    }

    // WinEvents
    internal delegate void WinEventProc(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time);
    internal const uint EventSystemForeground = 0x0003, EventSystemMinimizeStart = 0x0016, EventSystemMinimizeEnd = 0x0017, EventObjectDestroy = 0x8001, EventObjectHide = 0x8003, EventObjectLocationChange = 0x800B, EventObjectCloaked = 0x8017;
    [DllImport("user32.dll")] internal static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventProc proc, uint process, uint thread, uint flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWinEvent(nint hook);

    // Screen sampling for the home bar tint.
    [DllImport("user32.dll")] internal static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("gdi32.dll")] internal static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] internal static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll")] internal static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool BitBlt(nint dest, int x, int y, int width, int height, nint source, int sx, int sy, uint rop);
    [DllImport("gdi32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteDC(nint dc);
    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader { public int Size, Width, Height; public short Planes, BitCount; public int Compression, SizeImage, XPels, YPels, ClrUsed, ClrImportant; }
    [DllImport("gdi32.dll")] internal static extern int GetDIBits(nint dc, nint bitmap, uint start, uint lines, [Out] byte[] bits, ref BitmapInfoHeader info, uint usage);
}
