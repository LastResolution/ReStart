using System.Windows;

namespace ReStart.App;

public partial class MainWindow
{
    private bool _dragScrolling;
    private Point _dragViewportPoint;
    private string? _activeDragFormat, _activeDragId;

    private void TrackDragScrolling(DragEventArgs e)
    {
        if (!Document.Preferences.DragEdgeScrolling || !HasDrag(e)) { _dragScrolling = false; return; }
        _dragViewportPoint = e.GetPosition(ContentScroll);
        _dragScrolling = true;
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
