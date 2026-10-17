using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class StartButtonPressTests
{
    [Fact]
    public void OnlyAnEnabledCompleteClickTogglesAndBothEventsAreConsumed()
    {
        var press = new StartButtonPress();
        Assert.Equal(StartButtonAction.Forward, press.Process(true, false, true));
        Assert.Equal(StartButtonAction.Forward, press.Process(false, true, true));
        Assert.Equal(StartButtonAction.Forward, press.Process(true, true, false));
        Assert.Equal(StartButtonAction.Suppress, press.Process(true, true, true));
        Assert.True(press.IsPressed);
        Assert.Equal(StartButtonAction.Toggle, press.Process(false, true, true));
        Assert.False(press.IsPressed);
        Assert.Equal(StartButtonAction.Forward, press.Process(false, true, true));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ReleaseOutsideOrPreferenceChangesCancelWithoutAnUnmatchedMouseUp(bool inside, bool enabled)
    {
        var press = new StartButtonPress();
        press.Process(true, true, true);
        Assert.Equal(StartButtonAction.Suppress, press.Process(false, inside, enabled));
        Assert.False(press.IsPressed);
    }
}
