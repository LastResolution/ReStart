using System.Drawing;
using ReStart.App.Services;

namespace ReStart.Core.Tests;

public sealed class TaskbarMonitorTests
{
    private static TaskbarWindowState Window(long id, TaskbarBounds monitor, TaskbarBounds target, bool autoHide = true)
        => new(id, 1, 1, true, autoHide, monitor, target with { Y = monitor.Y + monitor.Height - 2 }, target);
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void CaptureIsRestrictedToTheLauncherMonitor(int value)
    {
        var mode = (TaskbarLeaseMode)value;
        var first = new TaskbarBounds(0, 0, 1280, 800); var second = new TaskbarBounds(-900, 0, 900, 1440);
        var windows = new[] { Window(1, first, new(0, 752, 1280, 48)), Window(2, second, new(-900, 1392, 900, 48)) };
        var backend = new TaskbarNativeBackend(() => windows) { Scope = first };
        var before = backend.Capture(mode)!;
        Assert.Equal(1, Assert.Single(before.Windows).Window); Assert.True(backend.IsCurrent(before));
        backend.Scope = second;
        Assert.False(backend.IsCurrent(before));
        Assert.Equal(2, Assert.Single(backend.Capture(mode)!.Windows).Window);
        backend.Scope = new(1280, 0, 1920, 1080);
        Assert.Null(backend.Capture(mode));
    }
    [Fact]
    public void FixedTaskbarIsNotRevealedAndExplorerChangesInvalidateOldOwnership()
    {
        var monitor = new TaskbarBounds(0, 0, 1280, 800);
        var observed = Window(1, monitor, new(0, 752, 1280, 48), false);
        var backend = new TaskbarNativeBackend(() => [observed]) { Scope = monitor };
        Assert.Null(backend.Capture(TaskbarLeaseMode.Reveal));
        observed = observed with { AutoHide = true };
        var saved = backend.Capture(TaskbarLeaseMode.Reveal)!;
        observed = observed with { ProcessStarted = 2 };
        Assert.False(backend.IsCurrent(saved));
        observed = observed with { ProcessStarted = 1, Monitor = new(0, 0, 800, 1280) };
        Assert.False(backend.IsCurrent(saved));
    }
    [Theory]
    [InlineData(0, 752, 1280, 48, 0, 0, 1280, 752)]
    [InlineData(0, 0, 1280, 48, 0, 48, 1280, 752)]
    [InlineData(0, 0, 48, 800, 48, 0, 1232, 800)]
    [InlineData(1232, 0, 48, 800, 0, 0, 1232, 800)]
    public void RevealedTaskbarsReserveOnlyTheirOwnEdge(int x, int y, int width, int height, int ax, int ay, int aw, int ah)
    {
        var monitor = new TaskbarBounds(-1280, -800, 1280, 800);
        var state = new TaskbarLeaseState(1, Guid.NewGuid().ToString("N"), 1, 1, TaskbarLeaseMode.Reveal,
            [Window(1, monitor, new(x - 1280, y - 800, width, height))]);
        Assert.Equal(new Rectangle(ax - 1280, ay - 800, aw, ah), TaskbarNativeBackend.AvailableArea(new(-1280, -800, 1280, 800), state, monitor));
        var another = new TaskbarBounds(0, 0, 1920, 1080);
        Assert.Equal(new Rectangle(0, 0, 1920, 1080), TaskbarNativeBackend.AvailableArea(new(0, 0, 1920, 1080), state, another));
    }
    [Fact]
    public void HidingTheWindowsTaskbarDoesNotRemoveAnotherAppbarsReservedArea()
    {
        var monitor = new TaskbarBounds(0, 0, 1280, 800);
        var state = new TaskbarLeaseState(1, Guid.NewGuid().ToString("N"), 1, 1, TaskbarLeaseMode.Hide, [Window(1, monitor, new(0, 752, 1280, 48))]);
        Assert.Equal(new Rectangle(40, 0, 1240, 800), TaskbarNativeBackend.AvailableArea(new(40, 0, 1240, 752), state, monitor));
        Assert.Equal(new Rectangle(0, 0, 1280, 720), TaskbarNativeBackend.AvailableArea(new(0, 0, 1280, 720), state, monitor));
    }
}
