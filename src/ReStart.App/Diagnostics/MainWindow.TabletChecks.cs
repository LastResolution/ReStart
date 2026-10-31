using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    [StructLayout(LayoutKind.Sequential)] private struct TaskbarCheckPosition { public nint Window, After; public int X, Y, Width, Height; public uint Flags; }

    private int VerifyTaskbarPositionCoercion()
    {
        var window = new Window { Width = 240, Height = 120, ShowActivated = false, ShowInTaskbar = false, Topmost = false };
        window.Show(); window.UpdateLayout();
        var handle = new WindowInteropHelper(window).Handle;
        var source = HwndSource.FromHwnd(handle);
        Native.GetWindowRect(handle, out var before);
        var original = new TaskbarBounds(before.Left, before.Top, before.Right - before.Left, before.Bottom - before.Top);
        var target = original with { X = original.X + 8, Y = original.Y + 8 };
        var coerced = 0;
        nint Clamp(nint _, int message, nint wParam, nint data, ref bool handled)
        {
            if (message == 0x46)
            {
                var position = Marshal.PtrToStructure<TaskbarCheckPosition>(data);
                position.X = original.X; position.Y = original.Y;
                Marshal.StructureToPtr(position, data, false); coerced++;
            }
            return 0;
        }
        source.AddHook(Clamp);
        try
        {
            Native.SetWindowPos(handle, 0, target.X, target.Y, target.Width, target.Height, 0x0014);
            Native.GetWindowRect(handle, out var rejected);
            Require(coerced > 0 && rejected.Left == original.X && rejected.Top == original.Y, "The owned taskbar fixture did not reproduce position coercion.");
            var calls = coerced;
            TaskbarNativeBackend.Position(handle, target, false);
            Native.GetWindowRect(handle, out var applied);
            Require(coerced == calls && applied.Left == target.X && applied.Top == target.Y, "The taskbar lease position was silently coerced instead of applied.");
            // Shell owns the release decision. The fixture deliberately selects
            // a different position than the saved one, as a changed preference
            // or display would. Recovery must not suppress that decision.
            var releaseTarget = original with { Y = original.Y + 24 };
            source.RemoveHook(Clamp);
            nint Reconcile(nint _, int message, nint wParam, nint data, ref bool handled)
            {
                if (message == 0x46)
                {
                    var position = Marshal.PtrToStructure<TaskbarCheckPosition>(data);
                    position.X = releaseTarget.X; position.Y = releaseTarget.Y;
                    Marshal.StructureToPtr(position, data, false); coerced++;
                }
                return 0;
            }
            source.AddHook(Reconcile);
            try { TaskbarNativeBackend.Handoff(handle, target, null); }
            finally { source.RemoveHook(Reconcile); source.AddHook(Clamp); }
            Native.GetWindowRect(handle, out var restored);
            Require(coerced > calls && restored.Left == releaseTarget.X && restored.Top == releaseTarget.Y,
                "Taskbar release bypassed the owning window's current position decision.");
            var cover = new Window { Left = window.Left, Top = window.Top, Width = 240, Height = 120, ShowActivated = false, ShowInTaskbar = false, Topmost = true };
            try
            {
                TaskbarNativeBackend.Position(handle, original, false, topmost: false);
                cover.Show(); cover.UpdateLayout();
                var coverHandle = new WindowInteropHelper(cover).Handle;
                Require(TaskbarNativeBackend.IsAbove(coverHandle, handle), "The owned fullscreen cover did not start above the taskbar fixture.");
                TaskbarNativeBackend.Position(handle, original, false);
                Require(!TaskbarNativeBackend.IsAbove(handle, coverHandle), "The old coordinate-only path unexpectedly changed stacking order.");
                var foreground = Native.GetForegroundWindow();
                TaskbarNativeBackend.Position(handle, original, true, topmost: true);
                Require(TaskbarNativeBackend.IsAbove(handle, coverHandle), "The taskbar fixture was still behind the fullscreen cover after reveal.");
                Require(Native.GetForegroundWindow() == foreground, "Revealing the taskbar fixture stole foreground activation.");
                TaskbarNativeBackend.Position(coverHandle, original, false, topmost: true);
                Require(TaskbarNativeBackend.IsAbove(coverHandle, handle), "The repeated cover did not reproduce topmost taskbar occlusion.");
                TaskbarNativeBackend.Position(handle, original, true, topmost: true);
                Require(TaskbarNativeBackend.IsAbove(handle, coverHandle), "An already-topmost taskbar fixture was not brought back in front.");
                Require(Native.GetForegroundWindow() == foreground, "Repeated taskbar reveal stole foreground activation.");
                TaskbarNativeBackend.Position(handle, original, false, topmost: false);
                Require(TaskbarNativeBackend.IsAbove(coverHandle, handle), "The taskbar fixture did not return to its original ordinary tier.");
                return 11;
            }
            finally { cover.Close(); }
        }
        finally { source.RemoveHook(Clamp); window.Close(); }
    }
    private sealed class TabletCheckLease : IDisposable { public void Dispose() { } }
    private sealed class TabletCheckStore : ITaskbarRecoveryStore
    {
        internal TaskbarLeaseState? State;
        internal bool FailSave;
        public IDisposable Acquire() => new TabletCheckLease();
        public TaskbarLeaseState? Read() => State;
        public void Save(TaskbarLeaseState state) { if (FailSave) throw new IOException("Owned write failure."); State = state; }
        public void Delete(string token) { Require(State?.Token == token, "Owned tablet recovery changed owner."); State = null; }
    }
    private sealed class TabletCheckBackend : ITaskbarLeaseBackend
    {
        internal TaskbarBounds Scope;
        internal TaskbarLeaseState? Applied;
        internal int Mutations, Restores;
        public TaskbarLeaseState? Capture(TaskbarLeaseMode mode) => new(1, Guid.NewGuid().ToString("N"), 1, 1, mode,
            [new(1, 1, 1, true, true, Scope, new(Scope.X, Scope.Y + Scope.Height - 2, Scope.Width, 40), new(Scope.X, Scope.Y + Scope.Height - 40, Scope.Width, 40))]);
        public bool IsCurrent(TaskbarLeaseState state) => state.Windows[0].Monitor == Scope;
        public void Apply(TaskbarLeaseState state) { Applied = state; Mutations++; }
        public void Restore(TaskbarLeaseState state) { Applied = null; Restores++; }
    }

    /// <summary>Exercises actual tablet initialization through complete inert dependencies, including the lifecycle skipped by ordinary preview startup.</summary>
    private static async Task<int> VerifyTabletIntegrationAsync(MainWindow fixture)
    {
        var checks = 0;
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        var saved = fixture.Document.Preferences;
        var preferences = fixture.Document.Preferences = new Preferences { Animate = false, ShowTaskbarWhileOpen = false, CaptureWindowsKey = true, CaptureStartButton = true };
        var layoutBefore = JsonSerializer.Serialize(fixture.Document.Groups);
        var backend = new TabletCheckBackend(); var store = new TabletCheckStore();
        var dependencies = new TabletIntegration.Dependencies(store, backend, b => backend.Scope = b,
            _ => new TabletCheckLease(), () => true, () => true);
        TabletIntegration? integration = null;
        async Task Settle(Func<bool> expected)
        {
            integration!.DeviceStateChanged();
            var deadline = Environment.TickCount64 + 3000;
            while ((integration.IsBusy || !expected()) && Environment.TickCount64 < deadline) await Task.Delay(20);
            Check(!integration.IsBusy && expected(), "The owned tablet integration did not settle.");
        }
        try
        {
            integration = new(fixture, HwndSource.FromHwnd(new WindowInteropHelper(fixture).EnsureHandle()), dependencies);
            await Settle(() => integration.CurrentMode == null);
            Check(backend.Mutations == 0, "The disabled reveal preference changed the shell.");
            preferences.TabletFeaturesEnabled = preferences.TabletHideTaskbar = true;
            await Settle(() => integration.CurrentMode == null);
            Check(backend.Mutations == 0, "Legacy tablet hide settings created a new taskbar hide lease.");
            preferences.ShowTaskbarWhileOpen = true;
            await Settle(() => integration.CurrentMode == TaskbarLeaseMode.Reveal);
            Check(store.State != null && backend.Applied?.Windows.All(w => w.Monitor == backend.Scope) == true,
                "Tablet effects escaped the fixture monitor or recovery transaction.");
            preferences.TabletKeyboardMeansDesktop = true;
            await Settle(() => integration.CurrentMode == TaskbarLeaseMode.Reveal);
            preferences.ShowTaskbarWhileOpen = false;
            await Settle(() => integration.CurrentMode == null);
            Check(store.State == null && backend.Applied == null, "Disabling reveal did not release its effect.");
            preferences.TabletFeaturesEnabled = false; preferences.ShowTaskbarWhileOpen = true;
            await Settle(() => integration.CurrentMode == TaskbarLeaseMode.Reveal);
            Check(preferences.CaptureWindowsKey && preferences.CaptureStartButton, "Tablet transitions rewrote desktop Start preferences.");
            Check(JsonSerializer.Serialize(fixture.Document.Groups) == layoutBefore, "Tablet initialization or transitions reseeded a layout.");
            fixture.Hide();
            await Settle(() => integration.CurrentMode == null);
            Check(store.State == null && backend.Applied == null, "Closing the launcher stranded its taskbar effect.");
            store.FailSave = true; fixture.Present();
            var mutations = backend.Mutations;
            await Settle(() => integration.CurrentMode == null);
            Check(backend.Mutations == mutations, "A failed recovery save still mutated the shell.");
            store.FailSave = false;
            await Settle(() => integration.CurrentMode == TaskbarLeaseMode.Reveal);
            integration.Dispose();
            var deadline = Environment.TickCount64 + 3000;
            while (store.State != null && Environment.TickCount64 < deadline) await Task.Delay(20);
            Check(store.State == null && backend.Applied == null && backend.Restores >= 3, "Disposal did not restore owned effects.");
            return checks;
        }
        finally { integration?.Dispose(); fixture.Document.Preferences = saved; }
    }
}
