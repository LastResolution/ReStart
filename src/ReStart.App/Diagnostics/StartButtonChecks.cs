using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using ReStart.App.Services;

namespace ReStart.App;

/// <summary>Exercises Start discovery against owned windows without hooks, Explorer changes, or profile access.</summary>
internal static class StartButtonChecks
{
    private sealed class Fixture : IDisposable
    {
        internal readonly HwndSource Source;
        internal readonly Button Button = new() { Width = 40, Height = 30, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        internal Fixture(bool visible)
        {
            Source = new(new HwndSourceParameters("Owned Start discovery fixture")
            { Width = 240, Height = 90, PositionX = 20, PositionY = 20, WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x08000080 });
            if (visible)
            {
                AutomationProperties.SetAutomationId(Button, "StartButton");
                Source.RootVisual = new Grid { Children = { Button } };
                Native.SetWindowPos(Source.Handle, 1, 20, 20, 240, 90, 0x10 | 0x40);
            }
        }
        public void Dispose() => Source.Dispose();
    }

    internal static async Task<int> RunAsync(string report, int iterations = 10000)
    {
        var checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        try
        {
            using var visible = new Fixture(true);
            using var first = new Fixture(false);
            using var second = new Fixture(false);
            using var process = Process.GetCurrentProcess();
            var started = process.StartTime.ToUniversalTime().Ticks;
            var primary = new TaskbarIdentity(first.Source.Handle, process.Id, started);
            var secondary = new TaskbarIdentity(second.Source.Handle, process.Id, started);
            var visual = new TaskbarIdentity(visible.Source.Handle, process.Id, started);
            var dispatcher = Dispatcher.CurrentDispatcher;
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var measurements = await Task.Run(() =>
            {
                Check(Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA, "Discovery did not run on an MTA thread.");
                var roots = 0;
                var failNext = false;
                var discovery = new StartButtonDiscovery(window =>
                {
                    roots++;
                    if (failNext) { failNext = false; throw new ElementNotAvailableException(); }
                    return AutomationElement.FromHandle(window);
                });
                var before = discovery.Read([visual]);
                Check(before.Length == 1 && before[0].Taskbar == visual.Window, "The owned Start button was not discovered.");
                var bounds = before[0].Bounds;
                dispatcher.Invoke(() => { visible.Button.Margin = new(70, 10, 0, 0); visible.Button.UpdateLayout(); });
                var moved = discovery.Read([visual]);
                Check(moved.Length == 1 && moved[0].Bounds.Left > bounds.Left + 60 && moved[0].ReadAt >= before[0].ReadAt,
                    "Cached providers returned stale Start geometry.");
                Check(roots == 1, "Moving Start recreated the taskbar root.");
                dispatcher.Invoke(() => visible.Button.Visibility = Visibility.Collapsed);
                Check(discovery.Read([visual]).Length == 0, "A hidden Start button remained clickable.");
                dispatcher.Invoke(() => visible.Button.Visibility = Visibility.Visible);
                Check(discovery.Read([visual]).Length == 1, "A restored Start button was not rediscovered.");
                discovery.Clear();
                discovery.Read([primary, secondary]);
                var stableRoots = roots;
                for (var i = 0; i < 1000; i++) discovery.Read([primary, secondary]);
                Check(roots == stableRoots, "Repeated polls recreated live taskbar roots.");
                discovery.Read([secondary]); discovery.Read([primary, secondary]);
                Check(roots == stableRoots + 1, "A removed/re-added taskbar retained its old provider.");
                var restarted = primary with { Started = started + 1 };
                discovery.Read([restarted, secondary]);
                Check(roots == stableRoots + 2, "A replaced Explorer lifetime reused an old provider.");
                discovery.Read([primary with { ProcessId = int.MaxValue }]);
                Check(roots == stableRoots + 2, "A mismatched window owner reached accessibility providers.");
                failNext = true;
                Check(discovery.Read([primary]).Length == 0, "Unavailable accessibility did not fail closed.");
                var failedRoots = roots;
                discovery.Read([primary]);
                Check(roots == failedRoots + 1, "An unavailable provider poisoned later discovery.");
                discovery.Clear(); discovery.Read([primary, secondary]);
                var warmRoots = roots;
                for (var i = 0; i < 2000; i++) discovery.Read([primary, secondary]);
                long Sample()
                {
                    // Measure retained/native growth after collection only in this
                    // isolated regression fixture. Production never forces GC.
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    process.Refresh(); return process.PrivateMemorySize64;
                }
                var warm = Sample();
                var halfway = warm;
                for (var i = 0; i < iterations; i++)
                {
                    discovery.Read([primary, secondary]);
                    if (i + 1 == iterations / 2) halfway = Sample();
                }
                var final = Sample();
                Check(roots == warmRoots, "The repeated-poll soak allocated more taskbar roots.");
                Check(final - halfway < 16 * 1024 * 1024, "Native memory continued growing after discovery warm-up.");
                discovery.Clear();
                return new { iterations, taskbars = 2, rootsDuringSoak = roots - warmRoots, warmPrivateBytes = warm, halfwayPrivateBytes = halfway, finalPrivateBytes = final };
            });
            // Exercise the real worker's enable/disable/dispose races, not only its scanner.
            var calls = 0; var workerRoots = 0;
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var finished = new ManualResetEventSlim();
            var locator = new StartButtonLocator(() =>
            {
                if (Interlocked.Increment(ref calls) == 1) { entered.Set(); release.Wait(TimeSpan.FromSeconds(10)); }
                return new[] { visual };
            }, window => { Interlocked.Increment(ref workerRoots); return AutomationElement.FromHandle(window); });
            try
            {
                locator.SetEnabled(true);
                Check(await Task.Run(() => entered.Wait(10000)), "Discovery worker did not start.");
                locator.SetEnabled(false); locator.SetEnabled(true); release.Set();
                var deadline = Environment.TickCount64 + 10000;
                while (locator.Targets.Count == 0 && Environment.TickCount64 < deadline) await Task.Delay(20);
                Check(locator.Targets.Count == 1 && calls >= 2 && workerRoots == 2, "Re-enabled discovery published an earlier settings lifetime.");
                locator.SetEnabled(false);
                Check(locator.Targets.Count == 0, "Disabling Start capture retained hit-test targets.");
                locator.Dispose(); locator.SetEnabled(true); locator.Dispose();
                Check(locator.Targets.Count == 0, "Disposed discovery could be re-enabled.");
            }
            finally { release.Set(); locator.Dispose(); }
            // A stale/destroyed HWND must be discarded without affecting the surviving taskbar.
            var dead = first.Source.Handle;
            first.Dispose();
            await Task.Run(() =>
            {
                var discovery = new StartButtonDiscovery();
                Check(discovery.Read([primary, secondary]).Length == 0, "A destroyed taskbar produced stale targets.");
                discovery.Clear(); finished.Set();
            });
            Check(finished.IsSet && dead != 0, "Owned discovery cleanup did not finish.");
            File.WriteAllText(report, JsonSerializer.Serialize(new { success = true, checks, measurements }));
            return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(report, JsonSerializer.Serialize(new { success = false, checks, error = error.GetType().Name + ": " + error.Message }));
            return 1;
        }
    }
}
