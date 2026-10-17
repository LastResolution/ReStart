using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class WindowsKeyStateTests
{
    [Theory]
    [InlineData(0x5B)]
    [InlineData(0x5C)]
    public void BareWindowsKeyTogglesOnlyOnRelease(int key)
    {
        var state = new WindowsKeyState();
        Assert.Equal(WindowsKeyAction.Suppress, state.Process(key, true));
        Assert.Equal(WindowsKeyAction.Suppress, state.Process(key, true));
        Assert.Equal(WindowsKeyAction.Toggle, state.Process(key, false));
        Assert.Equal(WindowsKeyAction.Pass, state.Process(key, false));
    }

    [Theory]
    [InlineData(0x45)] // Win+E
    [InlineData(0x44)] // Win+D
    [InlineData(0x4C)] // Win+L
    [InlineData(0x09)] // Win+Tab
    public void ReplaysWindowsDownBeforeShortcutKey(int key)
    {
        var state = new WindowsKeyState();
        state.Process(0x5B, true);
        Assert.Equal(WindowsKeyAction.ReplayWindowsDown, state.Process(key, true));
        Assert.Equal(0x5B, state.HeldKey);
        Assert.Equal(WindowsKeyAction.Pass, state.Process(key, false));
        Assert.Equal(WindowsKeyAction.Pass, state.Process(0x5B, false));
    }

    [Fact]
    public void ExistingModifiersKeepTheChordNative()
    {
        var state = new WindowsKeyState();
        Assert.Equal(WindowsKeyAction.Pass, state.Process(0x5B, true, true));
        Assert.Equal(WindowsKeyAction.Pass, state.Process(0x53, true));
        Assert.Equal(WindowsKeyAction.Pass, state.Process(0x5B, false));
    }

    [Fact]
    public void StateResetsBetweenShortcutAndBareKey()
    {
        var state = new WindowsKeyState();
        state.Process(0x5B, true); state.Process(0x45, true); state.Process(0x5B, false);
        state.Process(0x5B, true);
        Assert.Equal(WindowsKeyAction.Toggle, state.Process(0x5B, false));
    }
}
