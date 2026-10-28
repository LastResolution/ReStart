using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media;
using ReStart.App.Services;

namespace ReStart.App.Tablet.Gestures;

/// <summary>Display names, icons and identities of app windows, cached per executable.</summary>
internal static class AppInfo
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ImageSource?> Icons = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Executable file name ("brave.exe"), or "" when unreadable.</summary>
    internal static string ExeName(AppWindow window) => window.ExePath is { } path ? Path.GetFileName(path) : "";

    /// <summary>Short app name for labels ("Brave", "Spotify", "File Explorer"), not the window title.</summary>
    internal static string Name(AppWindow window)
    {
        var key = window.ExePath ?? "pid:" + window.ProcessId;
        if (Names.TryGetValue(key, out var cached)) return cached;
        var name = "";
        if (window.ExePath is { } path)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                name = info.FileDescription?.Trim() is { Length: > 0 } d ? d : info.ProductName?.Trim() ?? "";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
            name = Path.GetFileName(path).ToLowerInvariant() switch
            {
                "explorer.exe" => "File Explorer",
                "brave.exe" => "Brave",
                "windowsterminal.exe" => "Terminal",
                _ => name,
            };
            if (name.Length == 0 || name.Length > 40) name = Path.GetFileNameWithoutExtension(path);
        }
        if (name.Length == 0) name = window.Title;
        return Names[key] = name;
    }

    /// <summary>App icon at <paramref name="size"/> pixels (shell image factory, the same pipeline as ReStart's tiles).</summary>
    internal static ImageSource? Icon(AppWindow window, int size = 96)
    {
        var aumid = PackagedAumid(window.Handle);
        var target = aumid != null ? "shell:AppsFolder\\" + aumid : window.ExePath;
        return target == null ? null : Icon(target, size);
    }

    /// <summary>Icon of a parsing name or path, cached.</summary>
    internal static ImageSource? Icon(string target, int size)
    {
        var key = target + "|" + size;
        if (Icons.TryGetValue(key, out var cached)) return cached;
        return Icons[key] = AppCatalog.GetIcon(target, size);
    }

    /// <summary>AUMID of a UWP/packaged app window (from the window's property store), or null for ordinary Win32 apps.</summary>
    internal static string? PackagedAumid(nint hwnd)
    {
        if (AppWindows.ClassName(hwnd) != "ApplicationFrameWindow") return null;
        return WindowAumid(hwnd);
    }

    /// <summary>Explicit AppUserModelID of a window (PKEY_AppUserModel_ID), or null.</summary>
    internal static string? WindowAumid(nint hwnd)
    {
        var iid = typeof(IPropertyStore).GUID;
        if (SHGetPropertyStoreForWindow(hwnd, ref iid, out var store) != 0 || store == null) return null;
        try
        {
            var key = new PropertyKey { Format = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
            if (store.GetValue(ref key, out var value) != 0) return null;
            try { return value.Type == 31 /* VT_LPWSTR */ ? Marshal.PtrToStringUni(value.Pointer) : null; }
            finally { PropVariantClear(ref value); }
        }
        finally { Marshal.ReleaseComObject(store); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit)] private struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public nint Pointer; [FieldOffset(16)] public nint Extra; }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    }
    [DllImport("shell32.dll")] private static extern int SHGetPropertyStoreForWindow(nint hwnd, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore? store);
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);
}

/// <summary>
/// Tracks the foreground window and keeps a most-recently-used order of app windows, which
/// the z-order alone loses once apps get minimized or covered by Home.
/// </summary>
internal sealed class ForegroundWatcher : IDisposable
{
    private readonly NavNative.WinEventProc _proc;
    private readonly nint _hook;
    private readonly Dictionary<nint, long> _stamps = [];
    private long _counter;

    /// <summary>Raised on the UI thread with the new foreground window.</summary>
    internal event Action<nint>? Changed;
    /// <summary>Last foreground window reported.</summary>
    internal nint Foreground { get; private set; }

    internal ForegroundWatcher()
    {
        _proc = OnEvent; // keep the delegate alive for the hook
        _hook = NavNative.SetWinEventHook(NavNative.EventSystemForeground, NavNative.EventSystemForeground, 0, _proc, 0, 0, 0 /* OUTOFCONTEXT */);
        Foreground = AppWindows.GetForegroundWindow();
        Touch(Foreground);
    }

    private void OnEvent(nint hook, uint type, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != 0) return;
        Foreground = hwnd;
        TabletNavigation.Guard("foreground", () => { Touch(hwnd); Changed?.Invoke(hwnd); });
    }

    /// <summary>Marks a window (or the app window owning a dialog) as most recently used.</summary>
    internal void Touch(nint hwnd)
    {
        if (hwnd == 0) return;
        var root = NavNative.GetAncestor(hwnd, 3 /* GA_ROOTOWNER */);
        if (AppWindows.IsAppWindow(hwnd)) _stamps[hwnd] = ++_counter;
        else if (root != 0 && AppWindows.IsAppWindow(root)) _stamps[root] = ++_counter;
    }

    /// <summary>App windows, most recently used first (windows never activated since startup follow in z-order).</summary>
    internal List<AppWindow> Recent()
    {
        var list = AppWindows.List();
        foreach (var stale in _stamps.Keys.Where(h => !NavNative.IsWindow(h)).ToList()) _stamps.Remove(stale);
        return list.Select((w, i) => (w, i)).OrderByDescending(x => _stamps.TryGetValue(x.w.Handle, out var s) ? s : long.MinValue + 1_000_000 - x.i)
            .Select(x => x.w).ToList();
    }

    public void Dispose() { if (_hook != 0) NavNative.UnhookWinEvent(_hook); }
}
