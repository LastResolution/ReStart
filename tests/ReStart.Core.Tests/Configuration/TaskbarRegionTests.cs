using System.Text.Json;
using System.Text.Json.Nodes;
using ReStart.App.Services;

namespace ReStart.Core.Tests;

public sealed class TaskbarRegionTests
{
    [Theory]
    [InlineData(0, 1278, 0, 2, 800)]
    [InlineData(1, 0, 798, 1280, 2)]
    [InlineData(2, 0, 0, 2, 800)]
    [InlineData(3, 0, 0, 1280, 2)]
    [InlineData(3, 0, 0, 1280, 1)]
    public void HiddenStripIsNotAVisibleTaskbarSurface(int edge, int x, int y, int width, int height)
    {
        var size = new TaskbarBounds(-1280, -800, 1280, 800);
        var clip = new TaskbarClipState(true, [new(x, y, width, height)]);
        Assert.True(TaskbarNativeRegion.AutoHideStrip(clip, size, edge));
        Assert.False(TaskbarNativeRegion.FullSurface(clip, size));
        Assert.False(TaskbarNativeRegion.AutoHideStrip(clip, size, 4));
    }

    [Fact]
    public void OnlyExactNativeHiddenStripsQualifyForTemporaryRemoval()
    {
        var bounds = new TaskbarBounds(0, 0, 1920, 48);
        foreach (var clip in new TaskbarClipState?[] { null, new(false, []), new(true, []),
            new(true, [new(0, 0, 1920, 3)]), new(true, [new(1, 0, 1919, 2)]),
            new(true, [new(0, 1, 1920, 2)]), new(true, [new(0, 0, 1900, 2)]),
            new(true, [new(0, 0, 960, 2), new(1000, 0, 920, 2)]) })
            Assert.False(TaskbarNativeRegion.AutoHideStrip(clip, bounds, 3));
        Assert.False(TaskbarNativeRegion.FullSurface(null, bounds));
        Assert.False(TaskbarNativeRegion.FullSurface(new(true, []), bounds));
        Assert.True(TaskbarNativeRegion.FullSurface(new(false, []), bounds));
        Assert.True(TaskbarNativeRegion.FullSurface(new(true, [new(0, 0, 1920, 48)]), bounds));
    }

    [Fact]
    public void RecoveryPreservesBoundedClippingAndReadsOlderRecordsWithoutInventingIt()
    {
        var monitor = new TaskbarBounds(0, 0, 1920, 1080);
        var clip = new TaskbarClipState(true, [new(0, 0, 1920, 2)]);
        var state = new TaskbarLeaseState(1, Guid.NewGuid().ToString("N"), 1, 2, TaskbarLeaseMode.Reveal,
            [new(1, 1, 1, true, true, monitor, new(0, 1078, 1920, 48), new(0, 1032, 1920, 48), Clip: clip)]);
        var json = JsonSerializer.SerializeToNode(state)!;
        var roundTrip = json.Deserialize<TaskbarLeaseState>()!;
        Assert.True(roundTrip.IsValid());
        Assert.True(TaskbarNativeRegion.Same(clip, roundTrip.Windows[0].Clip));
        json["Windows"]![0]!.AsObject().Remove("Clip");
        var older = json.Deserialize<TaskbarLeaseState>()!;
        Assert.True(older.IsValid()); Assert.Null(older.Windows[0].Clip);
        foreach (var invalid in new TaskbarClipState[] { new(false, [new(0, 0, 1920, 2)]), new(true, null!),
            new(true, [new(0, 0, -1, 2)]), new(true, [new(300000, 0, 1, 2)]),
            new(true, Enumerable.Repeat(new TaskbarBounds(0, 0, 1, 1), 257).ToArray()) })
            Assert.False((state with { Windows = [state.Windows[0] with { Clip = invalid }] }).IsValid());
    }

    [Fact]
    public void OwnRegionRemovalDoesNotInvalidateTheLeaseButOtherClippingChangesDo()
    {
        var monitor = new TaskbarBounds(0, 0, 1920, 1080);
        var observed = new TaskbarWindowState(1, 1, 1, true, true, monitor, new(0, 1078, 1920, 48), new(0, 1032, 1920, 48),
            Clip: new(true, [new(0, 0, 1920, 2)]));
        var backend = new TaskbarNativeBackend(() => [observed]) { Scope = monitor };
        var state = backend.Capture(TaskbarLeaseMode.Reveal)!;
        Assert.True(backend.IsCurrent(state));
        observed = observed with { Clip = new(false, []) };
        Assert.True(backend.IsCurrent(state));
        var unrestricted = backend.Capture(TaskbarLeaseMode.Reveal)!;
        observed = observed with { Clip = new(true, [new(0, 0, 1920, 2)]) };
        Assert.False(backend.IsCurrent(unrestricted));
        observed = observed with { Clip = new(true, [new(0, 0, 1920, 20)]) };
        Assert.False(backend.IsCurrent(state));
    }

    [Theory]
    [InlineData(-1326, -800, 48, 800, 0)]
    [InlineData(-1280, -846, 1280, 48, 1)]
    [InlineData(-2, -800, 48, 800, 2)]
    [InlineData(-1280, -2, 1280, 48, 3)]
    [InlineData(-1280, -800, 48, 800, 0)]
    [InlineData(-1280, -800, 1280, 48, 1)]
    [InlineData(-48, -800, 48, 800, 2)]
    [InlineData(-1280, -48, 1280, 48, 3)]
    [InlineData(-1280, -20, 1280, 48, null)]
    [InlineData(-1270, -2, 1280, 48, null)]
    [InlineData(-1280, -2, 1200, 48, null)]
    [InlineData(0, 0, 1280, 48, null)]
    public void UnregisteredSecondaryTrayMustMatchItsExactMonitorEdge(int x, int y, int width, int height, int? edge)
        => Assert.Equal(edge, TaskbarNativeBackend.SecondaryEdge(new(-1280, -800, 1280, 800), new(x, y, width, height)));
}
