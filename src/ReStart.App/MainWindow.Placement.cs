using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private readonly TileFolderHover _folderHover = new();
    private sealed record DragViewState(bool AllApps, bool SearchRequested, string Query, double ScrollOffset, double HorizontalOffset);

    private DragViewState PrepareDragView(string format)
    {
        var previous = new DragViewState(_allApps, _searchRequested, SearchBox.Text, ContentScroll.VerticalOffset, ContentScroll.HorizontalOffset);
        if (format == "ReStart.App") { ShowPinned(this, new()); UpdateLayout(); }
        return previous;
    }

    private void RestoreDragView(DragViewState previous)
    {
        _allApps = previous.AllApps;
        _searchRequested = previous.SearchRequested;
        SearchBox.Text = previous.Query;
        SearchChanged(SearchBox, new(TextBox.TextChangedEvent, UndoAction.None));
        ContentScroll.ScrollToVerticalOffset(previous.ScrollOffset);
        ContentScroll.ScrollToHorizontalOffset(previous.HorizontalOffset);
    }

    private void ExtendDragSpace()
    {
        GroupsPanel.DragMinimumHeight = Math.Min(20000 * Cell / 2,
            Math.Max(GroupsPanel.DragMinimumHeight, ContentScroll.VerticalOffset + ContentScroll.ViewportHeight + Cell * 4));
        GroupsPanel.InvalidateMeasure();
    }

    private Tile? FolderHoverTarget(Tile source, TilePanel panel, Point point, string sourceId, long? timestamp = null)
    {
        var button = Ancestor<Button>(panel.InputHitTest(point) as DependencyObject);
        var target = button?.Tag as Tile;
        var allowed = !source.IsFolder && target != null && target != source && target.Widget.Length == 0;
        if (allowed && button != null)
        {
            var inside = panel.TranslatePoint(point, button);
            allowed = inside.X >= button.ActualWidth * 0.2 && inside.X <= button.ActualWidth * 0.8
                && inside.Y >= button.ActualHeight * 0.2 && inside.Y <= button.ActualHeight * 0.8;
        }
        return _folderHover.Update(sourceId, allowed ? target!.Id : null, timestamp ?? Environment.TickCount64) ? target : null;
    }
    private (TileGroup Group, TilePanel Panel)? GroupAt(Point point)
    {
        var hit = GroupsPanel.InputHitTest(point) as DependencyObject;
        TilePanel? nearest = null;
        while (hit != null && hit != GroupsPanel)
        {
            if (hit is InlineFolderView inline) nearest ??= inline.Tiles;
            if (hit is TilePanel tiles) nearest ??= tiles;
            if (hit is StackPanel { Tag: TileGroup group } stack && stack.Children.OfType<TilePanel>().FirstOrDefault() is TilePanel panel)
                return (group, nearest ?? panel);
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }

    private void OnBoardDragOver(object sender, DragEventArgs e)
    {
        if (!HasDrag(e)) return;
        e.Handled = true;
        e.Effects = DragDropEffects.Move;
        var point = e.GetPosition(GroupsPanel);
        if (e.Data.GetData("ReStart.Group") is string groupId)
        {
            _folderHover.Reset();
            var group = Document.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group == null) { e.Effects = DragDropEffects.None; return; }
            PreviewGroupOnBoard(group, point);
            return;
        }
        var tile = DragTile(e);
        if (tile == null) { e.Effects = DragDropEffects.None; return; }
        if (GroupAt(point) is { } target)
        {
            var relative = GroupsPanel.TranslatePoint(point, target.Panel);
            var model = target.Panel.ToModelPoint(relative);
            var (column, row) = TileLayout.Snap(model.X - _dragOffset.X, model.Y - _dragOffset.Y, Cell, tile.Size, Document.Preferences.Columns);
            if (target.Panel.FolderOwner != null)
            {
                _folderHover.Reset();
                if (tile.IsFolder) { e.Effects = DragDropEffects.None; ClearDropPreview(); return; }
                ShowDropPreview(target.Panel, tile, column, row);
                return;
            }
            var sourceId = (string?)e.Data.GetData("ReStart.Tile") ?? (string?)e.Data.GetData("ReStart.App") ?? tile.Id;
            var folderTarget = FolderHoverTarget(tile, target.Panel, relative, sourceId);
            if (folderTarget != null)
            {
                var placement = target.Panel.Placements.Single(p => p.Tile == folderTarget);
                ShowDropPreview(target.Panel, folderTarget, placement.Column, placement.Row);
                _dropAdorner!.FolderTarget = true;
                _dropAdorner.InvalidateVisual();
            }
            else if (target.Group.IsStandalone) PreviewStandalone(tile, point);
            else ShowDropPreview(target.Panel, tile, column, row);
        }
        else
        {
            _folderHover.Reset();
            PreviewStandalone(tile, point);
        }
    }

    private void ShowBoardPreview(Rect bounds)
    {
        if (_dropAdorner?.AdornedElement != GroupsPanel)
        {
            ClearDropPreview();
            _dropAdorner = new(GroupsPanel) { IsHitTestVisible = false };
            System.Windows.Documents.AdornerLayer.GetAdornerLayer(GroupsPanel)?.Add(_dropAdorner);
        }
        _dropAdorner.CellBounds = bounds;
        _dropAdorner.FolderTarget = false;
        _dropAdorner.InvalidateVisual();
    }

    private void OnBoardDrop(object sender, DragEventArgs e)
    {
        if (!HasDrag(e)) return;
        e.Handled = true;
        ClearDropPreview();
        var point = e.GetPosition(GroupsPanel);
        if (e.Data.GetData("ReStart.Group") is string id)
        {
            var group = Document.Groups.FirstOrDefault(g => g.Id == id);
            if (group == null) return;
            MoveGroupOnBoard(group, point);
        }
        else if (GroupAt(point) is { } target)
        {
            if (target.Panel.FolderOwner is Tile folder)
            {
                if (DragTile(e) is not Tile tile || tile.IsFolder) return;
                var relative = e.GetPosition(target.Panel);
                var (column, row) = TileLayout.Snap(relative.X - _dragOffset.X, relative.Y - _dragOffset.Y, Cell, tile.Size, Document.Preferences.Columns);
                MoveTileIntoOpenFolder(tile, folder, column, row);
            }
            else DropInGroup(target.Group, target.Panel, e);
        }
        else if (DragTile(e) is Tile tile) MoveTileToStandalone(tile, point);
    }

    private void MoveGroupOnBoard(TileGroup group, Point point)
    {
        var (column, row, x) = BoardAnchor(point);
        GroupLayout.Move(Document, group, column, row, GroupsPanel.Columns, Cell, x);
        CommitLayout();
    }

    private void PreviewGroupOnBoard(TileGroup group, Point point)
    {
        var (column, row, x) = BoardAnchor(point);
        var placements = GroupLayout.PreviewMove(Document, group, column, row, GroupsPanel.Columns, Cell, x);
        var target = placements.Single(p => p.Group == group);
        ShowBoardPreview(new(target.Left, target.Row * Cell / 2, target.Width, target.Height));
        GroupsPanel.DragPlacements = placements;
        GroupsPanel.InvalidateMeasure();
    }

    private void MoveTileIntoGroup(Tile tile, TileGroup destination, int column, int row, Tile? folderTarget = null)
    {
        var source = Document.Groups.FirstOrDefault(g => g.Tiles.Any(t => t == tile || t.Children.Contains(tile)));
        if (folderTarget != null) MergeFolder(tile, folderTarget);
        else LayoutEditor.MoveToCell(Document, tile, destination, column, row);
        if (source != destination && source?.Tiles.Count == 0) Document.Groups.Remove(source);
        CommitLayout();
    }

    private (int Column, int Row, double X) BoardAnchor(Point point)
    {
        var pitch = Document.Preferences.Columns * Cell + GroupLayout.Gap;
        var x = Math.Clamp(Math.Round((point.X - _dragOffset.X) / Cell * 2, MidpointRounding.AwayFromZero) / 2, 0, 10000);
        var column = Math.Clamp((int)Math.Floor(x * Cell / pitch), 0, 63);
        var row = Math.Clamp((int)Math.Round((point.Y - _dragOffset.Y) / (Cell / 2), MidpointRounding.AwayFromZero), 0, 20000);
        return (column, row, x);
    }

    private void PreviewStandalone(Tile tile, Point point)
    {
        var (_, row, x) = BoardAnchor(point);
        var (width, height) = TileLayout.Dimensions(tile.Size);
        ShowBoardPreview(new(x * Cell,
            row * Cell / 2, width * Cell, height * Cell));
    }

    private void MoveTileToStandalone(Tile tile, Point point)
    {
        var (column, row, x) = BoardAnchor(point);
        LayoutEditor.Detach(Document, tile, column, row, 0, GroupsPanel.Columns, Cell, x);
        CommitLayout();
    }
}
