using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class TileFolderHoverTests
{
    [Fact]
    public void ProgressIsBoundedAndDoesNotArmOrResetTheHover()
    {
        var hover = new TileFolderHover();
        Assert.Equal(0, hover.Progress(100));
        hover.Update("a", "b", 100);
        Assert.Equal(.5, hover.Progress(100 + TileFolderHover.DelayMilliseconds / 2));
        Assert.Equal(0, hover.Progress(0));
        Assert.Equal(1, hover.Progress(10000));
        Assert.False(hover.Update("a", "b", 101));
        hover.Reset(); Assert.Equal(0, hover.Progress(10000));
    }
    [Fact]
    public void GroupDwellArmsQuicklyAndChangingIntentCannotReuseItsElapsedTime()
    {
        var hover = new TileFolderHover();
        Assert.False(hover.Update("a", "b", 10, TileFolderHover.GroupDelayMilliseconds));
        Assert.False(hover.Update("a", "b", 309, TileFolderHover.GroupDelayMilliseconds));
        Assert.True(hover.Update("a", "b", 310, TileFolderHover.GroupDelayMilliseconds));
        Assert.Equal("b", hover.TargetId);
        Assert.False(hover.Update("a", "b", 311));
        Assert.False(hover.Update("a", "b", 310 + TileFolderHover.DelayMilliseconds - 1));
        Assert.True(hover.Update("a", "b", 311 + TileFolderHover.DelayMilliseconds));
        hover.Reset(); Assert.Null(hover.TargetId);
    }
    [Fact]
    public void RequiresAContinuousDwellWithoutModifiers()
    {
        var hover = new TileFolderHover();
        Assert.False(hover.Update("a", "b", 10));
        Assert.False(hover.Update("a", "b", 10 + TileFolderHover.DelayMilliseconds - 1));
        Assert.True(hover.Update("a", "b", 10 + TileFolderHover.DelayMilliseconds));
    }
    [Fact]
    public void LeavingChangingTargetsOrCancellingRestartsTheDwell()
    {
        var hover = new TileFolderHover();
        hover.Update("a", "b", 0);
        Assert.False(hover.Update("a", "c", 900));
        Assert.False(hover.Update("a", "c", 1100));
        hover.Update("a", null, 1200);
        Assert.False(hover.Update("a", "c", 3000));
        hover.Reset();
        Assert.False(hover.Update("a", "c", 6000));
        Assert.False(hover.Update("c", "c", 9000));
    }
    [Fact]
    public void ANewSourceAndAClockResetCannotReuseAnOldDwell()
    {
        var hover = new TileFolderHover();
        hover.Update("a", "b", 100);
        Assert.False(hover.Update("c", "b", 2000));
        Assert.False(hover.Update("c", "b", 1));
    }
}
