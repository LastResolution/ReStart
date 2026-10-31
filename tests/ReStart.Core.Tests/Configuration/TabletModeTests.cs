using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class TabletModeTests
{
    private sealed class Lease(Action release) : IDisposable { public void Dispose() => release(); }
    public static IEnumerable<object[]> Observations() =>
        from posture in Enum.GetValues<DevicePosture>()
        from keyboard in Enum.GetValues<KeyboardPresence>()
        from keyboardPolicy in new[] { false, true }
        select new object[] { posture, keyboard, keyboardPolicy };

    [Theory]
    [MemberData(nameof(Observations))]
    public void PostureAndTemporaryOverridesNeverGrantConsent(DevicePosture posture, KeyboardPresence keyboard, bool keyboardPolicy)
    {
        var preferences = new Preferences { TabletKeyboardMeansDesktop = keyboardPolicy };
        var input = new TabletInputSnapshot(posture, keyboard);
        Assert.False(TabletModePolicy.IsActive(preferences, input));
        Assert.False(TabletModePolicy.IsActive(preferences, input, DevicePosture.Slate));
    }

    [Theory]
    [MemberData(nameof(Observations))]
    public void KeyboardPolicyUsesOnlyCurrentPositivePresence(DevicePosture posture, KeyboardPresence keyboard, bool keyboardPolicy)
    {
        var preferences = new Preferences { TabletFeaturesEnabled = true, TabletKeyboardMeansDesktop = keyboardPolicy };
        var input = new TabletInputSnapshot(posture, keyboard);
        Assert.Equal(posture == DevicePosture.Slate && !(keyboardPolicy && keyboard == KeyboardPresence.Present), TabletModePolicy.IsActive(preferences, input));
        Assert.False(TabletModePolicy.IsActive(preferences, input, DevicePosture.Desktop));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TransitionsPreserveDesktopPreferencesAndEveryLayoutByte(bool windows, bool start)
    {
        var layout = new LayoutDocument { Preferences = new Preferences { CaptureWindowsKey = windows, CaptureStartButton = start, TabletFeaturesEnabled = true } };
        layout.Groups.Add(new TileGroup { Name = "Owned fixture", Tiles = [new Tile { AppId = "owned", Label = "Owned", Target = "owned.exe" }] });
        var before = JsonSerializer.Serialize(layout);
        var calls = new List<string>();
        using (var session = new TabletSession([() => { calls.Add("enter"); return new Lease(() => calls.Add("exit")); }]))
        {
            for (var index = 0; index < 3; index++)
            {
                Assert.True(session.Apply(layout.Preferences, new(DevicePosture.Slate, KeyboardPresence.Absent)));
                Assert.False(session.Apply(layout.Preferences, new(DevicePosture.Slate, KeyboardPresence.Absent)));
                Assert.True(session.Apply(layout.Preferences, new(DevicePosture.Desktop, KeyboardPresence.Absent)));
                Assert.False(session.Apply(layout.Preferences, new(DevicePosture.Desktop, KeyboardPresence.Absent)));
            }
        }
        Assert.Equal(new[] { "enter", "exit", "enter", "exit", "enter", "exit" }, calls);
        Assert.Equal(before, JsonSerializer.Serialize(layout));
    }

    [Fact]
    public void PartialInitializationCleansUpAndCanRetry()
    {
        var calls = new List<string>(); var fail = true;
        using var session = new TabletSession([
            () => { calls.Add("first"); return new Lease(() => calls.Add("release-first")); },
            () => { if (fail) throw new IOException("Owned failure"); return new Lease(() => calls.Add("release-second")); }
        ]);
        var preferences = new Preferences { TabletFeaturesEnabled = true };
        Assert.Throws<IOException>(() => session.Apply(preferences, new(DevicePosture.Slate, KeyboardPresence.Absent)));
        Assert.False(session.IsActive);
        Assert.Equal(new[] { "first", "release-first" }, calls);
        fail = false;
        Assert.True(session.Apply(preferences, new(DevicePosture.Slate, KeyboardPresence.Absent)));
        preferences.TabletFeaturesEnabled = false;
        Assert.True(session.Apply(preferences, new(DevicePosture.Slate, KeyboardPresence.Absent)));
        Assert.Equal(new[] { "first", "release-first", "first", "release-second", "release-first" }, calls);
    }

    [Fact]
    public void CleanupAttemptsEveryEffectAndCannotReactivateAfterDisposal()
    {
        var released = false;
        var session = new TabletSession([() => new Lease(() => released = true), () => new Lease(() => throw new IOException("Owned cleanup failure"))]);
        var preferences = new Preferences { TabletFeaturesEnabled = true };
        session.Apply(preferences, new(DevicePosture.Slate, KeyboardPresence.Absent));
        Assert.Throws<AggregateException>(() => session.Dispose());
        Assert.True(released); Assert.False(session.IsActive);
        session.Dispose();
        Assert.False(session.Apply(preferences, new(DevicePosture.Slate, KeyboardPresence.Absent)));
    }
}
