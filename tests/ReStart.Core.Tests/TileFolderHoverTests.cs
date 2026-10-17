using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class TileFolderHoverTests
{
    [Fact]
    public void RequiresAContinuousSecondWithoutModifiers()
    {
        var hover = new TileFolderHover();
        Assert.False(hover.Update("a", "b", 10));
        Assert.False(hover.Update("a", "b", 1009));
        Assert.True(hover.Update("a", "b", 1010));
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
