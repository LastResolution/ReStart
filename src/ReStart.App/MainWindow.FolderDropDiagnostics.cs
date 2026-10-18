using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Exercises the production preview/drop resolver, real dwell clocks and saved folder ownership with fictional data.</summary>
    internal async Task<FolderDropSmokeResult> VerifyFolderDropsAsync(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("Folder-drop verification requires preview mode.");
        var directory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "fixture-" + Guid.NewGuid().ToString("N"));
        var store = new LayoutStore(Path.Combine(directory, "layout.json"));
        var fixture = new MainWindow(store, true, () => Task.FromResult(PreviewApps()))
        { Owner = this, Width = 1280, Height = 800, Topmost = false, ShowInTaskbar = false };
        var timers = 0; var geometry = 0; var routes = 0; var cancellations = 0;
        try
        {
            await fixture.InitializeAsync();
            fixture.Root.Width = 1280; fixture.Root.Height = 800;
            foreach (var standalone in new[] { true, false })
                foreach (var editing in new[] { false, true })
                    foreach (var sizes in new[] { (Source: TileSize.Medium, Target: TileSize.Medium), (Source: TileSize.Wide, Target: TileSize.Small), (Source: TileSize.Large, Target: TileSize.Small) })
                    {
                        var (source, target, panel) = Scene(standalone, editing, sizes.Source, sizes.Target);
                        fixture._dragOffset = new(3, 3);
                        var point = Pointer(panel, target);
                        // Leave the real mouse untouched. An OLE drag event supplies
                        // its own screen position, independently of WPF mouse polling.
                        var stale = Mouse.GetPosition(fixture.GroupsPanel);
                        if ((point - stale).Length < 150)
                        {
                            var owner = fixture.Document.Groups.Single(g => g.Tiles.Contains(target)); owner.X = 16;
                            fixture.RenderGroups(); fixture.UpdateLayout(); panel = fixture.RootPanelFor(owner); point = Pointer(panel, target);
                        }
                        var before = JsonSerializer.Serialize(fixture.Document.Groups, LayoutStore.JsonOptions);
                        fixture._dragging = true;
                        Require(fixture.PreviewSingleTileDrop(source, point, source.Id), "A valid folder overlap was rejected.");
                        Require(fixture._pendingFolderDrop?.Target == target, "The drag did not start the folder dwell timer.");
                        await Task.Delay(TileFolderHover.DelayMilliseconds + 150);
                        Require(fixture._pendingFolderDrop?.Target == target && (standalone ? fixture._dropAdorner?.GroupTarget == true : fixture._dropAdorner?.FolderTarget == true),
                            $"The stationary OLE hover lost its target before drop: standalone={standalone}, editing={editing}, pending={fixture._pendingFolderDrop != null}.");
                        fixture.SaveScreenshot(Path.Combine(directory, $"ready-{standalone}-{editing}-{sizes.Source}-{sizes.Target}.png"));
                        Require(fixture.CommitSingleTileDrop(source, point, source.Id), "An armed folder drop was rejected.");
                        fixture._dragging = false;
                        VerifySavedDrop(source, target, standalone);
                        fixture.RestoreLayoutHistory(false);
                        Require(before == JsonSerializer.Serialize(fixture.Document.Groups, LayoutStore.JsonOptions), "Folder creation did not undo in one step.");
                        fixture.RestoreLayoutHistory(true); VerifySavedDrop(source, target, standalone);
                        timers++;
                    }

            foreach (var standalone in new[] { true, false })
                foreach (var editing in new[] { false, true })
                    foreach (var sourceSize in Enum.GetValues<TileSize>())
                        foreach (var targetSize in Enum.GetValues<TileSize>())
                            for (var grab = 0; grab < 3; grab++)
                            {
                                var (source, target, panel) = Scene(standalone, editing, sourceSize, targetSize);
                                var (w, h) = TileLayout.Dimensions(sourceSize);
                                fixture._dragOffset = grab switch { 0 => new(3, 3), 1 => new((w * fixture.Cell - 4) / 2, (h * fixture.Cell - 4) / 2), _ => new(w * fixture.Cell - 7, h * fixture.Cell - 7) };
                                var point = Pointer(panel, target);
                                Require(fixture.PreviewSingleTileDrop(source, point, source.Id, 0)
                                    && fixture.PreviewSingleTileDrop(source, point, source.Id, TileFolderHover.DelayMilliseconds)
                                    && (standalone ? fixture._dropAdorner?.GroupTarget == true : fixture._dropAdorner?.FolderTarget == true),
                                    $"A folder preview failed: standalone={standalone}, editing={editing}, source={sourceSize}, target={targetSize}, grab={grab}.");
                                Require(fixture.CommitSingleTileDrop(source, point, source.Id), "A geometry-verified folder drop was rejected.");
                                VerifySavedDrop(source, target, standalone); geometry++;
                            }
            var (cancelSource, cancelTarget, cancelPanel) = Scene(true, false, TileSize.Medium, TileSize.Medium);
            fixture._dragOffset = new(3, 3); fixture._dragging = true;
            var cancelBefore = JsonSerializer.Serialize(fixture.Document.Groups, LayoutStore.JsonOptions);
            fixture.PreviewSingleTileDrop(cancelSource, Pointer(cancelPanel, cancelTarget), cancelSource.Id);
            await Task.Delay(TileFolderHover.DelayMilliseconds + 150);
            Require(fixture._dropAdorner?.GroupTarget == true, "Cancellation fixture never armed its group.");
            fixture.StopFolderDwell(); fixture._folderHover.Reset(); fixture.ClearDropPreview(); fixture._dragging = false;
            await Task.Delay(75);
            Require(fixture._pendingFolderDrop == null && fixture._dropAdorner == null && !fixture._layoutHistory.CanUndo
                && cancelBefore == JsonSerializer.Serialize(fixture.Document.Groups, LayoutStore.JsonOptions), "Cancelling an armed folder changed the layout or retained its timer.");
            cancellations++;

            var (scrollSource, scrollTarget, scrollPanel) = Scene(true, false, TileSize.Medium, TileSize.Medium);
            fixture.GroupsPanel.DragMinimumHeight = fixture.ContentScroll.ViewportHeight + 500; fixture.GroupsPanel.InvalidateMeasure(); fixture.UpdateLayout();
            fixture._dragging = true;
            fixture.PreviewSingleTileDrop(scrollSource, Pointer(scrollPanel, scrollTarget), scrollSource.Id);
            fixture.ContentScroll.ScrollToVerticalOffset(240); fixture.UpdateLayout();
            await Task.Delay(100);
            Require(fixture.ContentScroll.VerticalOffset > 200 && fixture._pendingFolderDrop == null && fixture._dropAdorner == null,
                $"Scrolling away from a target retained a folder dwell at obsolete content coordinates: offset={fixture.ContentScroll.VerticalOffset}, pending={fixture._pendingFolderDrop != null}, preview={fixture._dropAdorner != null}.");
            fixture._dragging = false; fixture.GroupsPanel.DragMinimumHeight = 0; fixture.ContentScroll.ScrollToTop(); cancellations++;

            var (moveSource, _, movePanel) = Scene(false, true, TileSize.Medium, TileSize.Medium);
            fixture._dragOffset = new(3, 3);
            var cellPoint = movePanel.TranslatePoint(new(2 * fixture.Cell + 3, 6 * fixture.Cell + 3), fixture.GroupsPanel);
            Require(fixture.PreviewSingleTileDrop(moveSource, cellPoint, moveSource.Id) && fixture.CommitSingleTileDrop(moveSource, cellPoint, moveSource.Id)
                && fixture.Document.Groups.Count == 1 && !fixture.Document.Groups[0].IsStandalone && !moveSource.IsFolder
                && moveSource.Column == 2 && moveSource.Row == 6, "An empty group cell was classified as a folder or independent board space."); routes++;
            (moveSource, _, _) = Scene(true, false, TileSize.Medium, TileSize.Medium);
            var outside = new Point(18 * fixture.Cell + 3, GroupLayout.HeaderHeight + 8 * fixture.Cell + 3);
            Require(fixture.PreviewSingleTileDrop(moveSource, outside, moveSource.Id) && fixture.CommitSingleTileDrop(moveSource, outside, moveSource.Id)
                && fixture.Document.Groups.Count == 2 && fixture.Document.Groups.All(g => g.IsStandalone && !g.Tiles[0].IsFolder),
                "Open board space was incorrectly classified as a group or folder."); routes++;

            var (first, second, originalPanel) = Scene(false, false, TileSize.Medium, TileSize.Medium);
            var folderPoint = Pointer(originalPanel, second);
            fixture.PreviewSingleTileDrop(first, folderPoint, first.Id, 0); fixture.PreviewSingleTileDrop(first, folderPoint, first.Id, 1000);
            fixture.CommitSingleTileDrop(first, folderPoint, first.Id);
            var existing = fixture.Document.Groups.SelectMany(g => g.Tiles).Single();
            var extra = new Tile { Label = "Sample addition" }; AddIndependent(extra);
            var ownerPanel = fixture.RootPanelFor(fixture.Document.Groups.Single(g => g.Tiles.Contains(existing)));
            folderPoint = Pointer(ownerPanel, existing);
            fixture._folderHover.Reset(); fixture.PreviewSingleTileDrop(extra, folderPoint, extra.Id, 0); fixture.PreviewSingleTileDrop(extra, folderPoint, extra.Id, 1000);
            Require(fixture.CommitSingleTileDrop(extra, folderPoint, extra.Id) && existing.Children.Count == 3, "Dropping onto a closed folder created a new group instead of adding a child."); routes++;
            fixture.OpenFolder(existing); var child = new Tile { Label = "Sample inline addition" }; AddIndependent(child);
            var inline = VisualChildren<ReStart.App.Controls.TilePanel>(fixture.GroupsPanel).Single(p => p.FolderOwner == existing);
            var childPoint = inline.TranslatePoint(new(3, 3), fixture.GroupsPanel);
            Require(fixture.PreviewSingleTileDrop(child, childPoint, child.Id) && fixture.CommitSingleTileDrop(child, childPoint, child.Id)
                && existing.Children.Count == 4 && existing.Children.Contains(child), "The expanded folder body was classified as its surrounding group."); routes++;
            var nested = new Tile { Label = "Source folder", Children = [new() { Label = "Nested sample" }] }; AddIndependent(nested);
            var nestedBefore = JsonSerializer.Serialize(fixture.Document.Groups, LayoutStore.JsonOptions);
            Require(!fixture.PreviewSingleTileDrop(nested, childPoint, nested.Id) && !fixture.CommitSingleTileDrop(nested, childPoint, nested.Id)
                && nestedBefore == JsonSerializer.Serialize(fixture.Document.Groups, LayoutStore.JsonOptions), "A nested folder drop was accepted or changed ownership."); routes++;

            var (lead, independentTarget, independentPanel) = Scene(true, true, TileSize.Medium, TileSize.Medium);
            var companion = new Tile { Label = "Sample companion", Widget = "clock" }; AddIndependent(companion);
            fixture._selectedTileIds.Add(companion.Id); fixture.RefreshTileSelection();
            fixture._selectionDragPositions = fixture.CaptureTileSelection();
            Require(fixture._selectionDragPositions.Count == 2, "The batch grouping fixture did not capture both selected tiles.");
            independentPanel = fixture.RootPanelFor(fixture.Document.Groups.Single(g => g.Tiles.Contains(independentTarget)));
            var groupingPoint = Pointer(independentPanel, independentTarget);
            fixture._folderHover.Update(lead.Id, independentTarget.Id, 0);
            var batchPlan = fixture.PlanSelectionDrop(lead.Id, groupingPoint);
            Require(batchPlan != null, "A batch could not collect around an independent target.");
            fixture.CommitSelectionPlan(batchPlan!); fixture._selectionDragPositions = null;
            Require(fixture.Document.Groups.Count == 1, "A batch grouping drop retained independent source containers.");
            var batchGroup = fixture.Document.Groups.Single();
            Require(!batchGroup.IsStandalone && batchGroup.Tiles.Count == 3 && batchGroup.Tiles.All(t => !t.IsFolder), "A batch overlap produced independent entries or a folder instead of an ordinary group."); routes++;

            var (folderSource, plainTarget, plainPanel) = Scene(true, false, TileSize.Medium, TileSize.Medium);
            folderSource.Children = [new() { Label = "Sample child" }]; fixture.RenderGroups(); fixture.UpdateLayout();
            plainPanel = fixture.RootPanelFor(fixture.Document.Groups.Single(g => g.Tiles.Contains(plainTarget)));
            var plainPoint = Pointer(plainPanel, plainTarget);
            fixture.PreviewSingleTileDrop(folderSource, plainPoint, folderSource.Id, 0);
            fixture.PreviewSingleTileDrop(folderSource, plainPoint, folderSource.Id, 1000);
            Require(fixture._dropAdorner?.GroupTarget == true && fixture.CommitSingleTileDrop(folderSource, plainPoint, folderSource.Id)
                && fixture.Document.Groups.Single().Tiles.Single(t => t.Id == folderSource.Id).Children.Count == 1, "A folder could not become a root in a newly collected ordinary group."); routes++;
            return new(timers, geometry, routes, cancellations, directory);
        }
        finally { fixture._dragging = false; fixture.Exit(); }

        (Tile Source, Tile Target, ReStart.App.Controls.TilePanel Panel) Scene(bool standalone, bool editing, TileSize sourceSize, TileSize targetSize)
        {
            fixture._dragging = false; fixture.StopFolderDwell(); fixture._folderHover.Reset(); fixture.ClearDropPreview(); fixture.SetLayoutEditing(false);
            var source = new Tile { Label = "Sample source", Size = sourceSize, Column = 0, Row = 0 };
            var target = new Tile { Label = "Sample target", Size = targetSize, Column = 0, Row = standalone ? 0 : 8 };
            var group = new TileGroup { Name = "Workspace", IsStandalone = standalone, X = standalone ? 8 : 0, Column = standalone ? 1 : 0, Row = standalone ? 16 : 0, Tiles = [target] };
            fixture.Document = new()
            {
                Preferences = new() { Animate = false, ShowWallpaper = false, Language = "en" },
                Groups = standalone
                ? [new() { IsStandalone = true, X = 0, Column = 0, Row = 0, Tiles = [source] }, group] : [group]
            };
            if (!standalone) group.Tiles.Insert(0, source);
            fixture.ApplyAppearance(); fixture.RenderGroups(); fixture.SaveLayout(); fixture._layoutHistory.Reset(fixture.Document);
            if (!fixture.IsVisible) fixture.Present();
            fixture.SetLayoutEditing(editing);
            if (editing) fixture.SelectTileForEditing(source.Id);
            fixture.UpdateLayout();
            return (source, target, fixture.RootPanelFor(group));
        }

        Point Pointer(ReStart.App.Controls.TilePanel panel, Tile target) => panel.Children.OfType<Button>().Single(b => b.Tag == target)
            .TranslatePoint(fixture._dragOffset, fixture.GroupsPanel);

        void VerifySavedDrop(Tile source, Tile target, bool standalone)
        {
            if (standalone)
            {
                var group = fixture.Document.Groups.Single();
                Require(!group.IsStandalone && group.Tiles.Count == 2 && group.Tiles.All(t => !t.IsFolder)
                    && group.Tiles.Select(t => t.Id).ToHashSet().SetEquals([source.Id, target.Id]), "An independent overlap did not create an ordinary group with both original roots.");
                Require(JsonSerializer.Serialize(store.Load()!.Groups, LayoutStore.JsonOptions) == JsonSerializer.Serialize(fixture.Document.Groups, LayoutStore.JsonOptions), "The group preview and saved ownership disagree.");
                return;
            }
            var folder = fixture.Document.Groups.SelectMany(g => g.Tiles).SingleOrDefault(t => t.IsFolder);
            Require(folder != null && folder.Children.Select(t => t.Id).ToHashSet().SetEquals([source.Id, target.Id])
                && fixture.Document.Groups.SelectMany(g => g.Tiles).Count() == 1, "The actual drop did not create exactly one folder with both original tile identities.");
            var saved = store.Load()!.Groups.SelectMany(g => g.Tiles).Single();
            Require(saved.IsFolder && saved.Children.Select(t => t.Id).ToHashSet().SetEquals([source.Id, target.Id]), "The folder preview and persisted ownership disagree.");
        }

        void AddIndependent(Tile tile)
        {
            fixture.Document.Groups.Add(new() { Name = "", IsStandalone = true, X = 16, Column = 2, Row = 0, Tiles = [tile] });
            fixture.SaveLayout(); fixture.RenderGroups(); fixture.UpdateLayout();
        }
    }
}

internal sealed record FolderDropSmokeResult(int StationaryTimerCases, int SizeGrabModeCases, int DestinationCases, int CancellationCases, string Screenshots);
