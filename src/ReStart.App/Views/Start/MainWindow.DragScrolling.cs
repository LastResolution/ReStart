using System.Windows;
using System.Windows.Threading;

namespace ReStart.App;

public partial class MainWindow
{
    private DispatcherTimer? _dragScrollTimer;
    private Point _dragViewportPoint;
    private long _dragScrollTime;
    private string? _activeDragFormat, _activeDragId;

    private void TrackDragScrolling(DragEventArgs e)
    {
        if (!Document.Preferences.DragEdgeScrolling || !HasDrag(e)) { _dragScrollTimer?.Stop(); return; }
        _dragViewportPoint = e.GetPosition(ContentScroll);
        _dragScrollTimer ??= MakeDragScrollTimer();
        if (!_dragScrollTimer.IsEnabled) { _dragScrollTime = Environment.TickCount64; _dragScrollTimer.Start(); }
    }

    private DispatcherTimer MakeDragScrollTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(32) };
        timer.Tick += (_, _) =>
        {
            if (!_dragging || !IsVisible || _activeDragId == null) { timer.Stop(); return; }
            var now = Environment.TickCount64;
            var seconds = Math.Clamp((now - _dragScrollTime) / 1000d, 0, .1); _dragScrollTime = now;
            ScrollDragAt(_dragViewportPoint, seconds);
        };
        return timer;
    }

    private bool ScrollDragAt(Point point, double seconds)
    {
        if (!Document.Preferences.DragEdgeScrolling) return false;
        static double Speed(double p, double extent) => p < 42 ? -600 * Math.Clamp((42 - p) / 42, 0, 1)
            : p > extent - 42 ? 600 * Math.Clamp((p - extent + 42) / 42, 0, 1) : 0;
        if (!new Rect(-16, -16, ContentScroll.ActualWidth + 32, ContentScroll.ActualHeight + 32).Contains(point)) return false;
        var dx = Speed(point.X, ContentScroll.ViewportWidth) * seconds;
        var dy = Speed(point.Y, ContentScroll.ViewportHeight) * seconds;
        if (dx == 0 && dy == 0) return false;
        if (dy > 0) ExtendDragSpace();
        if (dx > 0)
        {
            GroupsPanel.DragMinimumWidth = Math.Min(10000 * Cell, Math.Max(GroupsPanel.DragMinimumWidth, ContentScroll.HorizontalOffset + ContentScroll.ViewportWidth + Cell * 4));
            GroupsPanel.InvalidateMeasure();
        }
        ContentScroll.UpdateLayout();
        ContentScroll.ScrollToVerticalOffset(ContentScroll.VerticalOffset + dy);
        ContentScroll.ScrollToHorizontalOffset(ContentScroll.HorizontalOffset + dx);
        ContentScroll.UpdateLayout();
        var board = ContentScroll.TranslatePoint(point, GroupsPanel);
        // The preview follows the stationary viewport pointer after scrolling;
        // no persisted coordinates are changed until the OLE drop commits.
        if (_activeDragFormat == "ReStart.Group" && Document.Groups.FirstOrDefault(g => g.Id == _activeDragId) is { } group)
            PreviewGroupOnBoard(group, board);
        else if (_activeDragFormat == "ReStart.Selection" && _activeDragId is { } anchor && PlanSelectionDrop(anchor, board) is { } plan)
        { ShowSelectionPreview(plan, board, anchor); ShowPendingFolderPreview(); }
        else if (_activeDragId != null && (_activeDragFormat == "ReStart.App" ? TileForAppDrag(_activeDragId) : FindTile(_activeDragId)) is { } tile)
            PreviewSingleTileDrop(tile, board, _activeDragId);
        return true;
    }
}
