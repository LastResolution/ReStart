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
            Document = new() { Preferences = new() { Animate = false, ShowWallpaper = false, Language = original.Preferences.Language }, Groups = [left, right, moving] };
            Root.Width = 1920;
            Root.Height = 1000;
            ResetToPinnedView();
            RenderGroups();
            UpdateLayout();
            Require(GroupsPanel.Columns >= 4, "The placement fixture has too few group columns.");
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
            MoveGroupOnBoard(moving, new(pitch + _dragOffset.X, 22 * Cell / 2 + _dragOffset.Y));
            UpdateLayout();
            Require(moving.Column == 1 && moving.Row == 22 && right.Column == 1 && right.Row == 8, "Group movement flattened independent positions.");
            Require(Document.Groups.SequenceEqual(new[] { left, right, moving }), "Group movement changed only the list order.");

            MoveTileIntoGroup(b, left, 4, 10);
            UpdateLayout();
            Require(b.Column == 4 && b.Row == 10 && a.Column == 0 && a.Row == 0, "Moving a tile collapsed an unrelated gap.");
            _dragOffset = new(Cell, Cell);
            var blank = new Point(3 * pitch + Cell, 4 * Cell / 2 + GroupLayout.HeaderHeight + Cell);
            Require(GroupAt(blank) == null, "The new-group target is not empty.");
            MoveTileToNewGroup(a, blank);
            UpdateLayout();
            var created = Document.Groups.Single(g => g.Tiles.Contains(a));
            Require(created != left && created.Column == 3 && created.Row == 4, "Dropping on empty board space did not create a positioned group.");
            Require(left.Tiles.SequenceEqual(new[] { b }), "The source tile was duplicated or lost.");
            VerifyGroupBounds();
            var unnamedHeader = HeaderFor(created);
            unnamedHeader.Handle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(unnamedHeader.IsEditing, "The unnamed-group placeholder did not enter inline editing.");
            RaiseEditorKey(unnamedHeader.Children.OfType<TextBox>().Single(), Key.Escape);
            Require(created.Name == "", "Cancelling an unnamed group replaced its empty label.");

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

            var saved = LayoutStore.Read(_store.FilePath);
            Require(saved.Version == 2 && saved.Groups.Single(g => g.Id == created.Id).Column == 3
                && saved.Groups.Single(g => g.Id == created.Id).Tiles.Single().Id == a.Id, "Saved layout lost its schema, anchor, or tile identity.");
            Root.Width = 800;
            UpdateLayout();
            VerifyGroupBounds();
            Require(created.Column == 3 && created.Row == 4, "A narrow view rewrote saved anchors.");
            Root.Width = 1920;
            UpdateLayout();
            VerifyGroupBounds();
            SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, Path.GetFileNameWithoutExtension(snapshotPath) + "-board.png"));
            MoveTileIntoGroup(a, left, 4, 10, b);
            var folder = left.Tiles.Single(t => t.IsFolder);
            MoveTileIntoGroup(folder, left, 0, 0);
            var below = new Tile { Label = "Below folder", Column = 0, Row = 2 };
            left.Tiles.Add(below);
            OpenFolder(folder);
            UpdateLayout();
            var root = RootPanelFor(left);
            var inline = root.Children.OfType<InlineFolderView>().Single();
            Require(root.Expansion?.Folder == folder && inline.Tiles.Children.Count == 2, "Folder contents did not expand inside the tile group.");
            var targetPoint = inline.Tiles.TranslatePoint(new Point(Cell, Cell), GroupsPanel);
            Require(GroupAt(targetPoint)?.Panel == inline.Tiles, "The inline folder is not an actual drop target.");
            var belowElement = root.Children.OfType<Button>().Single(button => button.Tag == below);
            var expandedY = belowElement.TranslatePoint(new Point(), root).Y;
            Require(Math.Abs(root.ToModelPoint(new Point(0, expandedY)).Y - 2 * Cell) <= 1, "Inline expansion changed persisted root coordinates.");
            var folderName = inline.Child is StackPanel body ? body.Children.OfType<GroupHeader>().Single() : throw new InvalidOperationException("Missing folder header.");
            folderName.Handle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
            return new(8, 4, 5, saved.Version, Document.Groups.Count);
        }
        finally
        {
            _editingGroupHeader?.EndEdit(false);
            Document = original;
            Root.Width = originalWidth;
            Root.Height = originalHeight;
            SaveLayout();
            RenderGroups();
        }
    }

    private GroupHeader HeaderFor(TileGroup group) => GroupsPanel.Children.OfType<StackPanel>().Single(s => s.Tag == group).Children.OfType<GroupHeader>().Single();
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
                Require(!bounds[i].IntersectsWith(bounds[j]), "Two displayed groups overlap.");
    }
}

internal sealed record PlacementSmokeResult(int PlacementChecks, int InlineNameChecks, int FolderChecks, int SchemaVersion, int Groups);
