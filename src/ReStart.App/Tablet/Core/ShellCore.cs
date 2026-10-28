using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ReStart.App.Services;
using ReStart.App.Tablet.Windowing;

namespace ReStart.App.Tablet.Core;

/// <summary>
/// Shell core of the tablet edition. ReStart stays resident; in tablet mode it captures
/// Start, hides the taskbar, runs apps maximized and is the Home screen under them. In
/// laptop mode it captures nothing and only appears when explicitly opened.
/// </summary>
internal static class ShellCore
{
    // How long a first tablet presentation waits for live-tile frames; then Home shows and they fill in place.
    private static readonly TimeSpan WarmUpWait = TimeSpan.FromMilliseconds(750);
    private static MainWindow? _window;
    private static StartIntegration? _integration;
    private static TaskbarControl? _taskbar;
    private static WindowManager? _windows;
    private static int _generation;
    // `--no-warmup` measures the upstream path (splash on the first presentation) for comparison.
    private static bool _warmUp = true;

    /// <summary>Wires the shell core (primary, non-preview instance only), then applies the current posture.</summary>
    internal static void Initialize(MainWindow window, StartIntegration integration, string[] args)
    {
        if (_window != null) return;
        var background = args.Contains("--background");
        _warmUp = !args.Contains("--no-warmup");
        _window = window;
        _integration = integration;
        var dispatcher = window.Dispatcher;
        TaskbarControl.RecoverAtStartup(TabletShell.IsTablet);
        _taskbar = new TaskbarControl(dispatcher);
        _windows = new WindowManager(dispatcher, () => new WindowInteropHelper(window).Handle);
        _windows.TrayShown += _taskbar.OnTrayShown;
        _windows.ForegroundChanged += OnForeground;
        window.Activated += (_, _) => window.SetTabletCovered(false);
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        TabletShell.ShowHome = ShowHome;
        TabletShell.ShowDesktop = ShowDesktop;
        TabletCommands.Register("desktop", _ => ShowDesktop());
        TabletShell.TabletChanged += OnTabletChanged;
        TabletCommands.Register("exit", _ => (Application.Current as App)?.ExitApplication());
        TabletCommands.Register("status", _ => LogStatus());
        TabletCommands.Register("maximize-all", _ => TabletLog.Write("windows", $"maximized {_windows.MaximizeAll()} window(s) (command)"));
        // `activate brave`: the frontmost window of a process, maximized in tablet mode (tests, button bindings).
        TabletCommands.Register("activate", name =>
        {
            var match = AppWindows.List().FirstOrDefault(w => string.Equals(AppWindows.ProcessName(w.ProcessId), name, StringComparison.OrdinalIgnoreCase));
            if (match == null) { TabletLog.Write("command", $"activate {name}: no window"); return; }
            var before = AppWindows.GetForegroundWindow();
            AppWindows.Activate(match.Handle, TabletShell.IsTablet);
            var after = AppWindows.GetForegroundWindow();
            TabletLog.Write("command", $"activate {name}: {(after == match.Handle ? "ok" : "not foreground")} (min={match.IsMinimized}, foreground before [{AppWindows.ClassName(before)}] after [{AppWindows.ClassName(after)}], home topmost={_window?.Topmost})");
        });
        // `screenshot C:\path.png`: the screen from inside the shell, so tests take no foreground (test helper).
        TabletCommands.Register("screenshot", path => TabletLog.Write("command", $"screenshot: {(path != null && Screenshot(path) ? "saved" : "failed")}"));
        TabletCommands.Register("launch", name => TabletLog.Write("command", $"launch {name}: {(name != null && window.LaunchByName(name) ? "started" : "no such app")}"));
        // `close notepad` / `minimize notepad`: the frontmost window of a process (test helpers).
        TabletCommands.Register("close", name => WithWindow("close", name, AppWindows.Close));
        TabletCommands.Register("minimize", name => WithWindow("minimize", name, hwnd => ShowWindowAsync(hwnd, 6)));
        TabletCommands.Register("sysclose", name => WithWindow("sysclose", name, hwnd => PostMessage(hwnd, 0x0112, 0xF060, 0))); // SC_CLOSE, like the X button
        integration.SetTablet(TabletShell.IsTablet);
        LogProcessStart();
        _ = MeasureStartupAsync();
        if (TabletShell.IsTablet) Enter("startup");
        else if (background && _warmUp) _ = window.WarmUpAsync();
    }

    /// <summary>Restores the taskbar and removes hooks (application exit or session end).</summary>
    internal static void Shutdown()
    {
        if (_window == null) return;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        TabletShell.TabletChanged -= OnTabletChanged;
        _windows?.Dispose();
        _taskbar?.Dispose();
        _window = null;
    }

    private static void OnTabletChanged(bool tablet)
    {
        if (tablet) Enter("posture");
        else Leave();
    }

    private static async void Enter(string reason)
    {
        // async void: nothing may escape to the dispatcher's fatal-error handler.
        try { await EnterAsync(reason); }
        catch (Exception ex) { TabletLog.Write("mode", "entering tablet mode failed: " + ex); }
    }

    private static async Task EnterAsync(string reason)
    {
        if (_window is not { } window) return;
        var generation = ++_generation;
        var watch = Stopwatch.StartNew();
        _integration!.SetTablet(true);
        _windows!.Start();
        var prepared = window.IsPrepared;
        if (!prepared && _warmUp)
        {
            // First presentation since start (logon): catalog and icons are needed; give live
            // tiles a moment to render, then show Home anyway (no splash) and let them finish in place.
            var warm = window.WarmUpAsync();
            await window.InitializeAsync();
            await Task.WhenAny(warm, Task.Delay(WarmUpWait));
            if (generation != _generation || !TabletShell.IsTablet) return;
            if (!window.IsPrepared) window.SkipStartupSplash();
        }
        // Home comes first and stays topmost while the desktop is rearranged behind it: hiding
        // the taskbar (a synchronous, ~0.5 s explorer round trip) and maximizing the apps.
        window.EnterTabletHome();
        LogWhenShown(watch, $"enter tablet ({reason}{(prepared ? ", prepared" : ", cold")})");
        await Task.Delay(400); // let the entrance animation run without competing with explorer
        if (generation != _generation || !TabletShell.IsTablet) return;
        _taskbar!.Hide();
        var maximized = _windows.MaximizeAll();
        await Task.Delay(maximized > 0 ? 450 : 50);
        if (generation == _generation && TabletShell.IsTablet) window.SettleTabletHome();
    }

    private static void Leave()
    {
        if (_window is not { } window) return;
        TabletShell.IsDesktopSession = false;
        _generation++;
        _integration!.SetTablet(false);
        _windows!.Stop();
        _taskbar!.Restore();
        window.LeaveTabletHome();
        TabletLog.Write("mode", "laptop: Home hidden, taskbar restored, Start capture off");
    }

    // Desktop session: tablet posture, but the user asked for the Windows desktop. Home hides,
    // the taskbar comes back, windows keep their size and the desktop is no longer redirected
    // to Home. Start capture stays on, so Start (or the home bar) is the way back to Home.
    private static void ShowDesktop()
    {
        if (_window is not { } window || !TabletShell.IsTablet || TabletShell.IsDesktopSession) return;
        TabletShell.IsDesktopSession = true;
        _generation++;
        TabletShell.CloseOverlays();
        _windows!.Stop();
        _taskbar!.Restore();
        window.LeaveTabletHome();
        MinimizeAll();
        TabletLog.Write("mode", "desktop session: Home hidden, taskbar shown, windows minimized");
    }

    private static void MinimizeAll()
    {
        try
        {
            if (Type.GetTypeFromProgID("Shell.Application") is { } type && Activator.CreateInstance(type) is { } shell)
            {
                type.InvokeMember("MinimizeAll", System.Reflection.BindingFlags.InvokeMethod, null, shell, null);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }
        catch (Exception ex) { TabletLog.Write("mode", "minimize all failed: " + ex.Message); }
    }

    private static void ShowHome()
    {
        if (_window is not { } window) return;
        if (TabletShell.IsDesktopSession)
        {
            // Leaving the desktop session: back to full tablet behavior (taskbar hidden, apps maximized, Home).
            TabletShell.IsDesktopSession = false;
            TabletLog.Write("mode", "desktop session ended");
            if (TabletShell.IsTablet) { Enter("from desktop"); return; }
        }
        var watch = Stopwatch.StartNew();
        if (!window.IsTabletHome)
        {
            // Laptop mode: the ordinary Start presentation.
            var cold = !window.IsPrepared;
            window.Present();
            LogWhenShown(watch, $"open Start ({(cold ? "cold" : "prepared")})");
            return;
        }
        var front = window.IsHomeInFront;
        window.ShowTabletHome();
        if (!front) LogWhenShown(watch, "go home");
    }

    // Home covered by an app pauses its live tiles; Home activation resumes them.
    private static void OnForeground(nint hwnd)
    {
        if (_window is not { } window || hwnd == 0) return;
        AppWindows.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == Environment.ProcessId) return; // Home's own Activated handler un-covers
        if (AppWindows.IsAppWindow(hwnd) && (AppWindows.IsZoomed(hwnd) || AppWindows.CanMaximize(hwnd))) window.SetTabletCovered(true);
    }

    private static void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        _window?.Dispatcher.BeginInvoke(() => _window?.RefitTabletHome(), DispatcherPriority.Background);

    // First rendered frame after a presentation, and the end of its fade-in.
    private static void LogWhenShown(Stopwatch watch, string what)
    {
        if (_window is not { } window) return;
        long first = -1;
        var deadline = Environment.TickCount64 + 15000;
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            if (Environment.TickCount64 > deadline) { CompositionTarget.Rendering -= handler; TabletLog.Write("perf", what + ": Home not shown within 15 s"); return; }
            if (!window.IsVisible) return;
            if (first < 0) first = watch.ElapsedMilliseconds;
            if (window.Opacity < 0.999) return;
            CompositionTarget.Rendering -= handler;
            TabletLog.Write("perf", $"{what}: first frame {first} ms, fully shown {watch.ElapsedMilliseconds} ms");
        };
        CompositionTarget.Rendering += handler;
    }

    private static void LogProcessStart()
    {
        try
        {
            using var self = Process.GetCurrentProcess();
            var started = self.StartTime;
            var logon = Process.GetProcessesByName("explorer").Select(p => { using (p) return p.SessionId == self.SessionId ? p.StartTime : DateTime.MaxValue; }).DefaultIfEmpty(DateTime.MaxValue).Min();
            var sinceLogon = logon == DateTime.MaxValue ? "" : $", {(started - logon).TotalSeconds:0.0} s after explorer (logon)";
            TabletLog.Write("perf", $"shell core up {(DateTime.Now - started).TotalMilliseconds:0} ms after process start{sinceLogon}, {Environment.TickCount64 / 1000} s after boot");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    private static async Task MeasureStartupAsync()
    {
        if (_window is not { } window) return;
        using var self = Process.GetCurrentProcess();
        var started = self.StartTime;
        await window.InitializeAsync();
        TabletLog.Write("perf", $"catalog and icons ready {(DateTime.Now - started).TotalMilliseconds:0} ms after process start");
        while (!window.IsPrepared && _window != null) await Task.Delay(100);
        TabletLog.Write("perf", $"ready (tiles prepared) {(DateTime.Now - started).TotalMilliseconds:0} ms after process start");
    }

    private static void WithWindow(string verb, string? name, Action<nint> action)
    {
        var match = AppWindows.List().FirstOrDefault(w => string.Equals(AppWindows.ProcessName(w.ProcessId), name, StringComparison.OrdinalIgnoreCase));
        if (match != null) action(match.Handle);
        TabletLog.Write("command", $"{verb} {name}: {(match == null ? "no window" : "done")}");
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(nint hwnd, int command);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);

    private static bool Screenshot(string path)
    {
        try
        {
            var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            using var bitmap = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) graphics.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
            bitmap.Save(path + ".tmp", System.Drawing.Imaging.ImageFormat.Png);
            File.Move(path + ".tmp", path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or ArgumentException or System.Runtime.InteropServices.ExternalException) { return false; }
    }

    private static void LogStatus()
    {
        if (_window is not { } window) return;
        var foreground = AppWindows.GetForegroundWindow();
        var home = new WindowInteropHelper(window).Handle;
        // Visible top-level windows from the top of the z-order: what the user actually sees.
        var top = new List<string>();
        for (var w = GetTopWindow(0); w != 0 && top.Count < 6; w = GetWindow(w, 2 /* GW_HWNDNEXT */))
        {
            if (!IsWindowVisible(w) || AppWindows.IsCloaked(w)) continue;
            var topmost = (AppWindows.GetWindowLongPtr(w, -20).ToInt64() & 0x8) != 0;
            AppWindows.GetWindowThreadProcessId(w, out var pid);
            top.Add($"{(w == home ? "HOME" : AppWindows.ClassName(w))}/{AppWindows.ProcessName((int)pid)}{(topmost ? "!top" : "")}");
        }
        TabletLog.Write("status", $"zorder: {string.Join(" > ", top)}");
        TabletLog.Write("status", $"tablet={TabletShell.IsTablet} home={window.IsTabletHome} visible={window.IsVisible} active={window.IsActive} topmost={window.Topmost} prepared={window.IsPrepared} " +
            $"taskbarHidden={_taskbar?.IsHidden} wm={_windows?.IsActive} foreground=[{AppWindows.ClassName(foreground)}] apps: " +
            string.Join(", ", AppWindows.List().Select(w => $"{AppWindows.ProcessName(w.ProcessId)}{(w.IsMinimized ? " min" : AppWindows.IsZoomed(w.Handle) ? " max" : " normal")}")));
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint GetTopWindow(nint parent);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hwnd);
}
