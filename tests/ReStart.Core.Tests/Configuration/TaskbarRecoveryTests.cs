using ReStart.App.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ReStart.Core.Tests;

public sealed class TaskbarRecoveryTests
{
    [Fact]
    public void ContentVisibilityRoundTripsAndOlderRecordsRemainReadable()
    {
        var state = new TaskbarLeaseState(1, Guid.NewGuid().ToString("N"), 1, 2, TaskbarLeaseMode.Reveal,
            [new(1, 1, 1, true, true, new(0, 0, 800, 600), new(0, 598, 800, 40), new(0, 560, 800, 40), Content: [new(2, false)])]);
        var json = JsonSerializer.SerializeToNode(state)!;
        var roundTrip = json.Deserialize<TaskbarLeaseState>()!;
        Assert.True(roundTrip.IsValid());
        Assert.False(Assert.Single(roundTrip.Windows[0].Content!).Visible);
        json["Windows"]![0]!.AsObject().Remove("Content");
        var older = json.Deserialize<TaskbarLeaseState>()!;
        Assert.True(older.IsValid());
        Assert.Null(older.Windows[0].Content);
        Assert.False((state with { Windows = [state.Windows[0] with { Content = [new(2, true), new(2, false)] }] }).IsValid());
        Assert.False((state with { Windows = [state.Windows[0] with { Content = [new(1, false)] }] }).IsValid());
        Assert.False((state with { Windows = [state.Windows[0] with { Content = [new(0, false)] }] }).IsValid());
        Assert.False((state with { Windows = [state.Windows[0] with { Content = [new(2, false), new(3, false), new(4, false), new(5, false), new(6, false)] }] }).IsValid());
    }
    [Fact]
    public void OlderRecoveryRecordsDoNotInventAnOriginalStackingTier()
    {
        var state = new TaskbarLeaseState(1, Guid.NewGuid().ToString("N"), 1, 2, TaskbarLeaseMode.Reveal,
            [new(1, 1, 1, true, true, new(0, 0, 800, 600), new(0, 598, 800, 40), new(0, 560, 800, 40), false)]);
        var json = JsonSerializer.SerializeToNode(state)!;
        json["Windows"]![0]!.AsObject().Remove("Topmost");
        var recovered = json.Deserialize<TaskbarLeaseState>()!;
        Assert.True(recovered.IsValid());
        Assert.Null(Assert.Single(recovered.Windows).Topmost);
        Assert.Null(TaskbarNativeBackend.RestoreTopmost(recovered.Windows[0].Topmost, true));
        Assert.False(Assert.Single(JsonSerializer.Deserialize<TaskbarLeaseState>(JsonSerializer.Serialize(state))!.Windows).Topmost);
    }
    private sealed class Guard : IDisposable { public void Dispose() { } }
    private sealed class Store : ITaskbarRecoveryStore
    {
        internal TaskbarLeaseState? State = new(1, Guid.NewGuid().ToString("N"), 1, 2, TaskbarLeaseMode.Hide,
            [new(1, 1, 1, true, false, new(0, 0, 800, 600), new(0, 560, 800, 40), new(0, 560, 800, 40))]);
        internal bool Locked, FailRead, FailDelete;
        public IDisposable Acquire() { if (Locked) throw new IOException(); return new Guard(); }
        public TaskbarLeaseState? Read() { if (FailRead) throw new InvalidDataException(); return State; }
        public void Save(TaskbarLeaseState state) => throw new NotSupportedException();
        public void Delete(string token) { if (FailDelete) throw new IOException(); Assert.Equal(State?.Token, token); State = null; }
    }
    private sealed class Backend : ITaskbarLeaseBackend
    {
        internal int Restored;
        internal bool Fail;
        public TaskbarLeaseState? Capture(TaskbarLeaseMode mode) => throw new NotSupportedException();
        public bool IsCurrent(TaskbarLeaseState state) => throw new NotSupportedException();
        public void Apply(TaskbarLeaseState state) => throw new NotSupportedException();
        public void Restore(TaskbarLeaseState state) { Restored++; if (Fail) throw new IOException(); }
    }
    [Fact]
    public void StartupRecoversOnlyOrphansAndNeverOverwritesLiveOwners()
    {
        var store = new Store(); var backend = new Backend();
        Assert.False(TaskbarRecovery.Run(store, backend, (_, _) => true));
        Assert.NotNull(store.State); Assert.Equal(0, backend.Restored);
        Assert.True(TaskbarRecovery.Run(store, backend, (_, _) => false));
        Assert.Null(store.State); Assert.Equal(1, backend.Restored);
        Assert.True(TaskbarRecovery.Run(store, backend, (_, _) => false)); Assert.Equal(1, backend.Restored);
    }
    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 2)]
    public void OldWatchdogNeverRestoresANewerProcessTransaction(int id, long started)
    {
        var store = new Store(); var backend = new Backend();
        Assert.True(TaskbarRecovery.Run(store, backend, (_, _) => false, (id, started)));
        Assert.NotNull(store.State); Assert.Equal(0, backend.Restored);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void InvalidPersistenceAndPartialRecoveryPreserveEvidenceForRetry(int failure)
    {
        var store = new Store { Locked = failure == 0, FailRead = failure == 1, FailDelete = failure == 3 };
        var backend = new Backend { Fail = failure == 2 };
        var before = store.State;
        Assert.False(TaskbarRecovery.Run(store, backend, (_, _) => false));
        Assert.Same(before, store.State);
        if (failure <= 1) Assert.Equal(0, backend.Restored);
        store.Locked = store.FailRead = store.FailDelete = backend.Fail = false;
        Assert.True(TaskbarRecovery.Run(store, backend, (_, _) => false)); Assert.Null(store.State);
    }
    [Fact]
    public void InvalidSnapshotCannotReachNativeRestoration()
    {
        var store = new Store(); var backend = new Backend(); store.State = store.State! with { Version = 2 };
        Assert.False(TaskbarRecovery.Run(store, backend, (_, _) => false)); Assert.Equal(0, backend.Restored);
    }
    [Theory]
    [InlineData("0", "1", "00000000000000000000000000000000")]
    [InlineData("-1", "1", "00000000000000000000000000000000")]
    [InlineData("1", "0", "00000000000000000000000000000000")]
    [InlineData("1", "1", "../outside")]
    public async Task InvalidWatchdogArgumentsNeverInitializeProfile(string id, string started, string nonce)
        => Assert.Equal(1, await TaskbarWatchdog.RunAsync(id, started, nonce));
}
