using ReStart.App.Services;
using System.ComponentModel;

namespace ReStart.Core.Tests;

public sealed class TaskbarLeaseTests
{
    private sealed class Guard(Action release) : IDisposable { public void Dispose() => release(); }
    private sealed class Store : ITaskbarRecoveryStore
    {
        internal TaskbarLeaseState? State;
        internal bool FailSave, CorruptSave, FailDelete;
        public IDisposable Acquire() => new Guard(() => { });
        public TaskbarLeaseState? Read() => State;
        public void Save(TaskbarLeaseState state)
        {
            if (FailSave) throw new UnauthorizedAccessException("Owned failure");
            if (State != null) throw new IOException("Existing owned transaction");
            State = CorruptSave ? state with { Token = "invalid" } : state;
        }
        public void Update(TaskbarLeaseState state) { Assert.Equal(state.Token, State?.Token); State = state; }
        public void Delete(string token)
        {
            if (FailDelete) throw new IOException("Owned cleanup failure");
            Assert.Equal(token, State?.Token); State = null;
        }
    }
    private sealed class Backend : ITaskbarLeaseBackend
    {
        internal int Applied, Restored;
        internal bool Current = true, FailApply, FailRestore, Unavailable;
        public TaskbarLeaseState? Capture(TaskbarLeaseMode mode) => Unavailable ? null : new(1, Guid.NewGuid().ToString("N"), 1, 1, mode,
            [new(1, 2, 1, true, true, new(0, 0, 1280, 800), new(0, 798, 1280, 48), new(0, 752, 1280, 48))]);
        public bool IsCurrent(TaskbarLeaseState state) => Current;
        public void Apply(TaskbarLeaseState state, Action armRecovery) { armRecovery(); Applied++; if (FailApply) throw new IOException("Owned partial failure"); }
        public void Restore(TaskbarLeaseState state) { Restored++; if (FailRestore) throw new IOException("Owned recovery failure"); }
    }

    private sealed class Tray
    {
        internal readonly List<(uint Message, nint Parameter)> Messages = [];
        internal bool Activated, TimerHeld, MenuOpen;
        internal Action<uint, nint>? Sending;

        internal void Send(nint window, uint message, nint parameter, nint data)
        {
            Messages.Add((message, parameter));
            Sending?.Invoke(message, parameter);
            Deliver(message, parameter);
        }

        internal void Deliver(uint message, nint parameter)
        {
            if (message == 0x0006) Activated = parameter != 0;
            if (message == 0x0211) TimerHeld = true;
            if (message == 0x0212) TimerHeld = false;
        }
    }

    private sealed class TimerBackend(Tray tray) : ITaskbarLeaseBackend
    {
        private readonly TaskbarNativeBackend _native = new(sendMessage: tray.Send, menuActive: _ => tray.MenuOpen) { LauncherWindow = 3 };
        public TaskbarLeaseState Capture(TaskbarLeaseMode mode) => new(2, Guid.NewGuid().ToString("N"), 1, 1, mode,
            [new(1, 2, 1, true, true, new(0, 0, 1280, 800), new(0, 798, 1280, 48), new(0, 752, 1280, 48), HoldAutoHide: true)]);
        public bool IsCurrent(TaskbarLeaseState state) => true;
        public void Apply(TaskbarLeaseState state, Action armRecovery) => _native.HoldTimer(1, reveal: true, armRecovery);
        public void Restore(TaskbarLeaseState state) => _native.ReleaseTimer(1, interacting: false, foreground: 3);
    }

    [Theory]
    [InlineData(0)] // Watchdog startup fails.
    [InlineData(1)] // Launcher request is cancelled.
    [InlineData(2)] // Watchdog fails its final readiness check.
    public void FailedPreparationDoesNotSendMessagesEvenIfAnOrphanedRecordRemains(int failure)
    {
        var store = new Store { FailDelete = true };
        var tray = new Tray();
        using var lease = new TaskbarLease(store, new TimerBackend(tray), _ => failure == 0
            ? throw new IOException("Owned watchdog startup failure") : new Guard(() => { }), () => failure != 2);
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Reveal, () => failure != 1));
        Assert.Equal(TaskbarRecoveryPhase.Prepared, store.State!.Phase);
        Assert.Empty(tray.Messages);

        store.FailDelete = false;
        Assert.True(TaskbarRecovery.Run(store, new TimerBackend(tray), (_, _) => false));
        Assert.Null(store.State);
        Assert.Empty(tray.Messages);
    }

    [Theory]
    [InlineData(0x0006, false)]
    [InlineData(0x0006, true)]
    [InlineData(0x0211, false)]
    [InlineData(0x0211, true)]
    public void AcquisitionTimeoutRecoversBeforeTheNextMonitoringApply(int timedOutMessage, bool delivered)
    {
        var store = new Store();
        var tray = new Tray();
        using var lease = new TaskbarLease(store, new TimerBackend(tray), _ => new Guard(() => { }));
        tray.Sending = (message, parameter) =>
        {
            Assert.Equal(TaskbarRecoveryPhase.RestorePending, store.State!.Phase);
            if (message != timedOutMessage) return;
            if (delivered) tray.Deliver(message, parameter);
            tray.MenuOpen = true;
            tray.Sending = null;
            throw new Win32Exception(1460);
        };
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Reveal));
        Assert.Equal(timedOutMessage == 0x0211 || delivered, tray.Activated);
        Assert.Equal(delivered && timedOutMessage == 0x0211, tray.TimerHeld);
        Assert.Equal(TaskbarRecoveryPhase.RestorePending, store.State!.Phase);
        var attempts = tray.Messages.Count;
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Reveal));
        Assert.Equal(attempts, tray.Messages.Count);

        tray.MenuOpen = false;
        Assert.True(lease.Synchronize(TaskbarLeaseMode.Reveal));
        Assert.Equal(new (uint, nint)[] { (0x0006, 0), (0x0212, 1), (0x0006, 0x10001), (0x0211, 1) }, tray.Messages.Skip(attempts));
        Assert.True(lease.Synchronize(null));
        Assert.False(tray.Activated);
        Assert.False(tray.TimerHeld);
        Assert.Null(store.State);
    }

    [Fact]
    public void FreshWatchdogRetriesDeletionWithoutSendingAnotherRelease()
    {
        var store = new Store();
        var tray = new Tray();
        using var lease = new TaskbarLease(store, new TimerBackend(tray), _ => new Guard(() => { }));
        Assert.True(lease.Synchronize(TaskbarLeaseMode.Reveal));
        store.FailDelete = true;
        Assert.False(lease.Synchronize(null));
        Assert.False(tray.TimerHeld);
        Assert.Equal(TaskbarRecoveryPhase.Restored, store.State!.Phase);

        var messages = tray.Messages.ToArray();
        tray.MenuOpen = true;
        Assert.False(TaskbarRecovery.Run(store, new TimerBackend(tray), (_, _) => false));
        store.FailDelete = false;
        Assert.True(TaskbarRecovery.Run(store, new TimerBackend(tray), (_, _) => false));
        Assert.Null(store.State);
        Assert.Equal(messages, tray.Messages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshWatchdogDefersAnUncertainReleaseWhileExplorerMenuIsOpen(bool delivered)
    {
        var store = new Store();
        var tray = new Tray();
        using var lease = new TaskbarLease(store, new TimerBackend(tray), _ => new Guard(() => { }));
        Assert.True(lease.Synchronize(TaskbarLeaseMode.Reveal));
        tray.Sending = (message, _) =>
        {
            if (message != 0x0212) return;
            if (delivered) tray.TimerHeld = false;
            tray.Sending = null;
            throw new Win32Exception(1460);
        };
        Assert.False(lease.Synchronize(null));
        Assert.Equal(!delivered, tray.TimerHeld);
        Assert.Equal(TaskbarRecoveryPhase.RestorePending, store.State!.Phase);

        var messages = tray.Messages.ToArray();
        tray.MenuOpen = true;
        Assert.False(TaskbarRecovery.Run(store, new TimerBackend(tray), (_, _) => false));
        Assert.False(TaskbarRecovery.Run(store, new TimerBackend(tray), (_, _) => false));
        Assert.Equal(messages, tray.Messages);
        tray.MenuOpen = false;
        Assert.True(TaskbarRecovery.Run(store, new TimerBackend(tray), (_, _) => false));
        Assert.False(tray.TimerHeld);
        Assert.Null(store.State);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedOrCorruptRecoveryWritesNeverReachShell(bool corrupt)
    {
        var store = new Store { FailSave = !corrupt, CorruptSave = corrupt }; var backend = new Backend(); var guards = 0;
        using var lease = new TaskbarLease(store, backend, _ => { guards++; return new Guard(() => { }); });
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Hide));
        Assert.Equal(0, backend.Applied); Assert.Equal(0, backend.Restored); Assert.Equal(0, guards);
    }
    [Fact]
    public void LossOfAnEstablishedGuardRestoresInsteadOfApplyingAnotherMutation()
    {
        var store = new Store(); var backend = new Backend(); var ready = true;
        using var lease = new TaskbarLease(store, backend, _ => new Guard(() => { }), () => ready);
        Assert.True(lease.Synchronize(TaskbarLeaseMode.Hide)); Assert.Equal(1, backend.Applied);
        ready = false;
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Hide));
        Assert.Equal(1, backend.Applied); Assert.Equal(1, backend.Restored); Assert.Null(store.State);
        ready = true;
        Assert.True(lease.Synchronize(TaskbarLeaseMode.Hide)); Assert.Equal(2, backend.Applied);
    }
    [Fact]
    public void ClosingOrChangingScopeDuringWatchdogReadinessPreventsLateMutation()
    {
        var store = new Store(); var backend = new Backend(); var requested = true; var released = false;
        using var lease = new TaskbarLease(store, backend, _ => { requested = false; return new Guard(() => released = true); });
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Reveal, () => requested));
        Assert.Equal(0, backend.Applied); Assert.Equal(0, backend.Restored); Assert.Null(store.State); Assert.Null(lease.Snapshot); Assert.True(released);
    }
    [Fact]
    public void MissingWatchdogPreventsMutationAndCleansValidRecoveryState()
    {
        var store = new Store(); var backend = new Backend();
        using var lease = new TaskbarLease(store, backend, _ => throw new IOException("Owned readiness failure"));
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Hide));
        Assert.Equal(0, backend.Applied); Assert.Equal(0, backend.Restored); Assert.Null(store.State); Assert.False(lease.IsActive);
    }
    [Fact]
    public void PartialMutationIsRestoredAndCanRetryWithoutLosingOriginalState()
    {
        var store = new Store(); var backend = new Backend { FailApply = true }; var released = 0;
        using var lease = new TaskbarLease(store, backend, _ => new Guard(() => released++));
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Hide));
        Assert.Equal(1, backend.Restored); Assert.Null(store.State); Assert.Equal(1, released);
        backend.FailApply = false;
        Assert.True(lease.Synchronize(TaskbarLeaseMode.Hide));
        Assert.True(lease.Synchronize(null)); Assert.Equal(2, released);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedRestoreRetainsMarkerAndProtectionForRetry(bool deletionFailure)
    {
        var store = new Store(); var backend = new Backend(); var released = false;
        using var lease = new TaskbarLease(store, backend, _ => new Guard(() => released = true));
        Assert.True(lease.Synchronize(TaskbarLeaseMode.Hide));
        store.FailDelete = deletionFailure; backend.FailRestore = !deletionFailure;
        Assert.False(lease.Synchronize(null));
        Assert.True(lease.RecoveryFailed); Assert.True(lease.IsActive); Assert.NotNull(store.State); Assert.False(released);
        Assert.Null(lease.Snapshot); // Recovery ownership is not proof that the taskbar is still hidden/revealed.
        store.FailDelete = backend.FailRestore = false;
        Assert.True(lease.Synchronize(null)); Assert.Null(store.State); Assert.True(released);
    }
    [Fact]
    public void RepeatedEnforcementAndPolicyChangesKeepExactlyOneVerifiedTransaction()
    {
        var store = new Store(); var backend = new Backend(); var acquired = 0; var released = 0;
        using (var lease = new TaskbarLease(store, backend, _ => { acquired++; return new Guard(() => released++); }))
        {
            Assert.True(lease.Synchronize(TaskbarLeaseMode.Reveal)); var token = store.State!.Token;
            Assert.True(lease.Synchronize(TaskbarLeaseMode.Reveal)); Assert.Equal(token, store.State.Token); Assert.Equal(1, acquired);
            Assert.True(lease.Synchronize(TaskbarLeaseMode.Hide)); Assert.NotEqual(token, store.State.Token); Assert.Equal(2, acquired);
            backend.Current = false;
            Assert.True(lease.Synchronize(TaskbarLeaseMode.Hide)); Assert.Equal(3, acquired);
        }
        Assert.Equal(3, backend.Restored); Assert.Equal(3, released); Assert.Null(store.State);
    }
    [Fact]
    public void ExistingOrUnavailableStateIsNeverOverwritten()
    {
        var store = new Store(); var backend = new Backend(); store.State = backend.Capture(TaskbarLeaseMode.Hide);
        var previous = store.State;
        using var lease = new TaskbarLease(store, backend, _ => new Guard(() => { }));
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Reveal)); Assert.Same(previous, store.State); Assert.Equal(0, backend.Applied);
        store.State = null; backend.Unavailable = true;
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Reveal)); Assert.Null(store.State);
    }
}
