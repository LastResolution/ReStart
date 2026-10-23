using System.Windows;
using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private List<TileGroup>? _projectedGroups;
    private string? _projectedSource, _projectedStationaryGroup;
    private (string Source, string? Group, string? Folder, int Column, int Row, double X, TileSize Size, int Render, bool Independent)? _dropPlanKey;
    private List<TileGroup>? _dropPlan;

    private void RestoreDropProjection(bool animate = false)
    {
        var changed = _tileTransitions.Count > 0;
        _tileTransitions.Reset(animate); _projectedGroups = null;
        if (changed && !animate) GroupsPanel.UpdateLayout();
        _groupBoundaries?.InvalidateVisual();
    }

    private void ProjectSingleTileDrop(Tile source, SingleTileDrop drop, Point point)
    {
        if (drop.MergeTarget != null) { RestoreDropProjection(MotionEnabled); return; }
        if (drop.GroupPlan is { } grouping)
        {
            // Keep the overlap recipient stationary while its dwell timer owns
            // the pointer; unrelated displaced entries can still preview.
            ProjectDropGroups(grouping, source.Id, drop.Group?.Id); return;
        }
        if (_folderHover.TargetId != null) { RestoreDropProjection(MotionEnabled); return; }
        var independent = drop.Group is not { IsStandalone: false } && drop.Panel?.FolderOwner == null;
        var anchor = independent ? StandaloneAnchor(source, point) : (Column: drop.Column, Row: drop.Row, X: 0d);
        var key = (source.Id, drop.Group?.Id, drop.Panel?.FolderOwner?.Id, anchor.Column, anchor.Row, anchor.X, source.Size, _boardRenderCount, FreePlacementActive);
        if (_dropPlanKey != key)
        {
            _dropPlanKey = key;
            var copy = new LayoutDocument { Preferences = Document.Preferences, Groups = LayoutHistory.CopyGroups(Document.Groups) };
            var tile = LayoutEditor.Find(copy, source.Id) ?? LayoutHistory.CopyGroups([new() { Tiles = [source] }])[0].Tiles[0];
            if (drop.Panel?.FolderOwner is { } folder) LayoutEditor.MoveIntoFolder(copy, tile, LayoutEditor.Find(copy, folder.Id)!, drop.Column, drop.Row);
            else if (drop.Group is { IsStandalone: false } group) LayoutEditor.MoveToCell(copy, tile, copy.Groups.Single(g => g.Id == group.Id), drop.Column, drop.Row);
            else LayoutEditor.Detach(copy, tile, anchor.Column, anchor.Row, 0, GroupsPanel.Columns, Cell, anchor.X, independent: FreePlacementActive);
            _dropPlan = PlacementAllowed(copy.Groups) ? copy.Groups : null;
        }
        if (_dropPlan != null)
        {
            ProjectDropGroups(_dropPlan, source.Id);
            if (independent)
            {
                var placed = GroupLayout.Pack(_dropPlan, GroupsPanel.Columns, Document.Preferences.Columns, Cell, preserveColumns: true)
                    .Single(p => p.Group.Tiles.Any(t => t.Id == source.Id));
                _dropAdorner!.CellBounds = new(placed.Left, placed.Top, placed.Width, placed.Height);
                if (!FreePlacementActive) _dropAdorner.GroupBounds = _dropAdorner.CellBounds;
                _dropAdorner.InvalidateVisual();
            }
        }
    }

    private void ProjectDropGroups(List<TileGroup> groups, string movingId, string? stationaryGroup = null)
    {
        if (ReferenceEquals(groups, _projectedGroups) && _projectedSource == movingId && _projectedStationaryGroup == stationaryGroup) return;
        _projectedGroups = groups; _projectedSource = movingId; _projectedStationaryGroup = stationaryGroup;
        var placements = GroupLayout.Pack(groups, GroupsPanel.Columns, Document.Preferences.Columns, Cell, expandedFolderId: _openFolder?.Id, preserveColumns: true);
        var destinations = new Dictionary<string, Point>();
        foreach (var p in placements)
        {
            if (p.Group.Id == stationaryGroup) continue;
            var tiles = TileLayout.Pack(p.Group.Tiles, Document.Preferences.Columns);
            var expansion = FolderExpansion.Create(tiles, Document.Preferences.Columns, _openFolder?.Id);
            var compact = p.Group.IsStandalone && expansion == null;
            var header = p.Group.IsStandalone ? 0 : GroupLayout.HeaderHeight;
            foreach (var t in tiles)
                destinations[t.Tile.Id] = new(p.Left + (compact ? 0 : t.Column * Cell), p.Top + header + (expansion?.TileY(t, Cell) ?? t.Row * Cell));
            if (expansion != null)
                foreach (var t in expansion.Children)
                    destinations[t.Tile.Id] = new(p.Left + t.Column * Cell, p.Top + header + expansion.Row * Cell + GroupLayout.HeaderHeight + Controls.InlineFolderView.ContentTopInset + t.Row * Cell);
        }
        foreach (var stack in GroupsPanel.Children.OfType<StackPanel>())
        {
            if (stack.Tag is not TileGroup group) continue;
            var destination = placements.FirstOrDefault(p => p.Group.Id == group.Id);
            if (stack.Children.OfType<Controls.GroupHeader>().FirstOrDefault() is not { } header) continue;
            var origin = LayoutOrigin(stack, GroupsPanel);
            _tileTransitions.Move(header, group.Id != stationaryGroup && destination.Group != null ? new(destination.Left - origin.X, destination.Top - origin.Y) : new(), MotionEnabled);
        }
        foreach (var (id, view) in _selectionViews)
        {
            var origin = LayoutOrigin(view.Button, GroupsPanel);
            var delta = id != movingId && (_selectionDragPositions == null || !_selectedTileIds.Contains(id)) && destinations.TryGetValue(id, out var position)
                ? position - origin : new Vector();
            _tileTransitions.Move(view.Button, delta, MotionEnabled);
        }
        if (placements.Count > 0)
        {
            var width = Math.Max(GroupsPanel.DragMinimumWidth, placements.Max(p => p.Left + p.Width));
            var height = Math.Max(GroupsPanel.DragMinimumHeight, placements.Max(p => p.Top + p.Height));
            if (width != GroupsPanel.DragMinimumWidth || height != GroupsPanel.DragMinimumHeight)
            { GroupsPanel.DragMinimumWidth = width; GroupsPanel.DragMinimumHeight = height; GroupsPanel.InvalidateMeasure(); }
        }
        // Installing a transform wrapper requires one arrange. Later frames
        // change only its retained offset; stationary targets skip this method.
        GroupsPanel.UpdateLayout();
    }
}
