using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private readonly Dictionary<FrameworkElement, Transform> _dropTransforms = [];
    private (string Source, string? Group, string? Folder, int Column, int Row, double X, TileSize Size, int Render)? _dropPlanKey;
    private List<TileGroup>? _dropPlan;

    private void RestoreDropProjection()
    {
        var changed = _dropTransforms.Count > 0;
        foreach (var (element, transform) in _dropTransforms) element.RenderTransform = transform;
        _dropTransforms.Clear();
        if (changed) GroupsPanel.UpdateLayout();
    }

    private void ProjectSingleTileDrop(Tile source, SingleTileDrop drop, Point point)
    {
        if (drop.MergeTarget != null) return;
        if (drop.GroupPlan is { } grouping)
        {
            // Keep the overlap recipient stationary while its dwell timer owns
            // the pointer; unrelated displaced entries can still preview.
            ProjectDropGroups(grouping, source.Id, drop.Group?.Id); return;
        }
        if (_folderHover.TargetId != null) return;
        var independent = drop.Group is not { IsStandalone: false } && drop.Panel?.FolderOwner == null;
        var anchor = independent ? StandaloneAnchor(source, point) : (Column: drop.Column, Row: drop.Row, X: 0d);
        var key = (source.Id, drop.Group?.Id, drop.Panel?.FolderOwner?.Id, anchor.Column, anchor.Row, anchor.X, source.Size, _boardRenderCount);
        if (_dropPlanKey != key)
        {
            _dropPlanKey = key;
            var copy = new LayoutDocument { Preferences = Document.Preferences, Groups = LayoutHistory.CopyGroups(Document.Groups) };
            var tile = LayoutEditor.Find(copy, source.Id) ?? LayoutHistory.CopyGroups([new() { Tiles = [source] }])[0].Tiles[0];
            if (drop.Panel?.FolderOwner is { } folder) LayoutEditor.MoveIntoFolder(copy, tile, LayoutEditor.Find(copy, folder.Id)!, drop.Column, drop.Row);
            else if (drop.Group is { IsStandalone: false } group) LayoutEditor.MoveToCell(copy, tile, copy.Groups.Single(g => g.Id == group.Id), drop.Column, drop.Row);
            else LayoutEditor.Detach(copy, tile, anchor.Column, anchor.Row, 0, GroupsPanel.Columns, Cell, anchor.X);
            _dropPlan = PlacementAllowed(copy.Groups) ? copy.Groups : null;
        }
        if (_dropPlan != null) ProjectDropGroups(_dropPlan, source.Id);
    }

    private void ProjectDropGroups(List<TileGroup> groups, string movingId, string? stationaryGroup = null)
    {
        RestoreDropProjection();
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
            if (stack.Tag is not TileGroup group || group.Id == stationaryGroup) continue;
            var destination = placements.FirstOrDefault(p => p.Group.Id == group.Id);
            if (destination.Group == null) continue;
            Translate(stack, new(destination.Left, destination.Top));
        }
        GroupsPanel.UpdateLayout();
        foreach (var (id, view) in _selectionViews)
            if (id != movingId && destinations.TryGetValue(id, out var position)) Translate(view.Button, position);
        if (placements.Count > 0)
        {
            GroupsPanel.DragMinimumWidth = Math.Max(GroupsPanel.DragMinimumWidth, placements.Max(p => p.Left + p.Width));
            GroupsPanel.DragMinimumHeight = Math.Max(GroupsPanel.DragMinimumHeight, placements.Max(p => p.Top + p.Height));
            GroupsPanel.InvalidateMeasure();
        }
        GroupsPanel.UpdateLayout();

        void Translate(FrameworkElement element, Point destination)
        {
            var origin = element.TranslatePoint(new(), GroupsPanel);
            var delta = destination - origin;
            if (Math.Abs(delta.X) < .01 && Math.Abs(delta.Y) < .01) return;
            var previous = element.RenderTransform;
            _dropTransforms[element] = previous;
            var transform = new TransformGroup(); transform.Children.Add(previous); transform.Children.Add(new TranslateTransform(delta.X, delta.Y));
            element.RenderTransform = transform;
        }
    }
}
