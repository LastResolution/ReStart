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
    private (string Id, int Column, int Row, double X, int Columns, double Cell, int Render)? _groupPreviewKey;

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

    private void PreserveDragExtent()
    {
        // Moving the lowest/rightmost group must not shrink the viewport under
        // the pointer. Otherwise offset coercion feeds back into the next drag.
        GroupsPanel.DragMinimumHeight = Math.Max(GroupsPanel.DragMinimumHeight, ContentScroll.ExtentHeight);
        var occupiedWidth = GroupsPanel.Placements.Count == 0 ? 0 : GroupsPanel.Placements.Max(p => p.Left + p.Width);
        GroupsPanel.DragMinimumWidth = Math.Max(GroupsPanel.DragMinimumWidth, occupiedWidth);
    }

    private sealed record FolderOverlap(Tile Tile, double Ratio, double Distance);
    private bool CreatesGroup(TilePanel panel, Tile target) => Document.Preferences.FreeTilePlacement && Document.Preferences.GroupTilesOnDrop
        && panel.Tag is TileGroup { IsStandalone: true } && !target.IsFolder;
    private int HoverDelay(TilePanel panel, Tile? target) => target != null && CreatesGroup(panel, target)
        ? TileFolderHover.GroupDelayMilliseconds : TileFolderHover.DelayMilliseconds;

    private FolderOverlap? FindFolderOverlap(Tile source, TilePanel panel, Point point, bool existingFoldersOnly = false)
    {
        if (panel.FolderOwner != null || _reorderGesture) return null;
        var (width, height) = TileLayout.Dimensions(source.Size);
        var dragged = new Rect(point.X - _dragOffset.X, point.Y - _dragOffset.Y, width * Cell - 4, height * Cell - 4);
        return panel.Placements
            .Where(p => p.Tile.Id != source.Id
                && (p.Tile.IsFolder || (CreatesGroup(panel, p.Tile) ? Document.Preferences.GroupTilesOnDrop : Document.Preferences.CreateFoldersOnDrop))
                && (!source.IsFolder || CreatesGroup(panel, p.Tile))
                && (_selectionDragPositions == null || !_selectedTileIds.Contains(p.Tile.Id))
                && (!existingFoldersOnly || p.Tile.IsFolder && !_selectedTileIds.Contains(p.Tile.Id)))
            .Select(p => (p.Tile, Bounds: new Rect(p.Column * Cell, panel.Expansion?.TileY(p, Cell) ?? p.Row * Cell, p.Width * Cell - 4, p.Height * Cell - 4)))
            .Select(p =>
            {
                var overlap = Rect.Intersect(dragged, p.Bounds);
                var delta = new Point(dragged.X + dragged.Width / 2, dragged.Y + dragged.Height / 2) - new Point(p.Bounds.X + p.Bounds.Width / 2, p.Bounds.Y + p.Bounds.Height / 2);
                return new FolderOverlap(p.Tile, overlap.IsEmpty ? 0 : overlap.Width * overlap.Height / Math.Min(dragged.Width * dragged.Height, p.Bounds.Width * p.Bounds.Height), delta.LengthSquared);
            })
            .Where(p => p.Ratio >= (CreatesGroup(panel, p.Tile) ? _folderHover.TargetId == p.Tile.Id ? .30 : .40 : _folderHover.TargetId == p.Tile.Id ? .55 : .65))
            .OrderByDescending(p => p.Ratio).ThenBy(p => p.Distance).FirstOrDefault();
    }

    private Tile? FolderHoverTarget(Tile source, TilePanel panel, Point point, string sourceId, long? timestamp = null, bool existingFoldersOnly = false)
    {
        var target = FindFolderOverlap(source, panel, point, existingFoldersOnly)?.Tile;
        var armed = _folderHover.Update(sourceId, target?.Id, timestamp ?? Environment.TickCount64, HoverDelay(panel, target));
        if (timestamp == null && _dragging) WatchFolderDwell(source, panel, point, sourceId, target, existingFoldersOnly);
        return armed ? target : null;
    }
    private (TileGroup Group, TilePanel Panel)? GroupAt(Point point)
    {
        // Presentation offsets must never change the recipient while neighbors
        // animate. Resolve the same rectangular layout slots used before motion.
        foreach (var stack in GroupsPanel.Children.OfType<StackPanel>().Reverse())
        {
            if (stack.Tag is not TileGroup group || stack.Children.OfType<TilePanel>().FirstOrDefault() is not { } panel) continue;
            foreach (var inline in VisualChildren<InlineFolderView>(stack))
                if (new Rect(LayoutOrigin(inline, GroupsPanel), inline.RenderSize).Contains(point)) return (group, inline.Tiles);
            if (new Rect(LayoutOrigin(stack, GroupsPanel), stack.RenderSize).Contains(point)) return (group, panel);
        }
        // A compact open folder must still accept the next child column. This
        // transient drop zone adds no invisible layout/collision footprint.
        foreach (var inline in VisualChildren<TilePanel>(GroupsPanel).Where(p => p.FolderOwner != null))
        {
            var origin = LayoutOrigin(inline, GroupsPanel);
            var right = origin.X + Math.Min(Document.Preferences.Columns * Cell, inline.ActualWidth + Cell);
            if (point.X < origin.X || point.X > right || point.Y < origin.Y || point.Y > origin.Y + inline.ActualHeight + Cell) continue;
            var owner = Document.Groups.FirstOrDefault(g => g.Tiles.Contains(inline.FolderOwner!));
            if (owner != null) return (owner, inline);
        }
        var adjacent = GroupLayout.DropRecipient(GroupsPanel.Placements, point.X, point.Y, Cell);
        var destination = GroupsPanel.Children.OfType<StackPanel>().FirstOrDefault(s => s.Tag == adjacent)?.Children.OfType<TilePanel>().FirstOrDefault();
        return adjacent != null && destination != null ? (adjacent, destination) : null;
    }

    private (TileGroup Group, TilePanel Panel)? TileDropTarget(Tile tile, Point pointer)
    {
        var (width, height) = TileLayout.Dimensions(tile.Size);
        var center = new Point(pointer.X - _dragOffset.X + (width * Cell - 4) / 2,
            pointer.Y - _dragOffset.Y + (height * Cell - 4) / 2);
        var centered = GroupAt(center);
        if (centered?.Panel.FolderOwner != null) return centered;
        // A wide/large source can completely cover a small independent target
        // while its center lies outside that target's compact group rectangle.
        var overlapping = GroupsPanel.Children.OfType<StackPanel>()
            .Where(s => s.Tag is TileGroup)
            .Select(s => (Group: (TileGroup)s.Tag, Panel: s.Children.OfType<TilePanel>().FirstOrDefault()))
            .Where(p => p.Panel != null)
            .Select(p => (p.Group, Panel: p.Panel!, Overlap: FindFolderOverlap(tile, p.Panel!, PanelPoint(pointer, p.Panel!))))
            .Where(p => p.Overlap != null).OrderByDescending(p => p.Overlap!.Ratio).ThenBy(p => p.Overlap!.Distance).FirstOrDefault();
        if (overlapping.Panel != null) return (overlapping.Group, overlapping.Panel);
        var leading = GroupAt(new(pointer.X - _dragOffset.X + .5, pointer.Y - _dragOffset.Y + .5));
        return leading?.Panel.FolderOwner != null ? leading : centered ?? leading;
    }

    private sealed record SingleTileDrop(TileGroup? Group = null, TilePanel? Panel = null,
        int Column = 0, int Row = 0, Tile? MergeTarget = null, List<TileGroup>? GroupPlan = null);

    private SingleTileDrop? ResolveSingleTileDrop(Tile tile, Point point, string sourceId, long? timestamp = null)
    {
        if (!CanEditLayout || !CanEditTile(tile.Id)) return null;
        if (TileDropTarget(tile, point) is not { } target)
        {
            _folderHover.Reset(); StopFolderDwell();
            return new();
        }
        if (!CanEditGroup(target.Group)) return null;
        var relative = PanelPoint(point, target.Panel);
        var model = target.Panel.ToModelPoint(relative);
        var (column, row) = TileLayout.SnapMove(target.Panel.FolderOwner?.Children ?? target.Group.Tiles,
            tile, model.X - _dragOffset.X, model.Y - _dragOffset.Y, Cell, Document.Preferences.Columns);
        if (target.Panel.FolderOwner != null)
        {
            _folderHover.Reset(); StopFolderDwell();
            return tile.IsFolder ? null : new(target.Group, target.Panel, column, row);
        }
        var merge = FolderHoverTarget(tile, target.Panel, relative, sourceId, timestamp);
        if (merge != null && CreatesGroup(target.Panel, merge))
        {
            var plan = TileGrouping.Plan(Document, [tile], merge.Id, GroupsPanel.Columns, Cell);
            return plan == null || !PlacementAllowed(plan) ? null : new(target.Group, target.Panel, column, row, GroupPlan: plan);
        }
        return new(target.Group, target.Panel, column, row, merge);
    }

    private bool PreviewSingleTileDrop(Tile tile, Point point, string sourceId, long? timestamp = null)
    {
        var drop = ResolveSingleTileDrop(tile, point, sourceId, timestamp);
        if (drop == null) { ClearDropPreview(); return false; }
        if (drop.GroupPlan is { } plan) ShowGroupCreationPreview(plan, drop.Group!.Id);
        else if (drop.MergeTarget is Tile merge)
        {
            var placement = drop.Panel!.Placements.Single(p => p.Tile == merge);
            ShowDropPreview(drop.Panel, merge, placement.Column, placement.Row);
            _dropAdorner!.FolderTarget = true;
            _dropAdorner.Caption = L.T(K.DropIntoFolder);
            _dropAdorner.InvalidateVisual();
        }
        else if (drop.Panel?.FolderOwner != null || drop.Group is { IsStandalone: false })
            ShowDropPreview(drop.Panel!, tile, drop.Column, drop.Row);
        else PreviewStandalone(tile, point);
        ShowPendingFolderPreview();
        ProjectSingleTileDrop(tile, drop, point);
        return true;
    }

    private bool CommitSingleTileDrop(Tile tile, Point point, string sourceId)
    {
        // Resolve before clearing the preview: the same dwell state and OLE
        // coordinates must decide both the visible destination and ownership.
        var drop = ResolveSingleTileDrop(tile, point, sourceId);
        ClearDropPreview(); StopFolderDwell();
        if (drop == null) return false;
        if (drop.GroupPlan is { } plan) { Document.Groups = plan; CommitLayout(); }
        else if (drop.Panel?.FolderOwner is Tile folder) MoveTileIntoOpenFolder(tile, folder, drop.Column, drop.Row);
        else if (drop.MergeTarget != null || drop.Group is { IsStandalone: false })
            MoveTileIntoGroup(tile, drop.Group!, drop.Column, drop.Row, drop.MergeTarget);
        else MoveTileToStandalone(tile, point);
        return true;
    }

    private void OnBoardDragOver(object sender, DragEventArgs e)
    {
        if (!HasDrag(e)) return;
        SetFreePlacementGesture(e.KeyStates.HasFlag(DragDropKeyStates.AltKey));
        SetReorderGesture(e.KeyStates.HasFlag(DragDropKeyStates.ShiftKey));
        e.Handled = true;
        if (!CanEditLayout) { e.Effects = DragDropEffects.None; ClearDropPreview(); return; }
        e.Effects = DragDropEffects.Move;
        var point = e.GetPosition(GroupsPanel);
        if (e.Data.GetData("ReStart.Selection") is string anchorId)
        { HandleSelectionDrop(e, anchorId, point, false); return; }
        if (e.Data.GetData("ReStart.Group") is string groupId)
        {
            _folderHover.Reset();
            StopFolderDwell();
            var group = Document.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group == null || !CanEditGroup(group)) { e.Effects = DragDropEffects.None; return; }
            PreviewGroupOnBoard(group, point);
            return;
        }
        var tile = DragTile(e);
        if (tile == null) { e.Effects = DragDropEffects.None; return; }
        var sourceId = (string?)e.Data.GetData("ReStart.Tile") ?? (string?)e.Data.GetData("ReStart.App") ?? tile.Id;
        e.Effects = PreviewSingleTileDrop(tile, point, sourceId) ? DragDropEffects.Move : DragDropEffects.None;
    }

    private void ShowBoardPreview(Rect bounds)
    {
        if (_dropAdorner?.AdornedElement != GroupsPanel)
        {
            ClearDropPreview();
            _dropAdorner = new(GroupsPanel) { IsHitTestVisible = false, ShowHints = Document.Preferences.ShowDropHints };
            System.Windows.Documents.AdornerLayer.GetAdornerLayer(GroupsPanel)?.Add(_dropAdorner);
        }
        _dropAdorner.CellBounds = bounds;
        _dropAdorner.SelectionBounds = null;
        _dropAdorner.FolderTarget = false;
        _dropAdorner.HoverProgress = 0;
        _dropAdorner.GroupTarget = false;
        _dropAdorner.GroupBounds = null;
        _dropAdorner.Caption = L.T(K.DropIndependent);
        _dropAdorner.InvalidateVisual();
    }

    private void ShowGroupCreationPreview(List<TileGroup> plan, string groupId)
    {
        var group = GroupLayout.Pack(plan, GroupsPanel.Columns, Document.Preferences.Columns, Cell, preserveColumns: true).Single(p => p.Group.Id == groupId);
        ShowBoardPreview(new(group.Left, group.Top, group.Width, group.Height));
        _dropAdorner!.GroupTarget = true;
        _dropAdorner.GroupBounds = _dropAdorner.CellBounds;
        _dropAdorner.SelectionBounds = TileLayout.Pack(group.Group.Tiles, Document.Preferences.Columns)
            .Select(p => new Rect(group.Left + p.Column * Cell, group.Top + GroupLayout.HeaderHeight + p.Row * Cell, p.Width * Cell - 4, p.Height * Cell - 4)).ToArray();
        _dropAdorner.Caption = L.T(K.DropCreateGroup);
        _dropAdorner.InvalidateVisual();
    }

    private void OnBoardDrop(object sender, DragEventArgs e)
    {
        if (!HasDrag(e)) return;
        SetFreePlacementGesture(e.KeyStates.HasFlag(DragDropKeyStates.AltKey));
        SetReorderGesture(e.KeyStates.HasFlag(DragDropKeyStates.ShiftKey));
        e.Handled = true;
        if (!CanEditLayout) { e.Effects = DragDropEffects.None; ClearDropPreview(); return; }
        var point = e.GetPosition(GroupsPanel);
        if (e.Data.GetData("ReStart.Selection") is string anchorId)
        { ClearDropPreview(); StopFolderDwell(); HandleSelectionDrop(e, anchorId, point, true); return; }
        if (e.Data.GetData("ReStart.Group") is string id)
        {
            ClearDropPreview(); StopFolderDwell();
            var group = Document.Groups.FirstOrDefault(g => g.Id == id);
            if (group == null) return;
            MoveGroupOnBoard(group, point);
        }
        else if (DragTile(e) is Tile tile)
        {
            var sourceId = (string?)e.Data.GetData("ReStart.Tile") ?? (string?)e.Data.GetData("ReStart.App") ?? tile.Id;
            e.Effects = CommitSingleTileDrop(tile, point, sourceId) ? DragDropEffects.Move : DragDropEffects.None;
        }
    }

    private void MoveGroupOnBoard(TileGroup group, Point point)
    {
        if (!CanEditGroup(group)) return;
        var (column, row, x) = BoardAnchor(point, group.IsStandalone);
        x = GroupDropX(group, x, row);
        row = GroupDropRow(group, x, row);
        GroupLayout.Move(Document, group, column, row, GroupsPanel.Columns, Cell, x, reorderOnDrag: true);
        CommitLayout();
    }

    private void PreviewGroupOnBoard(TileGroup group, Point point)
    {
        if (!CanEditGroup(group)) { ClearDropPreview(); return; }
        var (column, row, x) = BoardAnchor(point, group.IsStandalone);
        var key = (group.Id, column, row, x, GroupsPanel.Columns, Cell, _boardRenderCount);
        if (_groupPreviewKey == key && GroupsPanel.DragPlacements != null) return;
        x = GroupDropX(group, x, row);
        row = GroupDropRow(group, x, row);
        var candidate = new LayoutDocument { Preferences = Document.Preferences, Groups = LayoutHistory.CopyGroups(Document.Groups) };
        GroupLayout.Move(candidate, candidate.Groups.Single(g => g.Id == group.Id), column, row, GroupsPanel.Columns, Cell, x, reorderOnDrag: true);
        if (!PlacementAllowed(candidate.Groups)) { ClearDropPreview(); return; }
        var placements = GroupLayout.PreviewMove(Document, group, column, row, GroupsPanel.Columns, Cell, x, reorderOnDrag: true);
        if (GroupsPanel.DragPlacements is { } previous && placements.SequenceEqual(previous)) { _groupPreviewKey = key; return; }
        var target = placements.Single(p => p.Group == group);
        ShowBoardPreview(new(target.Left, target.Top, target.Width, target.Height));
        _dropAdorner!.Caption = L.T(K.DropMoveGroup);
        GroupsPanel.DragPlacements = placements;
        _groupPreviewKey = key;
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

    private double GroupDropX(TileGroup group, double x, int row)
    {
        var placements = GroupLayout.Pack(Document.Groups, GroupsPanel.Columns, Document.Preferences.Columns, Cell, preserveColumns: true);
        var moving = placements.Single(p => p.Group == group);
        var beside = GroupLayout.SnapBeside(placements, group, x * Cell, row * Cell / 2, moving.Width, moving.Height, Cell);
        return GroupLayout.SnapAlignedX(placements, group, beside, row * Cell / 2, moving.Width, moving.Height, Cell) / Cell;
    }

    private int GroupDropRow(TileGroup group, double x, int row)
    {
        var placements = GroupLayout.Pack(Document.Groups, GroupsPanel.Columns, Document.Preferences.Columns, Cell, preserveColumns: true);
        var moving = placements.Single(p => p.Group == group);
        return GroupLayout.SnapRow(placements, group, x * Cell, row, moving.Width, moving.Height, Cell);
    }

    private (int Column, int Row, double X) BoardAnchor(Point point, bool standalone = false)
    {
        var pitch = Document.Preferences.Columns * Cell + GroupLayout.Gap;
        var x = Math.Clamp(Math.Round((point.X - _dragOffset.X) / Cell * 2, MidpointRounding.AwayFromZero) / 2, 0, 10000);
        var column = Math.Clamp((int)Math.Floor(x * Cell / pitch), 0, 63);
        var row = Math.Clamp((int)Math.Round((point.Y - _dragOffset.Y - (standalone ? GroupLayout.HeaderHeight : 0)) / (Cell / 2), MidpointRounding.AwayFromZero), 0, 20000);
        return (column, row, x);
    }

    private void PreviewStandalone(Tile tile, Point point)
    {
        var (_, row, x) = StandaloneAnchor(tile, point);
        var (width, height) = TileLayout.Dimensions(tile.Size);
        ShowBoardPreview(new(x * Cell,
            (FreePlacementActive ? GroupLayout.HeaderHeight : 0) + row * Cell / 2, width * Cell, height * Cell + (FreePlacementActive ? 0 : GroupLayout.HeaderHeight)));
        if (!FreePlacementActive)
        {
            _dropAdorner!.GroupTarget = true; _dropAdorner.GroupBounds = _dropAdorner.CellBounds;
            _dropAdorner.Caption = L.T(K.DropNewGroup); _dropAdorner.InvalidateVisual();
        }
    }

    private void MoveTileToStandalone(Tile tile, Point point)
    {
        var (column, row, x) = StandaloneAnchor(tile, point);
        LayoutEditor.Detach(Document, tile, column, row, 0, GroupsPanel.Columns, Cell, x, independent: FreePlacementActive);
        CommitLayout();
    }

    private (int Column, int Row, double X) StandaloneAnchor(Tile tile, Point point)
    {
        var (_, row, x) = BoardAnchor(point, true);
        var owner = Document.Groups.FirstOrDefault(g => g.IsStandalone && g.Tiles.Contains(tile));
        var moving = FreePlacementActive && owner != null ? owner : new TileGroup { IsStandalone = FreePlacementActive };
        var placements = GroupLayout.Pack(Document.Groups, GroupsPanel.Columns, Document.Preferences.Columns, Cell, preserveColumns: true)
            .Where(p => p.Group != owner || p.Group == moving).ToArray();
        var (width, height) = TileLayout.Dimensions(tile.Size);
        var top = (FreePlacementActive ? GroupLayout.HeaderHeight : 0) + row * Cell / 2;
        var footprintHeight = height * Cell + (FreePlacementActive ? 0 : GroupLayout.HeaderHeight);
        var beside = GroupLayout.SnapBeside(placements, moving, x * Cell, top, width * Cell, footprintHeight, Cell);
        x = GroupLayout.SnapAlignedX(placements, moving, beside, top, width * Cell, footprintHeight, Cell) / Cell;
        row = GroupLayout.SnapRow(placements, moving, x * Cell, row, width * Cell, footprintHeight, Cell);
        var column = Math.Clamp((int)(x * Cell / (Document.Preferences.Columns * Cell + GroupLayout.Gap)), 0, 63);
        return (column, row, x);
    }
}
