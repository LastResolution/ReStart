using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Checks native defaults, temporary drag modifiers and legacy-folder preservation through production drop routes.</summary>
    /// <param name="directory">Owned fictional fixture output; no production catalog or settings are read.</param>
    /// <returns>Number of checked geometry, ownership, persistence and cancellation invariants.</returns>
    private int VerifyNativePlacement(string directory)
    {
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        try
        {
            foreach (var editing in new[] { false, true })
                foreach (var legacy in new[] { false, true })
                    foreach (var size in Enum.GetValues<TileSize>())
                        foreach (var targetSize in Enum.GetValues<TileSize>())
                        {
                            var (source, target, panel) = Scene(editing, legacy, size, targetSize);
                            var point = Pointer(panel, target);
                            var original = JsonSerializer.Serialize(Document.Groups, LayoutStore.JsonOptions);
                            PreviewSingleTileDrop(source, point, source.Id, 0);
                            Check(_folderHover.TargetId == target.Id && _dropAdorner?.FolderTarget != true, "A native overlap did not wait for folder intent.");
                            PreviewSingleTileDrop(source, point, source.Id, TileFolderHover.DelayMilliseconds);
                            Check(_dropAdorner?.FolderTarget == true && _dropAdorner.GroupTarget == false, "Native overlap incorrectly offered an ordinary group.");
                            Check(CommitSingleTileDrop(source, point, source.Id), "A native folder drop was rejected.");
                            var folder = Document.Groups.SelectMany(g => g.Tiles).Single(t => t.IsFolder);
                            Check(folder.Children.Select(t => t.Id).ToHashSet().SetEquals([source.Id, target.Id]), "Native folder creation changed child identities.");
                            _dragging = false; RestoreLayoutHistory(false);
                            Check(original == JsonSerializer.Serialize(Document.Groups, LayoutStore.JsonOptions), "Native folder creation did not undo atomically.");
                        }

            foreach (var editing in new[] { false, true })
                foreach (var size in Enum.GetValues<TileSize>())
                {
                    var (source, _, _) = Scene(editing, false, size, TileSize.Medium);
                    var outside = new Point(18 * Cell + 3, 8 * Cell + GroupLayout.HeaderHeight + 3);
                    PreviewSingleTileDrop(source, outside, source.Id, 0);
                    Check(_dropAdorner?.GroupTarget == true, "Empty space did not preview an unnamed group by default.");
                    SetFreePlacementGesture(true);
                    PreviewSingleTileDrop(source, outside, source.Id, 0);
                    Check(_dropAdorner?.GroupTarget == false && !Document.Preferences.FreeTilePlacement, "Alt did not temporarily preview independent placement.");
                    SetFreePlacementGesture(false);
                    PreviewSingleTileDrop(source, outside, source.Id, 0);
                    Check(_dropAdorner?.GroupTarget == true, "Releasing Alt did not restore the native preview.");
                    var bounds = _dropAdorner!.CellBounds;
                    Check(CommitSingleTileDrop(source, outside, source.Id), "Native empty-space placement failed.");
                    UpdateLayout();
                    var entry = Document.Groups.Single(g => g.Tiles.Contains(source));
                    var placed = GroupsPanel.Placements.Single(p => p.Group == entry);
                    Check(!entry.IsStandalone && entry.Name.Length == 0 && new Rect(placed.Left, placed.Top, placed.Width, placed.Height) == bounds,
                        "Native preview geometry and committed unnamed group differ.");

                    (source, _, _) = Scene(editing, false, size, TileSize.Medium);
                    SetFreePlacementGesture(true);
                    PreviewSingleTileDrop(source, outside, source.Id, 0);
                    Check(CommitSingleTileDrop(source, outside, source.Id) && Document.Groups.Single(g => g.Tiles.Contains(source)).IsStandalone
                        && !Document.Preferences.FreeTilePlacement, "Alt placement changed the saved mode or acquired a group header.");

                    var (moving, target, panel) = Scene(editing, false, size, TileSize.Medium);
                    var point = Pointer(panel, target);
                    PreviewSingleTileDrop(moving, point, moving.Id, 0);
                    SetReorderGesture(true);
                    PreviewSingleTileDrop(moving, point, moving.Id, 1000);
                    Check(_folderHover.TargetId == null && _dropAdorner?.FolderTarget != true, "Shift retained folder intent.");
                    Check(CommitSingleTileDrop(moving, point, moving.Id), "Shift rearrangement was rejected.");
                    var positions = TileLayout.Pack(Document.Groups.Single().Tiles, Document.Preferences.Columns);
                    Check(positions.Single(p => p.Tile == moving).Row == 6 && positions.Single(p => p.Tile == moving).Column == 0
                        && positions.Single(p => p.Tile == target).Row != 6 && Document.Groups.SelectMany(g => g.Tiles).All(t => !t.IsFolder),
                        "Shift did not place the second tile into the first tile's position and displace its neighbor.");
                    if (size == TileSize.Medium && !editing) SaveScreenshot(Path.Combine(directory, "shift-rearrange.png"));

                    (moving, target, panel) = Scene(editing, false, size, TileSize.Medium);
                    point = Pointer(panel, target); SetReorderGesture(true);
                    PreviewSingleTileDrop(moving, point, moving.Id, 0); SetReorderGesture(false);
                    PreviewSingleTileDrop(moving, point, moving.Id, 1000);
                    Check(_dropAdorner?.FolderTarget != true, "Releasing Shift reused an obsolete folder dwell deadline.");
                }

            foreach (var legacy in new[] { false, true })
            {
                var (source, target, panel) = Scene(true, legacy, TileSize.Medium, TileSize.Medium);
                var extra = new Tile { Label = "Selected companion", Column = 2, Row = 0 };
                if (legacy) Document.Groups.Add(new() { IsStandalone = true, X = 16, Column = 2, Row = 0, Tiles = [extra] });
                else Document.Groups[0].Tiles.Add(extra);
                RenderGroups(); UpdateLayout();
                _selectedTileIds.Add(extra.Id); RefreshTileSelection(); _selectionDragPositions = CaptureTileSelection();
                panel = RootPanelFor(Document.Groups.Single(g => g.Tiles.Contains(target)));
                var point = Pointer(panel, target);
                _folderHover.Update(source.Id, target.Id, 0);
                _folderHover.Update(source.Id, target.Id, TileFolderHover.DelayMilliseconds);
                var plan = PlanSelectionDrop(source.Id, point);
                Check(plan != null, "A native selection could not form a folder.");
                var folder = plan!.SelectMany(g => g.Tiles).Single(t => t.IsFolder);
                Check(folder.Children.Select(t => t.Id).ToHashSet().SetEquals([source.Id, target.Id, extra.Id]), "A native batch folder lost a selected tile.");
            }
            _selectionDragPositions = null; _dragging = false; SetFreePlacementGesture(false); SetReorderGesture(false); ClearDropPreview();
            return checks;
        }
        catch (Exception ex) { throw new InvalidOperationException($"Native placement after {checks} checks: {ex.Message}", ex); }

        (Tile Source, Tile Target, TilePanel Panel) Scene(bool editing, bool legacy, TileSize sourceSize, TileSize targetSize)
        {
            _dragging = false; StopFolderDwell(); _folderHover.Reset(); ClearDropPreview(); SetLayoutEditing(false);
            _selectionDragPositions = null; _freePlacementGesture = false; _reorderGesture = false;
            var source = new Tile { Label = "Second tile", Column = 0, Row = 0, Size = sourceSize };
            var target = new Tile { Label = "First tile", Column = 0, Row = legacy ? 0 : 6, Size = targetSize };
            var group = new TileGroup
            {
                Name = legacy ? "" : "Apps",
                IsStandalone = legacy,
                X = legacy ? 8 : 0,
                Column = legacy ? 1 : 0,
                Row = legacy ? 12 : 0,
                Tiles = [target]
            };
            Document = new()
            {
                Preferences = new() { Language = "en", Animate = false, ShowWallpaper = false },
                Groups = legacy ? [new() { IsStandalone = true, X = 0, Column = 0, Row = 0, Tiles = [source] }, group] : [group]
            };
            if (!legacy) group.Tiles.Insert(0, source);
            ApplyAppearance(); RenderGroups(); SaveLayout(); _layoutHistory.Reset(Document); SetLayoutEditing(editing);
            if (editing) SelectTileForEditing(source.Id);
            UpdateLayout(); _dragOffset = new(3, 3); _dragging = true;
            return (source, target, RootPanelFor(group));
        }

        Point Pointer(TilePanel panel, Tile target) => panel.Children.OfType<Button>().Single(b => b.Tag == target).TranslatePoint(_dragOffset, GroupsPanel);
    }
}
