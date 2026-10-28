using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ReStart.App.Tablet;

/// <summary>A top-level window the user would think of as an open app.</summary>
/// <param name="Handle">Top-level window handle (for UWP apps, the ApplicationFrameWindow).</param>
/// <param name="Title">Window title.</param>
/// <param name="ProcessId">Owning process; for UWP apps, the hosted app's process rather than ApplicationFrameHost.</param>
/// <param name="ExePath">Executable of <paramref name="ProcessId"/>, when readable.</param>
/// <param name="IsMinimized">Whether the window is iconic.</param>
internal sealed record AppWindow(nint Handle, string Title, int ProcessId, string? ExePath, bool IsMinimized);

/// <summary>
/// Enumerates and drives ordinary app windows (roughly the Alt+Tab set), excluding
/// shell surfaces and this process's own windows. Shared by the window manager,
/// app switcher and dock.
/// </summary>
internal static class AppWindows
{
    private const int GwlStyle = -16, GwlExStyle = -20;
    private const long WsCaption = 0x00C00000, WsThickFrame = 0x00040000, WsMaximizeBox = 0x00010000, WsChild = 0x40000000;
    private const long WsExToolWindow = 0x00000080, WsExAppWindow = 0x00040000, WsExNoActivate = 0x08000000;
    private const uint GwOwner = 4;
    private const int DwmwaCloaked = 14;
    private const int SwRestore = 9, SwMaximize = 3, SwShow = 5;
    private const uint WmClose = 0x0010;

    // Shell surfaces that look like windows but are not apps.
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow",
        "XamlExplorerHostIslandWindow", "TopLevelWindowForOverflowXamlIsland", "NotifyIconOverflowWindow",
        "ForegroundStaging", "MultitaskingViewFrame", "EdgeUiInputTopWndClass", "EdgeUiInputWndClass",
        "ApplicationManager_ImmersiveShellWindow", "Internet Explorer_Hidden", "Windows.Internal.Shell.TabProxyWindow",
        "Shell_InputSwitchTopLevelWindow", "SysShadow", "tooltips_class32", "IME", "MSCTFIME UI",
    };

    private static readonly int OwnProcess = Environment.ProcessId;

    /// <summary>App windows in z-order, frontmost first.</summary>
    internal static List<AppWindow> List()
    {
        var result = new List<AppWindow>();
        EnumWindows((hwnd, _) =>
        {
            if (Describe(hwnd) is { } window) result.Add(window);
            return true;
        }, 0);
        return result;
    }

    /// <summary>Whether <paramref name="hwnd"/> is an ordinary app window (visible, uncloaked, captioned, not a tool/shell/own window).</summary>
    internal static bool IsAppWindow(nint hwnd) => Describe(hwnd) != null;

    /// <summary>Builds the <see cref="AppWindow"/> for a qualifying window, or null.</summary>
    internal static AppWindow? Describe(nint hwnd)
    {
        if (hwnd == 0 || !IsWindowVisible(hwnd)) return null;
        var style = GetWindowLongPtr(hwnd, GwlStyle).ToInt64();
        var ex = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        if ((style & WsChild) != 0) return null;
        var appWindow = (ex & WsExAppWindow) != 0;
        if (!appWindow && ((ex & WsExToolWindow) != 0 || (ex & WsExNoActivate) != 0)) return null;
        if (!appWindow && GetWindow(hwnd, GwOwner) != 0) return null;
        if ((style & WsCaption) != WsCaption && !appWindow) return null;
        if (IsCloaked(hwnd)) return null;
        var cls = ClassName(hwnd);
        if (ShellClasses.Contains(cls)) return null;
        var title = Title(hwnd);
        if (title.Length == 0) return null;
        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == OwnProcess) return null;
        if (cls == "ApplicationFrameWindow")
        {
            // UWP: the real app is a CoreWindow child in another process; no child means a suspended ghost frame.
            var core = FindWindowEx(hwnd, 0, "Windows.UI.Core.CoreWindow", null);
            if (core != 0) GetWindowThreadProcessId(core, out pid);
        }
        return new AppWindow(hwnd, title, (int)pid, ExePath((int)pid), IsIconic(hwnd));
    }

    /// <summary>Whether the window can be maximized (resizable with a maximize box).</summary>
    internal static bool CanMaximize(nint hwnd)
    {
        var style = GetWindowLongPtr(hwnd, GwlStyle).ToInt64();
        return (style & WsThickFrame) != 0 && (style & WsMaximizeBox) != 0;
    }

    /// <summary>Maximizes a resizable window that is not already maximized.</summary>
    internal static void Maximize(nint hwnd)
    {
        if (CanMaximize(hwnd) && !IsZoomed(hwnd)) ShowWindowAsync(hwnd, SwMaximize);
    }

    /// <summary>Restores if minimized, optionally maximizes, and brings the window to the foreground.</summary>
    internal static void Activate(nint hwnd, bool maximize)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, maximize && CanMaximize(hwnd) ? SwMaximize : SwRestore);
        else if (maximize) Maximize(hwnd);
        else ShowWindow(hwnd, SwShow);
        SetForeground(hwnd);
    }

    /// <summary>Foreground activation that survives the foreground lock: a plain call first, then by sharing the current foreground thread's input state.</summary>
    internal static bool SetForeground(nint hwnd)
    {
        if (SetForegroundWindow(hwnd) && GetForegroundWindow() == hwnd) return true;
        var previous = GetForegroundWindow();
        var me = GetCurrentThreadId();
        var other = previous == 0 ? 0 : GetWindowThreadProcessId(previous, out _);
        if (other != 0 && other != me && !IsHungAppWindow(previous) && AttachThreadInput(me, other, true))
        {
            try { BringWindowToTop(hwnd); SetForegroundWindow(hwnd); }
            finally { AttachThreadInput(me, other, false); }
        }
        // Tablet core fix: when this process holds the foreground without having received input
        // (Home shown on a posture change), SetForegroundWindow is refused and the attach above is a
        // no-op (same thread). The shell's task-switch call still works there.
        if (GetForegroundWindow() != hwnd) SwitchToThisWindow(hwnd, true);
        return GetForegroundWindow() == hwnd;
    }

    /// <summary>Asks the window to close, like its close button.</summary>
    internal static void Close(nint hwnd) => PostMessage(hwnd, WmClose, 0, 0);

    /// <summary>Window title.</summary>
    internal static string Title(nint hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0) return "";
        var text = new StringBuilder(length + 1);
        GetWindowText(hwnd, text, text.Capacity);
        return text.ToString();
    }

    /// <summary>Window class name.</summary>
    internal static string ClassName(nint hwnd)
    {
        var name = new StringBuilder(256);
        GetClassName(hwnd, name, name.Capacity);
        return name.ToString();
    }

    /// <summary>DWM cloaking (other virtual desktop, suspended UWP, hidden shell UI).</summary>
    internal static bool IsCloaked(nint hwnd) => DwmGetWindowAttribute(hwnd, DwmwaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0;

    /// <summary>Full executable path of a process, or null if it is not readable (elevated/protected).</summary>
    internal static string? ExePath(int pid)
    {
        var handle = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid);
        if (handle == 0) return null;
        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString() : null;
        }
        finally { CloseHandle(handle); }
    }

    /// <summary>Process name without extension, for display fallbacks.</summary>
    internal static string ProcessName(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return p.ProcessName; }
        catch (ArgumentException) { return ""; }
        catch (InvalidOperationException) { return ""; }
    }

    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string? className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder name, int max);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindowAsync(nint hwnd, int command);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool BringWindowToTop(nint hwnd);
    [DllImport("user32.dll")] private static extern void SwitchToThisWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool altTab);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool AttachThreadInput(uint from, uint to, [MarshalAs(UnmanagedType.Bool)] bool attach);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsHungAppWindow(nint hwnd);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int pid);
    [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder name, ref int size);
}
