using ReStart.App.Services;

namespace ReStart.Core.Tests;

public sealed class TaskbarRecoveryStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ReStart-taskbar-test-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private static TaskbarLeaseState Snapshot() => new(1, Guid.NewGuid().ToString("N"), 1, 1, TaskbarLeaseMode.Hide,
        [new(1, 2, 1, true, true, new(0, 0, 1280, 800), new(0, 798, 1280, 48), new(0, 752, 1280, 48))]);
    [Fact]
    public void RecoveryAndMutationCannotRunUnderDifferentOwnersAtTheSameTime()
    {
        var first = new TaskbarRecoveryStore(_root); var second = new TaskbarRecoveryStore(_root);
        using (first.Acquire()) Assert.Throws<IOException>(() => second.Acquire());
        using (second.Acquire()) Assert.Throws<IOException>(() => first.Acquire());
    }
    [Fact]
    public void RecoveryIsDurableOwnerBoundAndDoesNotOverwriteAnotherTransaction()
    {
        var store = new TaskbarRecoveryStore(_root); var state = Snapshot();
        store.Save(state);
        var loaded = new TaskbarRecoveryStore(_root).Read();
        Assert.NotNull(loaded); Assert.Equal(state.Token, loaded.Token); Assert.Equal(state.Windows, loaded.Windows);
        var before = File.ReadAllBytes(store.MarkerPath);
        Assert.Throws<IOException>(() => store.Save(Snapshot()));
        Assert.Throws<InvalidDataException>(() => store.Delete(Guid.NewGuid().ToString("N")));
        Assert.Equal(before, File.ReadAllBytes(store.MarkerPath));
        store.Delete(state.Token); Assert.Null(store.Read());
    }
    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("{\"Version\":100}")]
    public void IncompleteAndUnknownRecoveryIsNotAccepted(string content)
    {
        Directory.CreateDirectory(_root); var store = new TaskbarRecoveryStore(_root);
        File.WriteAllText(store.MarkerPath, content);
        Assert.Throws<InvalidDataException>(() => store.Read());
        Assert.Throws<IOException>(() => store.Save(Snapshot()));
        Assert.Equal(content, File.ReadAllText(store.MarkerPath));
    }
    [Fact]
    public void InvalidSnapshotsCannotBePersistedOrInventShellOwnership()
    {
        var store = new TaskbarRecoveryStore(_root); var valid = Snapshot();
        foreach (var invalid in new[] { valid with { Version = 2 }, valid with { ParentId = 0 }, valid with { Windows = [null!] }, valid with { Windows = [] }, valid with { Windows = [valid.Windows[0], valid.Windows[0]] },
            valid with { Windows = [valid.Windows[0] with { Target = new(0, 0, -1, 100) }] } })
            Assert.Throws<InvalidDataException>(() => store.Save(invalid));
        Assert.False(Directory.Exists(_root));
    }
}
