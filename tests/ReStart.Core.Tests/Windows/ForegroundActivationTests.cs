using ReStart.App.Services;

namespace ReStart.Core.Tests;

public sealed class ForegroundActivationTests
{
    [Theory]
    [InlineData("Windows.UI.Core.CoreWindow", false)]
    [InlineData("XamlExplorerHostIslandWindow", false)]
    [InlineData("", false)]
    [InlineData("Shell_TrayWnd", true)]
    [InlineData("Owned classic window", true)]
    public void ActivationNeverJoinsShellHostedStartOrSearchQueues(string className, bool permitted)
        => Assert.Equal(permitted, ForegroundActivation.CanAttachQueue(className));

    [Fact]
    public void MissingLauncherCannotRequestForeground() => Assert.False(ForegroundActivation.Request(0));
}
