using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using ReStart.App.Services;

namespace ReStart.App;

/// <summary>Provides an explicit local-only native reveal check. CI uses inert dependencies instead.</summary>
internal static class TaskbarLiveCheck
{
    /// <summary>Checks real taskbar reveal and content recovery on one existing display without changing Windows preferences.</summary>
    /// <param name="directory">New local report directory; its JSON report must not already exist.</param>
    /// <param name="monitorIndex">Optional zero-based Screen.AllScreens ordinal; null selects the primary display. No monitor name is recorded.</param>
    /// <returns>Zero only after reveal, content visibility, front order and repeated recovery pass; one for unsupported configuration or failure.</returns>
    internal static async Task<int> RunAsync(string directory, int? monitorIndex = null)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (monitorIndex is { } requestedIndex && (requestedIndex < 0 || requestedIndex >= screens.Length)) return 1;
        var selected = monitorIndex is { } selectedIndex ? screens[selectedIndex] : System.Windows.Forms.Screen.PrimaryScreen;
        if (selected == null) return 1;
        var window = new Window
        {
            Title = "ReStart — Taskbar check",
            Width = 500,
            Height = 160,
            Topmost = true,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Content = new System.Windows.Controls.TextBlock { Text = "Checking the taskbar on this display.\nWindows retains control of automatic hiding.", Margin = new(24), TextWrapping = TextWrapping.Wrap }
        };
        var background = new Window
        {
            Title = "ReStart — Recovery fixture",
            Width = 360,
            Height = 120,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Content = new System.Windows.Controls.TextBlock { Text = "Checking automatic hiding after closing Start.", Margin = new(16), TextWrapping = TextWrapping.Wrap }
        };
        string? recoveryDecision = null;
        TaskbarLease? lease = null;
        var open = true;
        window.Closed += (_, _) => Volatile.Write(ref open, false);
        var available = false; var blocked = false; var acquired = false; var held = false; var restored = false;
        var overlay = true; var fullscreenStable = true;
        Exception? acquisitionFailure = null;
        var acquisitionAttempts = 0;
        var repeatCycles = 0;
        var autoHideBefore = TaskbarNativeBackend.AutoHideEnabled;
        var observations = new List<object>();
        static bool Recovered(TaskbarLeaseState? before, TaskbarLeaseState? after) => before != null && after != null
            && before.Windows.All(saved => after.Windows.Any(actual => actual.Window == saved.Window && actual.AutoHide
                && TaskbarNativeBackend.HiddenAtEdge(actual.Monitor, actual.Original,
                    actual.Target.Width >= actual.Monitor.Width / 2 ? actual.Target.Y <= actual.Monitor.Y ? 1 : 3 : actual.Target.X <= actual.Monitor.X ? 0 : 2)
                && (saved.Content ?? []).All(child => child.Visible || !(actual.Content ?? []).Any(current => current.Window == child.Window && current.Visible))));
        try
        {
            directory = Path.GetFullPath(directory); TaskbarRecoveryStore.CheckLinks(directory); Directory.CreateDirectory(directory);
            background.Show();
            var backgroundHandle = new WindowInteropHelper(background).Handle;
            Native.SetWindowPos(backgroundHandle, 0, selected.Bounds.X + 100, selected.Bounds.Y + 100, 360, 120, 0x0040);
            var launcher = new WindowInteropHelper(window).EnsureHandle();
            TaskbarNativeBackend.PrepareLauncher(launcher, true);
            MonitorPlacement.FitWorkingArea(launcher, 0, selected.Bounds, 0x0014, screen => screen.Bounds);
            window.Show();
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle);
            var bounds = screen.Bounds;
            MonitorPlacement.FitWorkingArea(launcher, new nint(-1), bounds, 0x0040, screen => screen.Bounds);
            var backend = new TaskbarNativeBackend { Scope = new(bounds.X, bounds.Y, bounds.Width, bounds.Height), LauncherWindow = launcher };
            available = await Task.Run(() => backend.Capture(TaskbarLeaseMode.Reveal) != null);
            if (available)
            {
                var store = new TaskbarRecoveryStore(TaskbarWatchdog.Profile);
                blocked = !await Task.Run(() => TaskbarRecovery.Run(store, backend, TaskbarWatchdog.IsAlive));
                if (!blocked)
                {
                    var watchdog = new TaskbarWatchdog(store);
                    lease = new(store, backend, watchdog.Protect, () => watchdog.IsReady);
                    var deadline = Environment.TickCount64 + 3000;
                    do
                    {
                        acquisitionAttempts++;
                        acquired = await Task.Run(() => lease.Synchronize(TaskbarLeaseMode.Reveal, () => Volatile.Read(ref open)));
                        acquisitionFailure = lease.LastFailure;
                        if (!acquired) await Task.Delay(200);
                    } while (!acquired && open && Environment.TickCount64 < deadline);
                    MonitorPlacement.FitWorkingArea(launcher, 0, TaskbarNativeBackend.AvailableArea(bounds, lease.Snapshot,
                        new(bounds.X, bounds.Y, bounds.Width, bounds.Height)), 0x0014, screen => screen.Bounds);
                    held = acquired;
                    for (var index = 0; index < 8 && open && acquired; index++)
                    {
                        await Task.Delay(200);
                        held &= await Task.Run(() =>
                        {
                            var current = backend.Capture(TaskbarLeaseMode.Reveal);
                            var original = lease.Snapshot;
                            Native.GetWindowRect(launcher, out var launcherRect);
                            fullscreenStable &= launcherRect.Left == bounds.Left && launcherRect.Top == bounds.Top
                                && launcherRect.Right == bounds.Right && launcherRect.Bottom == bounds.Bottom;
                            overlay &= original != null && original.Windows.All(w => TaskbarNativeBackend.IsAbove((nint)w.Window, launcher));
                            if (current != null && original != null)
                                foreach (var actual in current.Windows)
                                {
                                    var target = original.Windows.Single(w => w.Window == actual.Window).Target;
                                    observations.Add(new
                                    {
                                        actual.Visible,
                                        content = actual.Content?.Select(c => new { c.Visible }).ToArray(),
                                        dx = actual.Original.X - target.X,
                                        dy = actual.Original.Y - target.Y,
                                        dw = actual.Original.Width - target.Width,
                                        dh = actual.Original.Height - target.Height
                                    });
                                }
                            var visible = current != null && original != null && current.Windows.All(w => w.Visible && (w.Content == null || w.Content.All(c => c.Visible))
                                && original.Windows.Any(o => o.Window == w.Window && o.Target == w.Original));
                            return lease.Synchronize(TaskbarLeaseMode.Reveal, () => Volatile.Read(ref open)) && visible;
                        });
                    }
                    var saved = lease.Snapshot;
                    window.Hide();
                    background.Activate();
                    restored = await Task.Run(() =>
                    {
                        if (!lease.Synchronize(null)) return false;
                        var after = backend.Capture(TaskbarLeaseMode.Reveal);
                        return saved != null && after != null && saved.Windows.All(w => after.Windows.Any(a => a.Window == w.Window && a.AutoHide));
                    });
                    // A matching visible flag or saved rectangle does not prove
                    // auto-hide recovered. Wait for Explorer's own animation
                    // after closing the launcher; do not move the pointer.
                    var hideDeadline = Environment.TickCount64 + 5000;
                    var hidden = false;
                    while (restored && !hidden && Environment.TickCount64 < hideDeadline)
                    {
                        await Task.Delay(200);
                        var after = await Task.Run(() => backend.Capture(TaskbarLeaseMode.Reveal));
                        hidden = Recovered(saved, after);
                    }
                    restored &= hidden;
                    recoveryDecision = backend.LastRecoveryDecision;
                    // Repeat with the same watchdog and actual native window,
                    // starting from Explorer's hidden state each time.
                    for (var cycle = 0; cycle < 3 && restored; cycle++)
                    {
                        window.Show();
                        MonitorPlacement.FitWorkingArea(launcher, new nint(-1), bounds, 0x0040, screen => screen.Bounds);
                        var revealed = await Task.Run(() => lease.Synchronize(TaskbarLeaseMode.Reveal));
                        await Task.Delay(400);
                        var during = await Task.Run(() => backend.Capture(TaskbarLeaseMode.Reveal));
                        held &= revealed && during != null && during.Windows.All(w => w.AutoHide && w.Original == w.Target && (w.Content == null || w.Content.All(c => c.Visible))
                            && TaskbarNativeBackend.IsAbove((nint)w.Window, launcher));
                        var cycleSaved = lease.Snapshot;
                        window.Hide();
                        background.Activate();
                        restored &= await Task.Run(() => lease.Synchronize(null));
                        hidden = false; hideDeadline = Environment.TickCount64 + 5000;
                        while (restored && !hidden && Environment.TickCount64 < hideDeadline)
                        {
                            await Task.Delay(200);
                            var after = await Task.Run(() => backend.Capture(TaskbarLeaseMode.Reveal));
                            hidden = Recovered(cycleSaved, after);
                        }
                        restored &= hidden;
                        recoveryDecision = backend.LastRecoveryDecision;
                        if (held && restored) repeatCycles++;
                    }
                }
            }
            var autoHideAfter = TaskbarNativeBackend.AutoHideEnabled;
            var success = autoHideBefore && autoHideAfter && available && !blocked && acquired && held && restored && overlay && fullscreenStable && repeatCycles == 3;
            var path = Path.Combine(directory, "taskbar-live.json"); TaskbarRecoveryStore.CheckLinks(path);
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                await JsonSerializer.SerializeAsync(output, new
                {
                    success,
                    available,
                    blocked,
                    acquired,
                    held,
                    restored,
                    overlay,
                    fullscreenStable,
                    autoHideBefore,
                    autoHideAfter,
                    repeatCycles,
                    recoveryDecision,
                    acquisitionAttempts,
                    failure = acquisitionFailure?.Message,
                    nativeCode = (acquisitionFailure as System.ComponentModel.Win32Exception)?.NativeErrorCode,
                    observations
                });
            return success ? 0 : 1;
        }
        catch (Exception error) when (TaskbarLease.Expected(error) || error is ArgumentException) { return 1; }
        finally { if (lease != null) await Task.Run(lease.Dispose); window.Close(); background.Close(); }
    }
}
