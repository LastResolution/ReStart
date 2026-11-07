using ReStart.App.Services;

namespace ReStart.Core.Tests;

public sealed class TaskbarLeaseTests
{
    private sealed class ContendedBackend : ITaskbarLeaseBackend
    {
        internal int Applied, Restored;
        internal bool Visible, FailAfterMutation, FailRestore, SurfaceChanged;
        internal long Window = 1, Started = 1;
        internal TaskbarBounds Monitor = new(0, 0, 1280, 800);
        public TaskbarLeaseState? Capture(TaskbarLeaseMode mode) => new(1, Guid.NewGuid().ToString("N"), 1, 1, mode,
            [new(Window, 2, Started, Visible, true, Monitor, new(Monitor.X, Monitor.Y + (Visible ? 752 : 798), 1280, 48),
                new(Monitor.X, Monitor.Y + 752, 1280, 48), Clip: SurfaceChanged ? new(true, [new(0, 0, 1280, 2)]) : new(false, []))]);
        public bool IsCurrent(TaskbarLeaseState state) => Visible && !SurfaceChanged && state.Windows[0].Window == Window
            && state.Windows[0].ProcessStarted == Started && state.Windows[0].Monitor == Monitor;
        public void Apply(TaskbarLeaseState state)
        {
            Applied++; Visible = true; SurfaceChanged = false;
            if (FailAfterMutation) throw new IOException("Owned shell refusal after position change");
        }
        public void Restore(TaskbarLeaseState state) { Restored++; if (FailRestore) throw new IOException("Owned restore failure"); }
    }

    [Fact]
    public void MonitoringAnAcceptedRevealDoesNotRepeatedlyReapplyIt()
    {
        var store = new Store(); var backend = new ContendedBackend();
        using var lease = new TaskbarLease(store, backend, _ => new Guard(() => { }));
        Assert.True(lease.Synchronize(TaskbarLeaseMode.Reveal));
        for (var tick = 0; tick < 100; tick++) Assert.True(lease.Synchronize(TaskbarLeaseMode.Reveal));
        Assert.Equal(1, backend.Applied); Assert.NotNull(lease.Snapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplorerHidingOrReclippingTheTrayDoesNotStartARevealLoop(bool reclip)
    {
        var store = new Store(); var backend = new ContendedBackend(); var released = 0;
        using var lease = new TaskbarLease(store, backend, _ => new Guard(() => released++));
        Assert.True(lease.Synchronize(TaskbarLeaseMode.Reveal));
        backend.Visible = false; backend.SurfaceChanged = reclip;
        for (var tick = 0; tick < 100; tick++) Assert.False(lease.Synchronize(TaskbarLeaseMode.Reveal));
        Assert.False(backend.Visible); Assert.Equal(1, backend.Applied); Assert.Equal(1, backend.Restored);
        Assert.Equal(1, released); Assert.Null(store.State); Assert.Null(lease.Snapshot); Assert.False(lease.RecoveryFailed);
        // Pointer-triggered native reveals are not a fresh request to force it.
        backend.Visible = true;
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Reveal)); Assert.Equal(1, backend.Applied);
        Assert.True(lease.Synchronize(null));
        Assert.True(lease.Synchronize(TaskbarLeaseMode.Reveal)); Assert.Equal(2, backend.Applied);
    }

    [Fact]
    public void APartiallyRejectedRevealIsNotRetriedUntilAnotherOpenRequest()
    {
        var store = new Store(); var backend = new ContendedBackend { FailAfterMutation = true };
        using var lease = new TaskbarLease(store, backend, _ => new Guard(() => { }));
        for (var tick = 0; tick < 100; tick++) Assert.False(lease.Synchronize(TaskbarLeaseMode.Reveal));
        Assert.Equal(1, backend.Applied); Assert.Equal(1, backend.Restored); Assert.Null(store.State);
        backend.FailAfterMutation = false;
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Reveal));
        Assert.True(lease.Synchronize(null)); Assert.True(lease.Synchronize(TaskbarLeaseMode.Reveal));
        Assert.Equal(2, backend.Applied);
    }

    [Fact]
    public void FailedRecoveryAfterAShellVetoRetainsProtectionWithoutAnotherReveal()
    {
        var store = new Store(); var backend = new ContendedBackend(); var released = 0;
        using var lease = new TaskbarLease(store, backend, _ => new Guard(() => released++));
        Assert.True(lease.Synchronize(TaskbarLeaseMode.Reveal));
        backend.Visible = false; backend.FailRestore = true;
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Reveal));
        Assert.True(lease.RecoveryFailed); Assert.NotNull(store.State); Assert.Null(lease.Snapshot); Assert.Equal(0, released);
        backend.FailRestore = false;
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Reveal));
        Assert.False(lease.RecoveryFailed); Assert.Null(store.State); Assert.Equal(1, released); Assert.Equal(1, backend.Applied);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ANewMonitorOrExplorerLifetimeCanAcquireItsOwnReveal(bool restart)
    {
        var store = new Store(); var backend = new ContendedBackend();
        using var lease = new TaskbarLease(store, backend, _ => new Guard(() => { }));
        Assert.True(lease.Synchronize(TaskbarLeaseMode.Reveal));
        backend.Visible = false;
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Reveal));
        if (restart) backend.Started++;
        else backend.Monitor = new(-1280, 0, 1280, 800);
        Assert.True(lease.Synchronize(TaskbarLeaseMode.Reveal));
        Assert.Equal(2, backend.Applied); Assert.Equal(backend.Monitor, Assert.Single(lease.Snapshot!.Windows).Monitor);
    }

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
        public void Apply(TaskbarLeaseState state) { Applied++; if (FailApply) throw new IOException("Owned partial failure"); }
        public void Restore(TaskbarLeaseState state) { Restored++; if (FailRestore) throw new IOException("Owned recovery failure"); }
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
        Assert.Equal(0, backend.Applied); Assert.Null(store.State); Assert.Null(lease.Snapshot); Assert.True(released);
    }
    [Fact]
    public void MissingWatchdogPreventsMutationAndCleansValidRecoveryState()
    {
        var store = new Store(); var backend = new Backend();
        using var lease = new TaskbarLease(store, backend, _ => throw new IOException("Owned readiness failure"));
        Assert.False(lease.Synchronize(TaskbarLeaseMode.Hide));
        Assert.Equal(0, backend.Applied); Assert.Null(store.State); Assert.False(lease.IsActive);
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
