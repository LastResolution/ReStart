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
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new System.Windows.Controls.TextBlock { Text = "Checking the taskbar on this display.\nThe original state will be restored automatically.", Margin = new(24), TextWrapping = TextWrapping.Wrap }
        };
        TaskbarLease? lease = null;
        var open = true;
        window.Closed += (_, _) => Volatile.Write(ref open, false);
        var available = false; var blocked = false; var acquired = false; var held = false; var restored = false;
        var observations = new List<object>();
        try
        {
            directory = Path.GetFullPath(directory); TaskbarRecoveryStore.CheckLinks(directory); Directory.CreateDirectory(directory);
            window.Show();
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle);
            var bounds = screen.Bounds;
            var backend = new TaskbarNativeBackend { Scope = new(bounds.X, bounds.Y, bounds.Width, bounds.Height) };
            available = await Task.Run(() => backend.Capture(TaskbarLeaseMode.Reveal) != null);
            if (available)
            {
                var store = new TaskbarRecoveryStore(TaskbarWatchdog.Profile);
                blocked = !await Task.Run(() => TaskbarRecovery.Run(store, backend, TaskbarWatchdog.IsAlive));
                if (!blocked)
                {
                    var watchdog = new TaskbarWatchdog(store);
                    lease = new(store, backend, watchdog.Protect, () => watchdog.IsReady);
                    acquired = await Task.Run(() => lease.Synchronize(TaskbarLeaseMode.Reveal, () => Volatile.Read(ref open)));
                    held = acquired;
                    for (var index = 0; index < 8 && open && acquired; index++)
                    {
                        await Task.Delay(200);
                        held &= await Task.Run(() =>
                        {
                            var current = backend.Capture(TaskbarLeaseMode.Reveal);
                            var original = lease.Snapshot;
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
            var success = !available || !blocked && acquired && held && restored;
            var path = Path.Combine(directory, "taskbar-live.json"); TaskbarRecoveryStore.CheckLinks(path);
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                await JsonSerializer.SerializeAsync(output, new { success, available, blocked, acquired, held, restored, observations });
            return success ? 0 : 1;
        }
        catch (Exception error) when (TaskbarLease.Expected(error) || error is ArgumentException) { return 1; }
        finally { if (lease != null) await Task.Run(lease.Dispose); window.Close(); }
    }
}
