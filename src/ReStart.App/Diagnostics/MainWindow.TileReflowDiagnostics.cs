using System.Text.Json;
using System.Windows;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Exercises internal occupied-slot trajectories through the normal and selection planners.</summary>
    /// <returns>Checked preview, cancellation, ownership and undo invariants for fictional groups and open folders.</returns>
    private int VerifyInternalTileReflow()
    {
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        foreach (var editing in new[] { false, true })
            foreach (var folder in new[] { false, true })
                foreach (var vertical in new[] { false, true })
                    foreach (var size in Enum.GetValues<TileSize>())
                    {
                        _dragging = false; ClearDropPreview(); SetLayoutEditing(false); _selectionDragPositions = null;
                        var (width, height) = TileLayout.Dimensions(size);
                        var recipient = new Tile { Label = "Neighbor", Size = size, Column = 0, Row = 0 };
                        var moving = new Tile { Label = "Moving", Size = size, Column = vertical ? 0 : width, Row = vertical ? height : 0 };
                        var container = new Tile { Label = "Folder", Children = folder ? [recipient, moving] : [] };
                        var group = new TileGroup { Name = "Apps", X = 0, Column = 0, Row = 0, Tiles = folder ? [container] : [recipient, moving] };
                        var adjacent = new TileGroup { Name = "Unrelated group", X = width * 2 + 2, Column = 1, Row = 0, Tiles = [new() { Label = "Other app" }] };
                        Document = new()
                        {
                            Preferences = new() { Language = "en", Columns = 8, Animate = false, ShowWallpaper = false },
                            Groups = [group, adjacent]
                        };
                        _openFolder = folder ? container : null; ApplyAppearance(); RenderGroups(); SaveLayout(); _layoutHistory.Reset(Document);
                        SetLayoutEditing(editing); if (editing) SelectTileForEditing(moving.Id);
                        UpdateLayout(); _dragOffset = new(2, 2); _dragging = true; SetReorderGesture(true);
                        if (editing) _selectionDragPositions = CaptureTileSelection();
                        var panel = folder ? VisualChildren<TilePanel>(GroupsPanel).Single(p => p.FolderOwner == container) : RootPanelFor(group);
                        var origin = LayoutOrigin(panel, GroupsPanel);
                        var old = GroupLayout.Pack(Document.Groups, GroupsPanel.Columns, 8, Cell, expandedFolderId: _openFolder?.Id, preserveColumns: true)
                            .Single(p => p.Group == adjacent);
                        var before = JsonSerializer.Serialize(Document.Groups, LayoutStore.JsonOptions);
                        List<TileGroup>? selectionPlan = null; Point pointer = default;
                        foreach (var fraction in new[] { 0d, .2, .4, .6, .8, 1 })
                        {
                            pointer = new(origin.X + (vertical ? 0 : width * (1 - fraction) * Cell) + 2,
                                origin.Y + (vertical ? height * (1 - fraction) * Cell : 0) + 2);
                            if (editing)
                            {
                                selectionPlan = PlanSelectionDrop(moving.Id, pointer);
                                Check(selectionPlan != null, "Internal selection rearrangement was rejected.");
                                ShowSelectionPreview(selectionPlan!, pointer, moving.Id);
                            }
                            else Check(PreviewSingleTileDrop(moving, pointer, moving.Id, 0), "Internal rearrangement was rejected.");
                            var projected = _projectedGroups ?? throw new InvalidOperationException("Internal rearrangement had no projection.");
                            var target = projected.Single(g => g.Id == group.Id);
                            var tiles = folder ? target.Tiles.Single().Children : target.Tiles;
                            var positions = TileLayout.Pack(tiles, 8);
                            Check(positions.Max(p => p.Column + p.Width) <= (vertical ? width : width * 2)
                                && positions.Max(p => p.Row + p.Height) <= (vertical ? height * 2 : height), "An intermediate drag expanded its container.");
                            var neighbor = positions.Single(p => p.Tile.Id == recipient.Id);
                            Check((neighbor.Column == 0 && neighbor.Row == 0) || (neighbor.Column == moving.Column && neighbor.Row == moving.Row),
                                "An internal neighbor escaped its original or vacated slot.");
                            var placed = GroupLayout.Pack(projected, GroupsPanel.Columns, 8, Cell, expandedFolderId: _openFolder?.Id, preserveColumns: true)
                                .Single(p => p.Group.Id == adjacent.Id);
                            Check(placed.Left == old.Left && placed.Top == old.Top,
                                $"Internal rearrangement displaced an unrelated group ({size}, edit={editing}, folder={folder}, vertical={vertical}, progress={fraction}: {old.Left},{old.Top} to {placed.Left},{placed.Top}).");
                            Check(before == JsonSerializer.Serialize(Document.Groups, LayoutStore.JsonOptions), "A drag preview changed saved coordinates.");
                        }
                        ClearDropPreview();
                        Check(before == JsonSerializer.Serialize(Document.Groups, LayoutStore.JsonOptions), "Cancelling rearrangement changed the layout.");
                        if (editing) CommitSelectionPlan(selectionPlan!);
                        else Check(CommitSingleTileDrop(moving, pointer, moving.Id), "A previewed internal move failed to commit.");
                        _dragging = false; SetReorderGesture(false); _selectionDragPositions = null;
                        var committed = LayoutEditor.Find(Document, recipient.Id)!;
                        Check(committed.Column == (vertical ? 0 : width) && committed.Row == (vertical ? height : 0), "A neighbor did not use the vacated slot on commit.");
                        RestoreLayoutHistory(false);
                        Check(before == JsonSerializer.Serialize(Document.Groups, LayoutStore.JsonOptions),
                            $"Internal rearrangement did not undo in one step ({size}, edit={editing}, folder={folder}, vertical={vertical}).");
                    }
        _dragging = false; _openFolder = null; ClearDropPreview(); SetLayoutEditing(false); _selectionDragPositions = null;
        return checks;
    }
}
