using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using ReStart.App.Services;

namespace ReStart.App;

/// <summary>Provides an explicit local-only native reveal check. CI uses inert dependencies instead.</summary>
internal static class TaskbarLiveCheck
{
    internal static async Task<int> RunAsync(string directory)
    {
        var window = new Window
        {
            Title = "ReStart — Taskbar check",
            Width = 500,
            Height = 160,
            Topmost = true,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new System.Windows.Controls.TextBlock { Text = "Checking the taskbar on this display.\nThe original state will be restored automatically.", Margin = new(24), TextWrapping = TextWrapping.Wrap }
        };
        TaskbarLease? lease = null;
        var open = true;
        window.Closed += (_, _) => Volatile.Write(ref open, false);
        var available = false; var blocked = false; var acquired = false; var held = false; var restored = false;
        var overlay = true; var fullscreenStable = true;
        Exception? acquisitionFailure = null;
        var acquisitionAttempts = 0;
        var observations = new List<object>();
        try
        {
            directory = Path.GetFullPath(directory); TaskbarRecoveryStore.CheckLinks(directory); Directory.CreateDirectory(directory);
            window.Show();
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle);
            var bounds = screen.Bounds;
            var launcher = new WindowInteropHelper(window).Handle;
            MonitorPlacement.FitWorkingArea(launcher, new nint(-1), bounds, 0x0040);
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
                        new(bounds.X, bounds.Y, bounds.Width, bounds.Height)), 0x0014);
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
                                        dx = actual.Original.X - target.X,
                                        dy = actual.Original.Y - target.Y,
                                        dw = actual.Original.Width - target.Width,
                                        dh = actual.Original.Height - target.Height
                                    });
                                }
                            var visible = current != null && original != null && current.Windows.All(w => w.Visible && original.Windows.Any(o => o.Window == w.Window && o.Target == w.Original));
                            return lease.Synchronize(TaskbarLeaseMode.Reveal, () => Volatile.Read(ref open)) && visible;
                        });
                    }
                    var saved = lease.Snapshot;
                    restored = await Task.Run(() =>
                    {
                        if (!lease.Synchronize(null)) return false;
                        var after = backend.Capture(TaskbarLeaseMode.Reveal);
                        return saved != null && after != null && saved.Windows.All(w => after.Windows.Any(a => a.Window == w.Window && a.Original == w.Original));
                    });
                }
            }
            var success = available && !blocked && acquired && held && restored && overlay && fullscreenStable;
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
                    acquisitionAttempts,
                    failure = acquisitionFailure?.Message,
                    nativeCode = (acquisitionFailure as System.ComponentModel.Win32Exception)?.NativeErrorCode,
                    observations
                });
            return success ? 0 : 1;
        }
        catch (Exception error) when (TaskbarLease.Expected(error) || error is ArgumentException) { return 1; }
        finally { if (lease != null) await Task.Run(lease.Dispose); window.Close(); }
    }
}
