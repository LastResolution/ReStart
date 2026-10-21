using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Verifies native selection controls, atomic move previews, persistence and undo/redo with an isolated fictional layout.</summary>
    internal async Task<EditingSmokeResult> VerifyEditingAsync(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("Editing verification requires preview mode.");
        var checks = 0;
        void Require(bool condition, string message) { MainWindow.Require(condition, message); checks++; }
        var directory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "fixture-" + Guid.NewGuid().ToString("N"));
        var store = new LayoutStore(Path.Combine(directory, "layout.json"));
        var fixture = new MainWindow(store, true, () => Task.FromResult(PreviewApps()))
        { Owner = this, Width = 1280, Height = 800, Topmost = false, ShowInTaskbar = false };
        try
        {
            await fixture.InitializeAsync();
            var a = new Tile { AppId = "edge", Label = "Microsoft Edge", Target = "invalid-fixture-target", Column = 0, Row = 0 };
            var b = new Tile { Widget = "clock", Label = "Clock", Column = 2, Row = 0 };
            var c = new Tile { AppId = "notepad", Label = "Notes" }; var d = new Tile { AppId = "paint", Label = "Paint" };
            var folder = new Tile { Label = "Creative", Column = 0, Row = 2, Children = [c, d] };
            var source = new TileGroup { Name = "Everyday", Column = 0, Row = 0, Tiles = [a, b, folder] };
            var destination = new TileGroup { Name = "Workspace", Column = 1, Row = 0, Tiles = [new() { AppId = "calc", Label = "Calculator", Column = 0, Row = 0 }] };
            fixture.Document = new() { Preferences = new() { ShowWallpaper = false, Animate = false, Language = "en" }, Groups = [source, destination] };
            fixture.Root.Width = 1280; fixture.Root.Height = 800;
            fixture.ApplyAppearance(); fixture.RenderGroups(); fixture._layoutHistory.Reset(fixture.Document); fixture.SaveLayout();
            fixture.Present(); fixture.SetStatus("");
            fixture.EditModeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); fixture.UpdateLayout();
            Require(fixture._layoutEditing && fixture.EditBar.IsVisible && fixture.DoneEditingButton.IsKeyboardFocused, "Edit mode is not visible or has not taken keyboard focus.");
            fixture._selectionViews[a.Id].Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            fixture._selectionViews[b.Id].Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            fixture._selectionViews[folder.Id].Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(fixture._selectedTileIds.Count == 3 && fixture._openFolder == null && fixture.IsVisible && fixture.StatusText.Text.Length == 0,
                "Editing clicks launched an app, opened a folder or failed to select tiles.");
            fixture._selectionViews[folder.Id].Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            fixture.ComposeFixtureText("E");
            Require(fixture.SearchBox.Text.Length == 0, "Typing during editing unexpectedly started an app search.");
            Require(fixture._selectionViews[a.Id].Mark.IsVisible && fixture._selectionViews[b.Id].Mark.IsVisible && !fixture.UndoButton.IsEnabled,
                "The selection is not marked or an unchanged layout has undo history.");
            fixture.SaveScreenshot(Path.Combine(directory, "editing-dark.png"));

            fixture._selectionDragPositions = fixture.CaptureTileSelection(); fixture._dragOffset = new(12, 12);
            var targetPanel = fixture.RootPanelFor(destination);
            var drop = targetPanel.TranslatePoint(new(12, 2 * fixture.Cell + 12), fixture.GroupsPanel);
            var before = JsonSerializer.Serialize(fixture.Document.Groups, LayoutStore.JsonOptions);
            var plan = fixture.PlanSelectionDrop(a.Id, drop);
            Require(plan != null, "A fitting multi-tile group drop was rejected.");
            Require(ReferenceEquals(plan, fixture.PlanSelectionDrop(a.Id, new(drop.X + .1, drop.Y + .1))),
                "A stationary snapped drag recalculates the same selection plan.");
            fixture.ShowSelectionPreview(plan!, drop, a.Id); fixture.UpdateLayout();
            Require(fixture._dropAdorner?.SelectionBounds?.Count == 2 && before == JsonSerializer.Serialize(fixture.Document.Groups, LayoutStore.JsonOptions),
                "The multi-tile preview is incomplete or mutated the layout.");
            fixture.SaveScreenshot(Path.Combine(directory, "editing-drop-preview.png"));
            fixture.ClearDropPreview(); fixture._selectionDragPositions = null;
            Require(!fixture._layoutHistory.CanUndo && before == JsonSerializer.Serialize(store.Load()!.Groups, LayoutStore.JsonOptions),
                "Cancelling a preview modified persistence or history.");
            fixture.CommitSelectionPlan(plan!); fixture.UpdateLayout();
            var moved = fixture.Document.Groups.Single(g => g.Id == destination.Id).Tiles;
            Require(moved.Any(t => t.Id == a.Id && t.Column == 0 && t.Row == 2) && moved.Any(t => t.Id == b.Id && t.Column == 2 && t.Row == 2),
                "A batch drop changed the selected tiles' relative positions.");
            Require(fixture.UndoButton.IsEnabled && fixture._selectedTileIds.Count == 2 && !fixture.RedoButton.IsEnabled,
                "A committed drop did not become one undoable operation.");
            fixture.Document.Preferences.Theme = ReStart.Core.ThemeMode.Light; fixture.Document.Preferences.StartWithWindows = true;
            fixture.UndoButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(before == JsonSerializer.Serialize(fixture.Document.Groups, LayoutStore.JsonOptions)
                && fixture.Document.Preferences.Theme == ReStart.Core.ThemeMode.Light && fixture.Document.Preferences.StartWithWindows
                && !fixture.UndoButton.IsEnabled && fixture.RedoButton.IsEnabled, "Undo was incomplete, took multiple steps or rolled back preferences.");
            fixture.RedoButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            fixture.UnpinSelectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(fixture.FindTile(a.Id) == null && fixture.FindTile(b.Id) == null, "Batch unpin left a selected tile behind.");
            fixture.UndoButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(fixture.FindTile(a.Id) != null && fixture.FindTile(b.Id) != null, "Batch unpin did not undo in one step.");

            // Denying replacement of the layout file exercises the real atomic-save
            // failure path rather than a mocked status or history implementation.
            var successful = JsonSerializer.Serialize(fixture.Document.Groups, LayoutStore.JsonOptions);
            using (var locked = new FileStream(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fixture.FindTile(a.Id)!.Label = "Unsaved rename"; fixture.CommitLayout();
                Require(successful == JsonSerializer.Serialize(fixture.Document.Groups, LayoutStore.JsonOptions), "A failed write left an unsaved layout visible.");
                fixture.RestoreLayoutHistory(false);
                Require(fixture._layoutHistory.CanUndo && successful == JsonSerializer.Serialize(fixture.Document.Groups, LayoutStore.JsonOptions),
                    "A failed undo consumed history or changed the in-memory layout.");
            }
            fixture.SetStatus("");
            if (!fixture._selectedTileIds.Contains(a.Id)) fixture.ToggleTileSelection(a.Id);
            if (!fixture._selectedTileIds.Contains(b.Id)) fixture.ToggleTileSelection(b.Id);
            fixture.UpdateLayout();
            fixture._selectionDragPositions = fixture.CaptureTileSelection();
            var closedFolderPoint = fixture._selectionViews[folder.Id].Button.TranslatePoint(new(12, 12), fixture.GroupsPanel);
            fixture._folderHover.Reset(); fixture._folderHover.Update(a.Id, folder.Id, Environment.TickCount64 - TileFolderHover.DelayMilliseconds);
            plan = fixture.PlanSelectionDrop(a.Id, closedFolderPoint);
            Require(plan != null && LayoutEditor.Find(new() { Groups = plan }, folder.Id)?.Children.Count == 4,
                $"A multi-selection cannot enter an existing closed folder after its dwell: selection={fixture._selectedTileIds.Count}, target={fixture.TileDropTarget(fixture.FindTile(a.Id)!, closedFolderPoint)?.Group.Name}, plan={plan != null}, children={(plan == null ? -1 : LayoutEditor.Find(new() { Groups = plan }, folder.Id)?.Children.Count)}.");
            fixture.CommitSelectionPlan(plan!); fixture.UndoButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            fixture._selectionDragPositions = null; fixture._folderHover.Reset(); fixture.StopFolderDwell();
            fixture.ClearTileSelectionClick(this, new());
            var folderButton = fixture._selectionViews[folder.Id].Button;
            folderButton.ContextMenu!.Items.OfType<MenuItem>().Single(i => Equals(i.Header, L.T(K.OpenTileFolder))).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            fixture.UpdateLayout();
            Require(fixture._openFolder?.Id == folder.Id, "The explicit folder action does not work in edit mode.");
            fixture._selectionViews[c.Id].Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            fixture._selectionViews[folder.Id].Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(fixture._selectedTileIds.SetEquals([folder.Id]), "A folder and its child can be selected simultaneously.");
            fixture._selectionViews[c.Id].Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            fixture._selectionViews[d.Id].Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(fixture._selectedTileIds.SetEquals([c.Id, d.Id]), "Selecting a child did not exclude its selected parent.");
            fixture.CloseFolder(this, new());
            Require(fixture._selectedTileIds.Count == 0, "Closing a folder retains invisible child selections.");
            fixture.SetLayoutEditing(false); fixture.SelectTileForEditing(c.Id); fixture.UpdateLayout();
            Require(fixture._layoutEditing && fixture._openFolder?.Id == folder.Id && fixture._selectedTileIds.SetEquals([c.Id]),
                "Selecting a folder child from its context action loses or hides the child.");
            fixture.ToggleTileSelection(d.Id);
            fixture._selectionDragPositions = fixture.CaptureTileSelection();
            plan = fixture.PlanSelectionDrop(c.Id, new(18 * fixture.Cell + 12, GroupLayout.HeaderHeight + 6 * fixture.Cell + 12));
            Require(plan != null, "Moving folder children onto open space was rejected.");
            fixture.CommitSelectionPlan(plan!); fixture.UpdateLayout();
            Require(fixture.FindTile(folder.Id) == null && fixture.Document.Groups.Count(g => g.IsStandalone && g.Tiles.Any(t => t.Id == c.Id || t.Id == d.Id)) == 2,
                "Moving the last children out retained an empty folder or created a named group.");
            fixture.UndoButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(fixture.FindTile(folder.Id)?.Children.Count == 2, "Undo did not restore the removed folder and both children.");
            fixture._selectionDragPositions = null;
            fixture.OpenFolder(fixture.FindTile(folder.Id)!); fixture.ClearTileSelectionClick(this, new());
            fixture.ToggleTileSelection(a.Id); fixture.ToggleTileSelection(b.Id); fixture.UpdateLayout();
            fixture._selectionDragPositions = fixture.CaptureTileSelection();
            var inline = VisualChildren<ReStart.App.Controls.TilePanel>(fixture.GroupsPanel).Single(p => p.FolderOwner?.Id == folder.Id);
            var inside = inline.TranslatePoint(new(12, 12), fixture.GroupsPanel);
            plan = fixture.PlanSelectionDrop(a.Id, inside);
            Require(plan != null && LayoutEditor.Find(new() { Groups = plan }, folder.Id)?.Children.Count == 4,
                "An expanded folder was classified as its surrounding group.");
            fixture.CommitSelectionPlan(plan!); fixture.UndoButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            fixture.ClearTileSelectionClick(this, new()); fixture.ToggleTileSelection(folder.Id); fixture.UpdateLayout();
            fixture._selectionDragPositions = fixture.CaptureTileSelection();
            Require(fixture.PlanSelectionDrop(folder.Id, inside) == null, "A selected folder can be dropped into its own expanded children.");
            fixture._selectionDragPositions = null;

            fixture.Document.Preferences.Theme = ReStart.Core.ThemeMode.Light; fixture.ApplyAppearance(); fixture.RenderGroups(); fixture.UpdateLayout();
            fixture.ClearTileSelectionClick(this, new());
            fixture.ToggleTileSelection(a.Id); fixture.ToggleTileSelection(b.Id);
            fixture.SaveScreenshot(Path.Combine(directory, "editing-light.png"));
            fixture.MoveSelectionToGroup(null); fixture.UpdateLayout();
            Require(fixture.Document.Groups.Any(g => !g.IsStandalone && g.Tiles.Any(t => t.Id == a.Id) && g.Tiles.Any(t => t.Id == b.Id)),
                "The compact group-menu move failed.");
            fixture.UndoButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            fixture.ToggleTileSelection(a.Id); fixture.ToggleTileSelection(b.Id);

            var themes = 0;
            foreach (var language in TextCatalog.SupportedLanguages)
                foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
                {
                    fixture.Document.Preferences.Language = language; fixture.Document.Preferences.Theme = theme;
                    fixture.ApplyAppearance(); fixture.RenderGroups(); fixture.UpdateLayout();
                    Require(fixture.SelectionSummary.Text.Contains(L.T(K.LayoutEditingHint)) && fixture.EditRailLabel.Label == L.T(K.FinishLayoutEditing)
                        && AutomationProperties.GetName(fixture.EditModeButton) == L.T(K.FinishLayoutEditing), "Selection chrome is not localized.");
                    Require(fixture.EditModeButton.Content is RailLabel && fixture.EditModeButton.IsDescendantOf(fixture.NavigationRail)
                        && fixture.ExpandedEditModeButton.IsDescendantOf(fixture.ExpandedRail), "Selection mode is not available from both sidebar variants.");
                    Require(fixture.EditBar.ActualHeight > 0 && fixture.EditBar.ActualWidth <= fixture.ContentScroll.ActualWidth + 1, "The editing toolbar overflows its viewport.");
                    themes++;
                }
            fixture.Document.Preferences.Language = "en"; fixture.ApplyAppearance();
            fixture.SetLayoutEditing(false); fixture.SetLayoutEditing(true); fixture.ToggleTileSelection(a.Id);
            fixture.DoneEditingButton.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(fixture)!, Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Require(fixture._layoutEditing && fixture._selectedTileIds.Count == 0, "Escape did not clear selection before leaving editing.");
            fixture.DoneEditingButton.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(fixture)!, Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Require(!fixture._layoutEditing && !fixture.EditBar.IsVisible, "A second Escape did not leave editing.");
            fixture.SetLayoutEditing(true); fixture.Hide(); fixture.Present();
            Require(!fixture._layoutEditing && fixture._selectedTileIds.Count == 0 && fixture._layoutHistory.CanUndo, "Reopening retained editing mode or lost layout history.");
            var beforeImport = JsonSerializer.Serialize(fixture.Document.Groups, LayoutStore.JsonOptions);
            var imported = new LayoutDocument { Preferences = fixture.Document.Preferences, Groups = [new() { Name = "Imported fixture", Tiles = [new() { Label = "Sample" }] }] };
            fixture.ReplaceDocument(imported);
            Require(store.Load()!.Groups[0].Name == "Imported fixture", "A replacement layout was not saved before activation.");
            fixture.RestoreLayoutHistory(false);
            Require(beforeImport == JsonSerializer.Serialize(fixture.Document.Groups, LayoutStore.JsonOptions), "An imported layout cannot be undone in one step.");
            var previousDocument = fixture.Document;
            using (var locked = new FileStream(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var rejected = false;
                try { fixture.ReplaceDocument(imported); } catch (IOException) { rejected = true; }
                Require(rejected && ReferenceEquals(fixture.Document, previousDocument), "A failed import replaced the visible document.");
            }
            return new(checks, themes, directory);
        }
        finally { fixture.Exit(); }
    }
}

internal sealed record EditingSmokeResult(int Checks, int LanguageThemeCases, string Screenshots);
