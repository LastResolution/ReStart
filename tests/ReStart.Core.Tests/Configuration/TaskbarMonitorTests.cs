using System.Drawing;
using ReStart.App.Services;

namespace ReStart.Core.Tests;

public sealed class TaskbarMonitorTests
{
    [Fact]
    public void AutoHiddenRegistrationWinsWhenItsHiddenRectangleOverlapsAnotherDisplay()
    {
        var owner = new TaskbarBounds(0, 0, 1280, 800);
        var neighbor = new TaskbarBounds(0, 800, 1280, 800);
        var resolved = TaskbarNativeBackend.ResolveMonitor(neighbor, new[] { neighbor, owner }, monitor => monitor == owner ? 3 : null);
        Assert.Equal(owner, resolved.Monitor); Assert.Equal(3, resolved.Edge);
        Assert.Equal(neighbor, TaskbarNativeBackend.ResolveMonitor(neighbor, new[] { owner, neighbor }, _ => null).Monitor);
    }
    private static TaskbarWindowState Window(long id, TaskbarBounds monitor, TaskbarBounds target, bool autoHide = true)
        => new(id, 1, 1, true, autoHide, monitor, target with { Y = monitor.Y + monitor.Height - 2 }, target);
    [Fact]
    public void CaptureIsRestrictedToTheLauncherMonitor()
    {
        var mode = TaskbarLeaseMode.Reveal;
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
    public void FixedTaskbarAndLegacyHideAreNeverAcquiredAndPreferenceChangesInvalidateOwnership()
    {
        var monitor = new TaskbarBounds(0, 0, 1280, 800);
        var observed = Window(1, monitor, new(0, 752, 1280, 48), false);
        var backend = new TaskbarNativeBackend(() => [observed]) { Scope = monitor };
        Assert.Null(backend.Capture(TaskbarLeaseMode.Reveal));
        Assert.Null(backend.Capture(TaskbarLeaseMode.Hide));
        observed = observed with { AutoHide = true };
        var saved = backend.Capture(TaskbarLeaseMode.Reveal)!;
        Assert.Null(backend.Capture(TaskbarLeaseMode.Hide));
        observed = observed with { AutoHide = false };
        Assert.False(backend.IsCurrent(saved));
        observed = observed with { AutoHide = true };
        observed = observed with { ProcessStarted = 2 };
        Assert.False(backend.IsCurrent(saved));
        observed = observed with { ProcessStarted = 1, Monitor = new(0, 0, 800, 1280) };
        Assert.False(backend.IsCurrent(saved));
    }
    [Theory]
    [InlineData(0, 752, 1280, 48)]
    [InlineData(0, 0, 1280, 48)]
    [InlineData(0, 0, 48, 800)]
    [InlineData(1232, 0, 48, 800)]
    public void RevealingAnyTaskbarEdgePreservesTheExistingFullscreenArea(int x, int y, int width, int height)
    {
        var monitor = new TaskbarBounds(-1280, -800, 1280, 800);
        var state = new TaskbarLeaseState(1, Guid.NewGuid().ToString("N"), 1, 1, TaskbarLeaseMode.Reveal,
            [Window(1, monitor, new(x - 1280, y - 800, width, height))]);
        Assert.Equal(new Rectangle(-1280, -800, 1280, 800), TaskbarNativeBackend.AvailableArea(new(-1280, -800, 1280, 800), state, monitor));
        var reserved = new Rectangle(-1240, -770, 1240, 730);
        Assert.Equal(reserved, TaskbarNativeBackend.AvailableArea(reserved, state, monitor));
        var another = new TaskbarBounds(0, 0, 1920, 1080);
        Assert.Equal(new Rectangle(0, 0, 1920, 1080), TaskbarNativeBackend.AvailableArea(new(0, 0, 1920, 1080), state, another));
    }
    [Theory]
    [InlineData(0, 752, 1280, 48, 0, 0, 1280, 752)]
    [InlineData(0, 0, 1280, 48, 0, 48, 1280, 752)]
    [InlineData(0, 0, 48, 800, 48, 0, 1232, 800)]
    [InlineData(1232, 0, 48, 800, 0, 0, 1232, 800)]
    public void RevealedTaskbarReservesInteractiveContentOnly(int x, int y, int width, int height, int safeX, int safeY, int safeWidth, int safeHeight)
    {
        var monitor = new TaskbarBounds(-1280, -800, 1280, 800);
        var area = new Rectangle(monitor.X, monitor.Y, monitor.Width, monitor.Height);
        var state = new TaskbarLeaseState(1, Guid.NewGuid().ToString("N"), 1, 1, TaskbarLeaseMode.Reveal,
            [Window(1, monitor, new(x - 1280, y - 800, width, height))]);
        var safe = new Rectangle(safeX - 1280, safeY - 800, safeWidth, safeHeight);
        Assert.Equal(safe, TaskbarNativeBackend.ContentSafeArea(area, state, monitor));
        Assert.Equal(area, TaskbarNativeBackend.AvailableArea(area, state, monitor));
        Assert.Equal(safe, TaskbarNativeBackend.ContentSafeArea(safe, state, monitor));
        Assert.Equal(area, TaskbarNativeBackend.ContentSafeArea(area, null, monitor));
        Assert.Equal(area, TaskbarNativeBackend.ContentSafeArea(area, state with { Mode = TaskbarLeaseMode.Hide }, monitor));
        Assert.Equal(area, TaskbarNativeBackend.ContentSafeArea(area, state, new(0, 0, 1920, 1080)));
    }
    [Fact]
    public void ContentReservationPreservesOtherAppbarsAndDeductsOnlyTheActualOverlap()
    {
        var monitor = new TaskbarBounds(0, 0, 1280, 800);
        var state = new TaskbarLeaseState(1, Guid.NewGuid().ToString("N"), 1, 1, TaskbarLeaseMode.Reveal,
            [Window(1, monitor, new(0, 752, 1280, 48))]);
        Assert.Equal(new Rectangle(40, 30, 1240, 722), TaskbarNativeBackend.ContentSafeArea(new(40, 30, 1240, 768), state, monitor));
        Assert.Equal(new Rectangle(0, 0, 1280, 720), TaskbarNativeBackend.ContentSafeArea(new(0, 0, 1280, 720), state, monitor));
    }
    [Theory]
    [InlineData(null, false, null)]
    [InlineData(null, true, null)]
    [InlineData(false, true, false)]
    [InlineData(false, false, null)]
    [InlineData(true, false, null)]
    [InlineData(true, true, null)]
    public void RecoveryRestoresOnlyAnOwnedTopmostPromotion(bool? original, bool current, bool? expected)
        => Assert.Equal(expected, TaskbarNativeBackend.RestoreTopmost(original, current));
    [Theory]
    [InlineData(0, -46, 0, 48, 800, true)]
    [InlineData(1, 0, -46, 1280, 48, true)]
    [InlineData(2, 1278, 0, 48, 800, true)]
    [InlineData(3, 0, 798, 1280, 48, true)]
    [InlineData(0, -44, 0, 48, 800, false)]
    [InlineData(1, 0, -44, 1280, 48, false)]
    [InlineData(2, 1276, 0, 48, 800, false)]
    [InlineData(3, 0, 796, 1280, 48, false)]
    [InlineData(0, 0, 0, 48, 800, false)]
    [InlineData(1, 0, 0, 1280, 48, false)]
    [InlineData(2, 1232, 0, 48, 800, false)]
    [InlineData(3, 0, 752, 1280, 48, false)]
    [InlineData(4, 0, 798, 1280, 48, false)]
    public void ContentRecoveryExcludesVisibleAndPartiallyRevealingTaskbars(int edge, int x, int y, int width, int height, bool hidden)
    {
        var monitor = new TaskbarBounds(-1280, -800, 1280, 800);
        Assert.Equal(hidden, TaskbarNativeBackend.HiddenAtEdge(monitor, new(x - 1280, y - 800, width, height), edge));
    }
    [Theory]
    [InlineData(0, -1326, -800, 48, 800)]
    [InlineData(1, -1280, -846, 1280, 48)]
    [InlineData(2, -2, -800, 48, 800)]
    [InlineData(3, -1280, -2, 1280, 48)]
    public void RecoveryUsesTheCurrentRegisteredEdgeRatherThanCapturedPositions(int edge, int x, int y, int width, int height)
    {
        var monitor = new TaskbarBounds(-1280, -800, 1280, 800);
        var bounds = TaskbarNativeBackend.HiddenBounds(monitor, new(9999, 9999, edge is 0 or 2 ? 48 : 1280, edge is 0 or 2 ? 800 : 48), edge);
        Assert.Equal(new TaskbarBounds(x, y, width, height), bounds);
        Assert.True(TaskbarNativeBackend.HiddenAtEdge(monitor, bounds, edge));
        Assert.Throws<ArgumentOutOfRangeException>(() => TaskbarNativeBackend.HiddenBounds(monitor, bounds, 4));
    }
    [Fact]
    public void ReplacedCompositionContentInvalidatesTheLease()
    {
        var monitor = new TaskbarBounds(0, 0, 1280, 800);
        var observed = Window(1, monitor, new(0, 752, 1280, 48)) with { Content = [new(2, false)] };
        var backend = new TaskbarNativeBackend(() => [observed]) { Scope = monitor };
        var saved = backend.Capture(TaskbarLeaseMode.Reveal)!;
        observed = observed with { Content = [new(2, true)] };
        Assert.True(backend.IsCurrent(saved));
        observed = observed with { Content = [new(3, false)] };
        Assert.False(backend.IsCurrent(saved));
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
