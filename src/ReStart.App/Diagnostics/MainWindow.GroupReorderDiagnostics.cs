using System.Text.Json;
using System.Windows;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Exercises early group reordering through pointer snapping, preview cancellation and the real drop path.</summary>
    /// <param name="snapshotPath">Owned placement-fixture image path used to locate an additional fictional group preview.</param>
    /// <returns>Number of native preview, model, cancellation and commit invariants checked.</returns>
    private int VerifyGroupDragReordering(string snapshotPath)
    {
        var checks = 0;
        var editingBefore = _layoutEditing;
        var draggingBefore = _dragging;
        var offsetBefore = _dragOffset;
        void Check(bool value, string message) { Require(value, message); checks++; }
        try
        {
            foreach (var scale in new[] { .7, 1.0, 1.5 })
                foreach (var editing in new[] { false, true })
                    foreach (var downward in new[] { false, true })
                    {
                        ClearDropPreview();
                        var moving = new TileGroup { Name = "Moving group", X = 0, Column = 0, Row = downward ? 0 : 8, Tiles = [new() { Label = "Moving app", Column = 0, Row = 0 }] };
                        var target = new TileGroup { Name = "Recipient", X = editing ? .5 : 0, Column = 0, Row = downward ? 8 : 0, Tiles = [new() { Label = "Recipient app", Column = 0, Row = 0 }] };
                        var below = new TileGroup { Name = "Unrelated below", X = 0, Column = 0, Row = 32, Tiles = [new() { Label = "Sample app" }] };
                        var side = new TileGroup { Name = "Unrelated beside", X = 8, Column = 1, Row = 0, Tiles = [new() { Label = "Sample app", Size = TileSize.Large }] };
                        Document = new()
                        {
                            Preferences = new() { Language = "en", TileScale = scale, Animate = false, ShowWallpaper = false },
                            Groups = [below, target, side, moving]
                        };
                        _layoutEditing = editing; _dragging = true; _dragOffset = new(12, 10);
                        ApplyAppearance(); RenderGroups(); UpdateLayout();
                        var baseline = GroupsPanel.Placements.ToArray();
                        var origin = baseline.Single(p => p.Group == moving);
                        var recipient = baseline.Single(p => p.Group == target);
                        var half = Cell / 2;
                        var overlap = Math.Min(origin.Height, recipient.Height) / 2;
                        var threshold = downward ? (recipient.Top + overlap - origin.Height) / half : (recipient.Top + recipient.Height - overlap) / half;
                        var row = downward ? (int)Math.Ceiling(threshold) : (int)Math.Floor(threshold);
                        Point Pointer(int anchor) => new(_dragOffset.X, anchor * half + _dragOffset.Y);
                        var before = JsonSerializer.Serialize(Document, LayoutStore.JsonOptions);

                        PreviewGroupOnBoard(moving, Pointer(row + (downward ? -1 : 1))); UpdateLayout();
                        Check(GroupsPanel.Placements.Single(p => p.Group == target).Row == target.Row, "Shallow group overlap started pushing its recipient away.");
                        PreviewGroupOnBoard(moving, Pointer(row)); UpdateLayout();
                        Check(GroupsPanel.Placements.Single(p => p.Group == target).Row == (downward ? 0 : 8)
                            && GroupsPanel.Placements.Single(p => p.Group == moving).Row == (downward ? 8 : 0), "Half-overlap did not reorder groups through native pointer snapping.");
                        Check(GroupsPanel.Placements.Where(p => p.Group == below || p.Group == side).SequenceEqual(baseline.Where(p => p.Group == below || p.Group == side)),
                            "Reordering groups displaced an unrelated row or column.");
                        Check(before == JsonSerializer.Serialize(Document, LayoutStore.JsonOptions), "A group reorder preview changed saved anchors or child cells.");
                        var preview = GroupsPanel.DragPlacements;
                        PreviewGroupOnBoard(moving, Pointer(row)); UpdateLayout();
                        Check(ReferenceEquals(preview, GroupsPanel.DragPlacements), "A stationary group reorder rebuilt its preview.");
                        VerifyGroupBounds();
                        if (scale == 1 && !editing && downward)
                            SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "placement-group-reorder.png"));
                        ClearDropPreview(); UpdateLayout();
                        Check(GroupsPanel.Placements.SequenceEqual(baseline), "Cancelling an early group reorder did not restore its original slots.");
                        Document.Preferences.LockedGroupIds.Add(target.Id);
                        PreviewGroupOnBoard(moving, Pointer(row)); UpdateLayout();
                        Check(GroupsPanel.DragPlacements == null && GroupsPanel.Placements.SequenceEqual(baseline), "Group reordering bypassed a recipient's layout lock.");
                        Document.Preferences.LockedGroupIds.Clear();
                        MoveGroupOnBoard(moving, Pointer(row)); UpdateLayout();
                        Check(preview != null && GroupsPanel.Placements.SequenceEqual(preview), "An early group reorder committed different geometry than its preview.");
                        Check(moving.Tiles[0].Column == 0 && moving.Tiles[0].Row == 0 && target.Tiles[0].Column == 0 && target.Tiles[0].Row == 0,
                            "Group reordering changed local tile coordinates.");
                        VerifyGroupBounds();
                    }
        }
        finally
        {
            ClearDropPreview(); _layoutEditing = editingBefore; _dragging = draggingBefore; _dragOffset = offsetBefore;
        }
        return checks;
    }
}
