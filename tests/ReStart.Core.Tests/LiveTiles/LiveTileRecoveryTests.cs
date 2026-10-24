using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class LiveTileRecoveryTests
{
    [Fact]
    public void RetriesBackOffAndRepeatedFailuresPauseOnlyTheirOwnTile()
    {
        var failed = new LiveTileRecovery(); var healthy = new LiveTileRecovery();
        failed.Fail(1000, true); Assert.False(failed.CanRun(2999)); Assert.True(failed.CanRun(3000));
        failed.Fail(3000, true); Assert.False(failed.CanRun(12999)); Assert.True(failed.CanRun(13000));
        failed.Fail(13000, true); Assert.True(failed.Paused); Assert.False(failed.CanRun(long.MaxValue - 1));
        Assert.True(healthy.CanRun(13000));
    }
    [Fact]
    public void IsolatedOldFailureDoesNotSpendANewRetryBudget()
    {
        var budget = new LiveTileRecovery(); budget.Fail(1000, true); budget.Fail(122000, true);
        Assert.False(budget.Paused); Assert.Equal(124000, budget.RetryAt);
        var disabled = new LiveTileRecovery(); disabled.Fail(1000, false); Assert.True(disabled.Paused);
        var spaced = new LiveTileRecovery(); spaced.Fail(0, true); spaced.Fail(110000, true); spaced.Fail(220000, true);
        Assert.False(spaced.Paused);
    }
}
