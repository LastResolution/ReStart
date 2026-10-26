using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Exercises placement, routed inline-name input, and persistence in an isolated preview executable.</summary>
    internal PlacementSmokeResult VerifyPlacement(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("Placement verification requires preview mode.");
        var original = Document;
        var originalWidth = Root.Width;
        var originalHeight = Root.Height;
        try
        {
            var a = new Tile { Label = "Sample A", Column = 0, Row = 0 };
            var b = new Tile { Label = "Sample B", Column = 2, Row = 6 };
            var left = new TileGroup { Name = "Left", Column = 0, Row = 0, Tiles = [a, b] };
            var right = new TileGroup { Name = "Right", Column = 1, Row = 8, Tiles = [new() { Label = "Sample C" }] };
            var moving = new TileGroup { Name = "Movable", Column = 2, Row = 0, Tiles = [new() { Label = "Sample D" }] };
            Document = new() { Preferences = new() { FreeTilePlacement = true, Animate = false, ShowWallpaper = false, Language = "en" }, Groups = [left, right, moving] };
            ApplyAppearance();
            Root.Width = 1920;
            Root.Height = 1000;
            ResetToPinnedView();
            RenderGroups();
            UpdateLayout();
            Require(GroupsPanel.Columns >= 4, "The placement fixture has too few group columns.");
            Require(ContentScroll.ScrollableHeight <= 1 && ContentScroll.ScrollableWidth <= 1, "A fitting board creates unnecessary scrollbars.");
            var boardHeight = GroupsPanel.ActualHeight;
            ExtendDragSpace();
            UpdateLayout();
            Require(GroupsPanel.ActualHeight > boardHeight && ContentScroll.ExtentHeight > ContentScroll.ViewportHeight,
                "Dragging cannot extend into empty space below the current board.");
            GroupsPanel.DragMinimumHeight = 0;
            GroupsPanel.InvalidateMeasure();
            UpdateLayout();
            ShowAllApps(this, new());
            SetSmokeSearch("Edge");
            var cancelledView = PrepareDragView("ReStart.App");
            UpdateLayout();
            Require(GroupsPanel.IsVisible && !ResultsPanel.IsVisible, "An app drag did not reveal the pinned drop surface.");
            RestoreDragView(cancelledView);
            UpdateLayout();
            Require(ResultsPanel.IsVisible && SearchBox.Text == "Edge", "Cancelling an app drag did not restore its search view.");
            ResetToPinnedView();
            UpdateLayout();
            var pitch = Document.Preferences.Columns * Cell + GroupLayout.Gap;
            _dragOffset = new(20, 12);
            var single = moving.Tiles.Single();
            single.Size = TileSize.Small; single.Column = 4; single.Row = 0;
            RenderGroups(); UpdateLayout();
            Require(Math.Abs(RootPanelFor(moving).ActualHeight - Cell) <= 1, "A single small tile still reserves a medium tile's minimum height.");
            var beforePreview = JsonSerializer.Serialize(Document, LayoutStore.JsonOptions);
            var localBefore = RootPanelFor(moving).Children.OfType<Button>().Single().TranslatePoint(new Point(), RootPanelFor(moving));
            PreviewGroupOnBoard(moving, new(pitch + _dragOffset.X, 8 * Cell / 2 + _dragOffset.Y));
            UpdateLayout();
            Require(GroupsPanel.Placements.Single(p => p.Group == right).Row > 8
                && JsonSerializer.Serialize(Document, LayoutStore.JsonOptions) == beforePreview, "Group preview did not show the displaced group or mutated the document.");
            var localPreview = RootPanelFor(moving).Children.OfType<Button>().Single().TranslatePoint(new Point(), RootPanelFor(moving));
            Require(localPreview == localBefore && single.Column == 4 && single.Row == 0, "Moving a one-tile group changed the child's local cell.");
            SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, Path.GetFileNameWithoutExtension(snapshotPath) + "-group-preview.png"));
            ClearDropPreview(); UpdateLayout();
            Require(GroupsPanel.DragPlacements == null && GroupsPanel.Placements.Single(p => p.Group == right).Row == 8,
                "Cancelling the group preview did not restore the original visual placement.");
            MoveGroupOnBoard(moving, new(pitch + _dragOffset.X, 22 * Cell / 2 + _dragOffset.Y));
            UpdateLayout();
            Require(moving.Column == 1 && moving.Row == 22 && right.Column == 1 && right.Row == 8, "Group movement flattened independent positions.");
            Require(Document.Groups.SequenceEqual(new[] { left, right, moving }), "Group movement changed only the list order.");

            MoveTileIntoGroup(b, left, 4, 10);
            UpdateLayout();
            Require(b.Column == 4 && b.Row == 10 && a.Column == 0 && a.Row == 0, "Moving a tile collapsed an unrelated gap.");
            _dragOffset = new(Cell, Cell);
            var blank = new Point(3 * pitch + Cell, GroupLayout.HeaderHeight + 4 * Cell / 2 + Cell);
            Require(GroupAt(blank) == null, "The new-group target is not empty.");
            MoveTileToStandalone(a, blank);
            UpdateLayout();
            var created = Document.Groups.Single(g => g.Tiles.Contains(a));
            Require(created != left && created.IsStandalone && created.Column == 3 && created.Row == 4, "Dropping on empty board space did not detach an independent tile.");
            var standalone = GroupsPanel.Children.OfType<StackPanel>().Single(s => s.Tag == created);
            Require(!standalone.Children.OfType<GroupHeader>().Any() && standalone.ContextMenu == null
                && Math.Abs(standalone.ActualWidth - 2 * Cell) <= 1 && Math.Abs(standalone.ActualHeight - 2 * Cell) <= 1,
                "An independent tile still has a group header, menu, or invisible group footprint.");
            Require(left.Tiles.SequenceEqual(new[] { b }), "The source tile was duplicated or lost.");
            VerifyGroupBounds();
            var unnamed = new TileGroup { Name = "", Column = 2, Row = 30 };
            Document.Groups.Add(unnamed);
            RenderGroups(); UpdateLayout();
            var unnamedHeader = HeaderFor(unnamed);
            unnamedHeader.Handle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(unnamedHeader.IsEditing, "The unnamed-group placeholder did not enter inline editing.");
            RaiseEditorKey(unnamedHeader.Children.OfType<TextBox>().Single(), Key.Escape);
            Require(unnamed.Name == "", "Cancelling an unnamed group replaced its empty label.");

            var header = HeaderFor(left);
            header.Handle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent });
            Require(header.IsEditing && !OwnedWindows.Cast<Window>().Any(w => w.IsVisible), "Naming opened a modal dialog or did not start inline.");
            var editor = header.Children.OfType<TextBox>().Single();
            editor.Text = "Renamed in place";
            RaiseEditorKey(editor, Key.Enter);
            Require(left.Name == "Renamed in place" && _editingGroupHeader == null, "Enter did not commit the inline name.");
            header = HeaderFor(left);
            header.BeginEdit();
            editor = header.Children.OfType<TextBox>().Single();
            editor.Text = "Cancelled name";
            RaiseEditorKey(editor, Key.Escape);
            Require(left.Name == "Renamed in place" && !header.IsEditing && IsVisible, "Escape did not cancel naming while keeping Start open.");
            header.BeginEdit();
            editor = header.Children.OfType<TextBox>().Single();
            editor.Text = "Renamed on focus loss";
            var pendingClick = RootPanelFor(left).Children.OfType<Button>().Single(button => button.Tag == b);
            editor.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, Environment.TickCount, editor, pendingClick)
            { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Require(left.Name == "Renamed on focus loss" && System.Windows.Media.VisualTreeHelper.GetParent(pendingClick) != null,
                "Committing a name removed the tile before its pending click.");
            header.EditButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(header.IsEditing && header.EditButton.Visibility == Visibility.Collapsed, "The group edit icon did not enter inline editing.");
            RaiseEditorKey(editor, Key.Escape);

            var saved = LayoutStore.Read(_store.FilePath);
            Require(saved.Version == LayoutDocument.CurrentVersion && saved.Groups.Single(g => g.Id == created.Id).Column == 3
                && saved.Groups.Single(g => g.Id == created.Id).Tiles.Single().Id == a.Id, "Saved layout lost its schema, anchor, or tile identity.");
            Root.Width = 800;
            UpdateLayout();
            VerifyGroupBounds();
            Require(created.Column == 3 && created.Row == 4, "A narrow view rewrote saved anchors.");
            Require(ContentScroll.ScrollableWidth > 0, "A wide board cannot scroll horizontally on a narrow screen.");
            ContentScroll.ScrollToRightEnd(); UpdateLayout();
            Require(ContentScroll.HorizontalOffset > 0, "Horizontal scrolling does not reach off-screen content.");
            Root.Width = 1920;
            UpdateLayout();
            VerifyGroupBounds();
            SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, Path.GetFileNameWithoutExtension(snapshotPath) + "-board.png"));
            var standaloneFolder = new Tile { Label = "Independent folder", Column = 2, Children = [new() { Label = "Child one" }, new() { Label = "Child two" }] };
            var folderEntry = LayoutEditor.Detach(Document, standaloneFolder, 3, 16, 2, GroupsPanel.Columns, Cell);
            RenderGroups(); UpdateLayout();
            var closedX = RootPanelFor(folderEntry).Children.OfType<Button>().Single().TranslatePoint(new Point(), GroupsPanel).X;
            OpenFolder(standaloneFolder); UpdateLayout();
            var expandedRoot = RootPanelFor(folderEntry);
            Require(expandedRoot.Expansion?.Folder == standaloneFolder && Math.Abs(expandedRoot.Children.OfType<Button>().Single().TranslatePoint(new Point(), GroupsPanel).X - closedX) <= 1,
                "Expanding an independent folder moved its root tile horizontally.");
            Require(!GroupsPanel.Children.OfType<StackPanel>().Single(s => s.Tag == folderEntry).Children.OfType<GroupHeader>().Any(),
                "An independent folder acquired a group header.");
            VerifyGroupBounds();
            CloseFolder(this, new());
            LayoutEditor.Remove(Document, standaloneFolder.Id);
            RenderGroups(); UpdateLayout();
            var compactGroup = new TileGroup { Name = "Compact", Column = 2, Row = 0, Tiles = [new() { Size = TileSize.Small, Column = 0, Row = 0 }] };
            Document.Groups.Add(compactGroup); RenderGroups(); UpdateLayout();
            Require(Math.Abs(RootPanelFor(compactGroup).ActualWidth - Cell) <= 1, "A one-cell group still reserves the configured group width.");
            _dragOffset = new(10, 10);
            MoveGroupOnBoard(compactGroup, new(1.5 * Cell + 10, 30 * Cell / 2 + 10)); UpdateLayout();
            Require(compactGroup.X == 1.5 && Math.Abs(GroupsPanel.Placements.Single(p => p.Group == compactGroup).Left - 1.5 * Cell) <= 1,
                "A group cannot move between the old fixed-width column anchors.");
            Document.Groups.Remove(compactGroup); RenderGroups(); UpdateLayout();
            var targetPanel = RootPanelFor(left);
            var targetButton = targetPanel.Children.OfType<Button>().Single(button => button.Tag == b);
            _dragOffset = new(targetButton.ActualWidth / 2, targetButton.ActualHeight / 2);
            var center = targetButton.TranslatePoint(new Point(targetButton.ActualWidth / 2, targetButton.ActualHeight / 2), targetPanel);
            _folderHover.Reset();
            Require(FolderHoverTarget(a, targetPanel, center, a.Id, 0) == null
                && FolderHoverTarget(a, targetPanel, center, a.Id, TileFolderHover.DelayMilliseconds - 1) == null
                && FolderHoverTarget(a, targetPanel, center, a.Id, TileFolderHover.DelayMilliseconds) == b, "Actual tile hit testing does not arm a folder after a continuous folder dwell.");
            var edge = targetButton.TranslatePoint(new Point(1, 1), targetPanel);
            Require(FolderHoverTarget(a, targetPanel, edge, a.Id, 1001) == null, "The tile edge incorrectly creates a folder instead of moving a tile.");
            _folderHover.Reset();
            _dragOffset = new(3, 3);
            var cornerGrab = targetButton.TranslatePoint(new Point(3, 3), targetPanel);
            Require(FolderHoverTarget(a, targetPanel, cornerGrab, a.Id, 2000) == null
                && FolderHoverTarget(a, targetPanel, cornerGrab, a.Id, 3000) == b,
                "Grabbing a tile near its corner prevents an otherwise complete tile-over-tile folder drop.");
            var sourceSize = a.Size;
            foreach (var size in Enum.GetValues<TileSize>())
            {
                a.Size = size;
                var (w, h) = TileLayout.Dimensions(size);
                foreach (var grab in new[] { new Point(3, 3), new Point(w * Cell / 2, h * Cell / 2), new Point(w * Cell - 7, h * Cell - 7) })
                {
                    _dragOffset = grab;
                    var aligned = targetButton.TranslatePoint(grab, targetPanel);
                    _folderHover.Reset();
                    Require(FolderHoverTarget(a, targetPanel, aligned, a.Id, 0) == null
                        && FolderHoverTarget(a, targetPanel, aligned, a.Id, TileFolderHover.DelayMilliseconds) == b,
                        "Folder overlap depends on the source tile size or pointer grab corner.");
                }
            }
            a.Size = sourceSize;
            MoveTileIntoGroup(a, left, 4, 10, b);
            var folder = left.Tiles.Single(t => t.IsFolder);
            MoveTileIntoGroup(folder, left, 0, 0);
            var below = new Tile { Label = "Below folder", Column = 0, Row = 2 };
            left.Tiles.Add(below);
            OpenFolder(folder);
            UpdateLayout();
            var root = RootPanelFor(left);
            var opened = root.Children.OfType<Button>().Single(button => button.Tag == folder);
            opened.ApplyTemplate();
            Require(opened.Template.FindName("Chrome", opened) is Border chrome && chrome.BorderBrush == opened.BorderBrush
                && opened.BorderThickness.Left > 0, "The open-folder outline is ignored by the button template.");
            var inline = root.Children.OfType<InlineFolderView>().Single();
            Require(root.Expansion?.Folder == folder && inline.Tiles.Children.Count == 2, "Folder contents did not expand inside the tile group.");
            var targetPoint = inline.Tiles.TranslatePoint(new Point(Cell, Cell), GroupsPanel);
            Require(GroupAt(targetPoint)?.Panel == inline.Tiles, "The inline folder is not an actual drop target.");
            var belowElement = root.Children.OfType<Button>().Single(button => button.Tag == below);
            var expandedY = belowElement.TranslatePoint(new Point(), root).Y;
            Require(Math.Abs(root.ToModelPoint(new Point(0, expandedY)).Y - 2 * Cell) <= 1, "Inline expansion changed persisted root coordinates.");
            var folderName = inline.Child is StackPanel body ? body.Children.OfType<GroupHeader>().Single() : throw new InvalidOperationException("Missing folder header.");
            folderName.EditButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(folderName.IsEditing, "The folder edit icon did not enter inline editing.");
            editor = folderName.Children.OfType<TextBox>().Single();
            editor.Text = "Inline folder";
            RaiseEditorKey(editor, Key.Enter);
            Require(folder.Label == "Inline folder", "The folder name did not commit in place.");
            var incoming = right.Tiles[0];
            MoveTileIntoOpenFolder(incoming, folder, 4, 0);
            MoveTileIntoGroup(a, left, 4, 4);
            UpdateLayout();
            Require(folder.Children.Contains(incoming) && !folder.Children.Contains(a) && left.Tiles.Contains(a), "Moving into or out of the open folder lost ownership.");
            VerifyGroupBounds();
            SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, Path.GetFileNameWithoutExtension(snapshotPath) + "-folder.png"));
            CloseFolder(this, new());
            UpdateLayout();
            root = RootPanelFor(left);
            belowElement = root.Children.OfType<Button>().Single(button => button.Tag == below);
            Require(root.Expansion == null && Math.Abs(belowElement.TranslatePoint(new Point(), root).Y - 2 * Cell) <= 1, "Closing the inline folder did not restore root tile positions.");
            VerifyDetachedBaseline(snapshotPath);
            VerifyAdjacentDrop(snapshotPath);
            VerifyRectangularGroupsAndFolderExpansion(snapshotPath);
            var groupReorderChecks = VerifyGroupDragReordering(snapshotPath);
            return new(23 + groupReorderChecks, 6, 29, 6, saved.Version, Document.Groups.Count);
        }
        finally
        {
            _editingGroupHeader?.EndEdit(false);
            Document = original;
            Root.Width = originalWidth;
            Root.Height = originalHeight;
            ApplyAppearance();
            SaveLayout();
            RenderGroups();
        }
    }

    private GroupHeader HeaderFor(TileGroup group) => GroupsPanel.Children.OfType<StackPanel>().Single(s => s.Tag == group).Children.OfType<GroupHeader>().Single();

    private void VerifyRectangularGroupsAndFolderExpansion(string snapshotPath)
    {
        Document = new() { Preferences = new() { FreeTilePlacement = true, Animate = false, ShowWallpaper = false, Language = "en" } };
        var tall = new TileGroup
        {
            Name = "Tall sample",
            Column = 0,
            Row = 0,
            Tiles = [
            new() { Column = 0, Row = 0, Size = TileSize.Large }, new() { Column = 4, Row = 0 },
            new() { Column = 0, Row = 4, Size = TileSize.Large }]
        };
        var top = new TileGroup { Name = "Top sample", X = 6, Column = 0, Row = 0, Tiles = [new(), new(), new()] };
        var side = new TileGroup { Name = "Side sample", X = 4.5, Column = 0, Row = 10, Tiles = [new(), new()] };
        Document.Groups = [tall, top, side]; ApplyAppearance(); RenderGroups(); UpdateLayout();
        Require(GroupsPanel.Placements.Single(p => p.Group == side).Row > 10, "A group was allowed to overlap a neighbor’s rectangular footprint.");
        Require(GroupLayout.OccupiedAreas(tall, Document.Preferences.Columns, Cell).Count == 1, "A group boundary is not rectangular.");
        var sideButton = RootPanelFor(side).Children.OfType<Button>().First();
        var point = sideButton.TranslatePoint(new Point(Cell, Cell), GroupsPanel);
        Require(Ancestor<Button>(GroupsPanel.InputHitTest(point) as DependencyObject)?.Tag == side.Tiles[0], "A displaced group cannot receive tile input.");
        // Reverse child order to verify the clipping works in either z order.
        Document.Groups.Reverse(); RenderGroups(); UpdateLayout();
        Require(Ancestor<Button>(GroupsPanel.InputHitTest(point) as DependencyObject)?.Tag == side.Tiles[0], "Group z order changes the visible tile hit target.");
        Require(_groupBoundaries != null && !_groupBoundaries.IsHitTestVisible, "Group boundaries are missing or intercept tile input.");
        _groupBoundaries!.HoveredGroup = side; _groupBoundaries.InvalidateVisual();
        SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "placement-rectangular-groups.png"));
        ShowGroupBoundaries(true);
        SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "placement-group-boundaries.png"));
        ShowGroupBoundaries(false); _groupBoundaries.HoveredGroup = null; _groupBoundaries.InvalidateVisual();
        foreach (var standalone in new[] { false, true })
            foreach (var scale in new[] { .7, 1, 1.5 })
            {
                var folder = new Tile { Label = "Folder sample", Column = standalone ? 2 : 0, Row = 0, Children = [new(), new(), new(), new()] };
                var owner = new TileGroup { Name = "Opening sample", Column = 0, Row = 0, IsStandalone = standalone, Tiles = [folder] };
                var lower = new TileGroup { Name = "Lower sample", Column = 0, Row = 8, Tiles = [new()] };
                var adjacent = Enumerable.Range(0, 4).Select(i => new TileGroup
                {
                    IsStandalone = true,
                    X = 7,
                    Column = 1,
                    Row = i * 4,
                    Tiles = [new() { Label = "Unrelated live tile", Size = TileSize.Wide }]
                }).ToArray();
                Document = new() { Preferences = new() { FreeTilePlacement = true, Animate = false, ShowWallpaper = false, TileScale = scale, Language = "en" }, Groups = [lower, owner, .. adjacent] };
                ApplyAppearance(); RenderGroups(); UpdateLayout();
                var before = GroupsPanel.Placements.ToArray();
                var root = RootPanelFor(owner).Children.OfType<Button>().Single().TranslatePoint(new Point(), GroupsPanel);
                var saved = JsonSerializer.Serialize(Document, LayoutStore.JsonOptions);
                OpenFolder(folder); UpdateLayout();
                var after = RootPanelFor(owner).Children.OfType<Button>().Single().TranslatePoint(new Point(), GroupsPanel);
                Require((after - root).Length < 1, "Opening a folder displaced its root when a lower group was first in document order.");
                var expanded = GroupsPanel.Placements.Single(p => p.Group == owner);
                Require(before.Where(p => adjacent.Contains(p.Group)).SequenceEqual(GroupsPanel.Placements.Where(p => adjacent.Contains(p.Group))),
                    "Opening a folder enlarged unrelated live-tile row spacing.");
                Require(GroupsPanel.Placements.Single(p => p.Group == lower).Top >= expanded.Top + expanded.Height + GroupLayout.Gap,
                    "An expanded folder overlaps the group below it.");
                CloseFolder(this, new()); UpdateLayout();
                Require(before.SequenceEqual(GroupsPanel.Placements) && saved == JsonSerializer.Serialize(Document, LayoutStore.JsonOptions),
                    "Closing a folder did not restore the original group and tile anchors.");
            }
    }

    private void VerifyDetachedBaseline(string snapshotPath)
    {
        foreach (var columns in new[] { 6, 8 })
            foreach (var scale in new[] { 0.7, 1.0, 1.5 })
            {
                var tile = new Tile { Label = "Detach sample", Column = 0, Row = 6 };
                var group = new TileGroup { Name = "Baseline sample", Column = 0, Row = 0, Tiles = [tile, new() { Label = "Neighbor", Column = 2, Row = 6 }] };
                Document = new() { Preferences = new() { FreeTilePlacement = true, Columns = columns, TileScale = scale, Animate = false, ShowWallpaper = false, Language = "en" }, Groups = [group] };
                ApplyAppearance(); RenderGroups(); UpdateLayout();
                var button = RootPanelFor(group).Children.OfType<Button>().Single(b => b.Tag == tile);
                var top = button.TranslatePoint(new Point(), GroupsPanel).Y;
                _dragOffset = new(12, 20);
                var point = new Point(columns * Cell + GroupLayout.Gap + _dragOffset.X, top + _dragOffset.Y);
                ShowDropPreview(RootPanelFor(group), tile, 0, 6);
                Require(_dropAdorner!.GroupBounds.HasValue && _dropAdorner.Caption.Contains(group.Name, StringComparison.Ordinal), "A group insertion preview does not identify its destination.");
                PreviewStandalone(tile, point);
                Require(Math.Abs(_dropAdorner!.CellBounds.Top - top) <= 1 && !_dropAdorner.GroupBounds.HasValue,
                    $"The independent preview jumps vertically or retains the group destination highlight: scale={scale}, top={top}, preview={_dropAdorner.CellBounds.Top}.");
                ClearDropPreview();
                MoveTileToStandalone(tile, point); UpdateLayout();
                var independent = Document.Groups.Single(g => g.IsStandalone);
                button = RootPanelFor(independent).Children.OfType<Button>().Single();
                Require(Math.Abs(button.TranslatePoint(new Point(), GroupsPanel).Y - top) <= 1,
                    "Detaching a tile changes its visual baseline.");
                if (scale == 1) SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"detached-baseline-{columns}.png"));
            }
    }
    private void VerifyAdjacentDrop(string snapshotPath)
    {
        var originalTile = new Tile { Label = "Existing tile" };
        var incoming = new Tile { Label = "Incoming tile" };
        var group = new TileGroup { Name = "Drop into this group", Column = 0, Row = 0, Tiles = [originalTile] };
        Document = new() { Preferences = new() { FreeTilePlacement = true, Animate = false, ShowWallpaper = false, Language = "en" }, Groups = [group] };
        ApplyAppearance(); RenderGroups(); UpdateLayout();
        var point = new Point(3 * Cell, GroupLayout.HeaderHeight + Cell);
        _dragOffset = new(Cell, Cell);
        var target = GroupAt(point);
        Require(target?.Group == group, "The empty medium-tile cell beside a one-tile group does not accept insertion.");
        var model = target!.Value.Panel.ToModelPoint(GroupsPanel.TranslatePoint(point, target.Value.Panel));
        var (column, row) = TileLayout.Snap(model.X - _dragOffset.X, model.Y - _dragOffset.Y, Cell, incoming.Size, Document.Preferences.Columns);
        ShowDropPreview(target.Value.Panel, incoming, column, row);
        Require(_dropAdorner!.GroupBounds?.Width == 4 * Cell && column == 2 && row == 0, "The adjacent preview does not show the expanded destination group.");
        SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "adjacent-group-drop.png"));
        ClearDropPreview();
        MoveTileIntoGroup(incoming, group, column, row); UpdateLayout();
        Require(group.Tiles.Count == 2 && originalTile.Column == 0 && originalTile.Row == 0 && incoming.Column == 2 && incoming.Row == 0,
            "Inserting beside a one-tile group displaced its original tile.");
    }

    private TilePanel RootPanelFor(TileGroup group) => GroupsPanel.Children.OfType<StackPanel>().Single(s => s.Tag == group).Children.OfType<TilePanel>().Single();

    private static void RaiseEditorKey(TextBox editor, Key key) => editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
        PresentationSource.FromVisual(editor) ?? throw new InvalidOperationException("The editor has no presentation source."), Environment.TickCount, key)
    { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    private void VerifyGroupBounds()
    {
        var bounds = GroupsPanel.Children.OfType<StackPanel>().Where(s => s.Tag is TileGroup)
            .Select(s => s.TransformToAncestor(GroupsPanel).TransformBounds(new Rect(s.RenderSize))).ToArray();
        Require(bounds.All(b => b.Left >= -1 && b.Right <= GroupsPanel.ActualWidth + 1), "A group escaped the viewport width.");
        for (var i = 0; i < bounds.Length; i++)
            for (var j = i + 1; j < bounds.Length; j++)
                Require(!(bounds[i].Left < bounds[j].Right - 0.5 && bounds[i].Right > bounds[j].Left + 0.5
                    && bounds[i].Top < bounds[j].Bottom - 0.5 && bounds[i].Bottom > bounds[j].Top + 0.5), "Two displayed groups overlap.");
    }
}

internal sealed record PlacementSmokeResult(int PlacementChecks, int InlineNameChecks, int FolderChecks, int BaselineChecks, int SchemaVersion, int Groups);
