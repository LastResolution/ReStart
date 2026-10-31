using System.ComponentModel;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class TabletKeyboardTests
{
    [Fact]
    public void DisconnectedAndInternalDevicesDoNotCountAsExternal()
    {
        Assert.Equal(KeyboardPresence.Absent, TabletInputDevices.ReadKeyboard(() => [new(false, true), new(true, false)]));
        Assert.Equal(KeyboardPresence.Present, TabletInputDevices.ReadKeyboard(() => [new(true, false), new(true, true)]));
    }
    [Fact]
    public void EveryPollUsesCurrentPresenceRatherThanPreviousSuccess()
    {
        var connected = true;
        IEnumerable<TabletInputDevices.KeyboardObservation> Enumerate() => connected ? [new(true, true)] : [];
        Assert.Equal(KeyboardPresence.Present, TabletInputDevices.ReadKeyboard(Enumerate));
        connected = false;
        Assert.Equal(KeyboardPresence.Absent, TabletInputDevices.ReadKeyboard(Enumerate));
        connected = true;
        Assert.Equal(KeyboardPresence.Present, TabletInputDevices.ReadKeyboard(Enumerate));
        Assert.Equal(KeyboardPresence.Unknown, TabletInputDevices.ReadKeyboard(() => throw new UnauthorizedAccessException()));
        Assert.Equal(KeyboardPresence.Absent, TabletInputDevices.ReadKeyboard(() => []));
    }
    [Theory]
    [InlineData(5)]
    [InlineData(13)]
    [InlineData(1167)]
    public void NativeFailuresAreUnknownAndDoNotInventAnAttachedKeyboard(int error)
        => Assert.Equal(KeyboardPresence.Unknown, TabletInputDevices.ReadKeyboard(() => throw new Win32Exception(error)));
    [Fact]
    public void NativeEnumerationReturnsOnlyAnonymousStates()
    {
        if (!OperatingSystem.IsWindows()) return;
        var sample = TabletInputDevices.Read();
        Assert.True(Enum.IsDefined(sample.Keyboard));
        Assert.True(Enum.IsDefined(sample.Posture));
        Assert.Equal(2, typeof(TabletInputSnapshot).GetProperties().Length);
    }
}
