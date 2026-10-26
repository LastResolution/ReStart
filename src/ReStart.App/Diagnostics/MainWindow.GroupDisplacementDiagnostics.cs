using System.Text.Json;
using System.Windows;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Checks horizontal slot reuse and diagonal source-side displacement through native pointer preview/drop paths.</summary>
    /// <param name="snapshotPath">Owned placement-fixture image path locating fictional directional previews.</param>
    /// <returns>Number of placement, cancellation, cache, lock and persistence invariants checked.</returns>
    private int VerifyDirectionalGroupDisplacement(string snapshotPath)
    {
        var checks = 0;
        var editingBefore = _layoutEditing; var draggingBefore = _dragging; var offsetBefore = _dragOffset;
        void Check(bool value, string message) { Require(value, message); checks++; }
        var cases = (from scale in new[] { .7, 1.0, 1.5 }
                     from editing in new[] { false, true }
                     from right in new[] { false, true }
                     select (Scale: scale, Editing: editing, X: right ? 8.0 : 0.0, Row: 0, TargetX: right ? 0.0 : 8.0, TargetRow: 0, Side: right ? "right" : "left")).ToList();
        cases.AddRange([(1, false, 6, 0, 4, 8, "up"), (1, false, 6, 16, 4, 8, "down"),
            (1, false, 12, 0, 4, 2, "right"), (1, false, 0, 0, 8, 2, "left")]);
        try
        {
            foreach (var sample in cases)
            {
                ClearDropPreview();
                var moving = new TileGroup { Name = "Moving group", X = sample.X, Column = 0, Row = sample.Row, Tiles = [new() { Label = "Moving app", Column = 0, Row = 0 }] };
                var target = new TileGroup { Name = "Recipient", X = sample.TargetX, Column = 0, Row = sample.TargetRow, Tiles = [new() { Label = "Recipient app", Column = 0, Row = 0 }] };
                var below = new TileGroup { Name = "Unrelated below", X = 0, Column = 0, Row = 32, Tiles = [new() { Label = "Sample app" }] };
                Document = new() { Preferences = new() { Language = "en", TileScale = sample.Scale, Animate = false, ShowWallpaper = false }, Groups = [below, target, moving] };
                _layoutEditing = sample.Editing; _dragging = true; _dragOffset = new(12, 10);
                ApplyAppearance(); RenderGroups(); UpdateLayout();
                var baseline = GroupsPanel.Placements.ToArray();
                var before = JsonSerializer.Serialize(Document, LayoutStore.JsonOptions);
                var pointer = new Point(sample.TargetX * Cell + _dragOffset.X, sample.TargetRow * Cell / 2 + _dragOffset.Y);
                PreviewGroupOnBoard(moving, pointer); UpdateLayout();
                var incoming = GroupsPanel.Placements.Single(p => p.Group == moving);
                var recipient = GroupsPanel.Placements.Single(p => p.Group == target);
                Check(sample.Side switch
                {
                    "right" => recipient.Left >= incoming.Left + incoming.Width - .001,
                    "left" => recipient.Left + recipient.Width <= incoming.Left + .001,
                    "down" => recipient.Top >= incoming.Top + incoming.Height - .001,
                    _ => recipient.Top + recipient.Height <= incoming.Top + .001
                }, "Native group displacement ignored the incoming source side.");
                Check(sample.Side is "up" or "down" || recipient.Row == sample.TargetRow, "Horizontal displacement unnecessarily added a row.");
                Check(GroupsPanel.Placements.Single(p => p.Group == below) == baseline.Single(p => p.Group == below), "Directional displacement moved an unrelated group.");
                Check(before == JsonSerializer.Serialize(Document, LayoutStore.JsonOptions), "Directional preview changed the saved layout.");
                var preview = GroupsPanel.DragPlacements;
                PreviewGroupOnBoard(moving, pointer); UpdateLayout();
                Check(ReferenceEquals(preview, GroupsPanel.DragPlacements), "A stationary directional preview rebuilt its plan.");
                VerifyGroupBounds();
                if (sample.Scale == 1 && !sample.Editing && sample.Row == 0 && sample.TargetRow == 0)
                    SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "placement-from-" + sample.Side + ".png"));
                ClearDropPreview(); UpdateLayout();
                Check(GroupsPanel.Placements.SequenceEqual(baseline), "Cancelling directional displacement did not restore original anchors.");
                Document.Preferences.LockedGroupIds.Add(target.Id);
                PreviewGroupOnBoard(moving, pointer); UpdateLayout();
                Check(GroupsPanel.DragPlacements == null && GroupsPanel.Placements.SequenceEqual(baseline), "Directional displacement bypassed a group lock.");
                Document.Preferences.LockedGroupIds.Clear();
                MoveGroupOnBoard(moving, pointer); UpdateLayout();
                Check(preview != null && GroupsPanel.Placements.SequenceEqual(preview), "Directional preview and committed anchors disagree.");
                Check(moving.Tiles[0].Column == 0 && moving.Tiles[0].Row == 0 && target.Tiles[0].Column == 0 && target.Tiles[0].Row == 0,
                    "Directional displacement changed local tile cells.");
                VerifyGroupBounds();
            }
        }
        finally { ClearDropPreview(); _layoutEditing = editingBefore; _dragging = draggingBefore; _dragOffset = offsetBefore; }
        return checks;
    }
}
