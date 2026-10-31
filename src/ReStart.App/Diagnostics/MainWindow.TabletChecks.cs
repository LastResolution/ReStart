using System.Text.Json;
using System.Windows.Interop;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
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
        var input = new TabletInputSnapshot(DevicePosture.Slate, KeyboardPresence.Absent);
        var reads = 0;
        var dependencies = new TabletIntegration.Dependencies(store, backend, b => backend.Scope = b,
            _ => new TabletCheckLease(), () => { reads++; return input; }, () => true);
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
            await Settle(() => !integration.TabletActive && integration.CurrentMode == null);
            Check(reads == 0 && backend.Mutations == 0, "Default-off tablet initialization read devices or changed the shell.");
            preferences.TabletFeaturesEnabled = preferences.TabletHideTaskbar = true;
            await Settle(() => integration.CurrentMode == TaskbarLeaseMode.Hide);
            Check(store.State != null && backend.Applied?.Windows.All(w => w.Monitor == backend.Scope) == true,
                "Tablet effects escaped the fixture monitor or recovery transaction.");
            preferences.TabletKeyboardMeansDesktop = true; input = input with { Keyboard = KeyboardPresence.Present };
            await Settle(() => !integration.TabletActive && integration.CurrentMode == null);
            Check(store.State == null && backend.Applied == null, "A current keyboard did not restore tablet effects.");
            input = input with { Keyboard = KeyboardPresence.Unknown };
            await Settle(() => !integration.TabletActive);
            input = input with { Keyboard = KeyboardPresence.Absent };
            await Settle(() => integration.CurrentMode == TaskbarLeaseMode.Hide);
            preferences.TabletFeaturesEnabled = false; preferences.ShowTaskbarWhileOpen = true;
            await Settle(() => !integration.TabletActive && integration.CurrentMode == TaskbarLeaseMode.Reveal);
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
