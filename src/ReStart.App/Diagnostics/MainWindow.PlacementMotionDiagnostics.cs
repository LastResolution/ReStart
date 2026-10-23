using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Checks real placement clocks, rapid retargeting, stable ownership and bounded stationary-preview work.</summary>
    /// <param name="directory">Owned fixture directory for intermediate animation frames and timing summaries.</param>
    /// <returns>Number of checked animation, model and performance invariants; never uses a production layout.</returns>
    private async Task<int> VerifyPlacementMotionAsync(string directory)
    {
        var checks = 0; var timings = new List<double>();
        void Check(bool value, string message) { Require(value, message); checks++; }
        _clock.Stop(); _verifyMotion = true;
        Document.Preferences.Animate = false; Present(); StopMotion();
        foreach (var size in Enum.GetValues<TileSize>())
            foreach (var editing in new[] { false, true })
            {
                var source = new Tile { Label = "Moving app", Size = size, Column = 0, Row = 0 };
                var target = new Tile { Label = "Displaced app", Size = TileSize.Medium, Column = 0, Row = 0 };
                var group = new TileGroup { Name = "Apps", X = 0, Column = 0, Row = 0, Tiles = [target] };
                Document = new()
                {
                    Preferences = new() { Language = "en", Columns = 8, Animate = true, ShowWallpaper = false, CreateFoldersOnDrop = false, GroupTilesOnDrop = false },
                    Groups = [group, new() { IsStandalone = true, X = 20, Column = 0, Row = 0, Tiles = [source] }]
                };
                _layoutEditing = editing; _dragging = true; ClearDropPreview(); RenderGroups(); UpdateLayout();
                var panel = RootPanelFor(group); var button = _selectionViews[target.Id].Button;
                var before = JsonSerializer.Serialize(Document.Groups, LayoutStore.JsonOptions);
                var initial = button.TranslatePoint(new(), GroupsPanel);
                _dragOffset = new(2, 2);
                var origin = LayoutOrigin(panel, GroupsPanel); var pointer = new Point(origin.X + 2, origin.Y + 2);
                var renderCount = _boardRenderCount;
                PreviewSingleTileDrop(source, pointer, source.Id, 0); UpdateLayout();
                Check((button.TranslatePoint(new(), GroupsPanel) - initial).Length < 1, "A displacement jumped directly to its destination.");
                var retargets = _tileTransitions.RetargetCount;
                var watch = Stopwatch.StartNew();
                for (var sample = 0; sample < 500; sample++) PreviewSingleTileDrop(source, pointer, source.Id, 0);
                timings.Add(watch.Elapsed.TotalMilliseconds / 500);
                Check(_tileTransitions.RetargetCount == retargets && _boardRenderCount == renderCount, "A stationary pointer restarted clocks or rebuilt the board.");
                await Task.Delay(70);
                var middle = button.TranslatePoint(new(), GroupsPanel);
                Check((middle - initial).Length > 1, "A displaced tile did not animate away from its original slot.");
                Check(ResolveSingleTileDrop(source, pointer, source.Id, 0)?.Group == group
                    && JsonSerializer.Serialize(Document.Groups, LayoutStore.JsonOptions) == before, "Presentation animation changed the recipient or saved model.");
                // A changed preview returns from the currently visible position.
                PreviewSingleTileDrop(source, new(1100, 650), source.Id, 0); UpdateLayout();
                Check((button.TranslatePoint(new(), GroupsPanel) - middle).Length < 1, "Retargeting a moving neighbor introduced a visual jump.");
                await Task.Delay(250);
                Check((button.TranslatePoint(new(), GroupsPanel) - initial).Length < 1, "A retargeted neighbor did not settle into its original slot.");
                PreviewSingleTileDrop(source, pointer, source.Id, 0); await Task.Delay(250);
                var destination = button.TranslatePoint(new(), GroupsPanel);
                Check((destination - initial).Length > 1, "A completed displacement lost its requested offset.");
                if (size == TileSize.Medium && !editing) SaveScreenshot(Path.Combine(directory, "placement-displaced.png"));
                Check(CommitSingleTileDrop(source, pointer, source.Id), "A moving preview could not commit.");
                UpdateLayout();
                Check((LayoutOrigin(_selectionViews[target.Id].Button, GroupsPanel) - destination).Length < 1, "A preview and committed target disagree.");
                await Task.Delay(250);
                Check(_tileTransitions.Count == 0 && _groupTransitions.Count == 0, "Settled placement retained transient offsets or clocks.");
                Check(Document.Groups.SelectMany(g => g.Tiles).All(t => !t.IsFolder), "Animation created an unrequested folder.");
            }

        var moving = new TileGroup { Name = "Moving group", X = 20, Column = 0, Row = 0, Tiles = [new() { Label = "Sample app" }] };
        Document.Groups.Add(moving); ClearDropPreview(); RenderGroups(); UpdateLayout();
        _dragOffset = new(5, 5); _dragging = true;
        var movingView = GroupsPanel.Children.OfType<StackPanel>().Single(s => s.Tag == moving);
        var start = movingView.TranslatePoint(new(), GroupsPanel);
        PreviewGroupOnBoard(moving, new(5, 5)); UpdateLayout();
        Check((movingView.TranslatePoint(new(), GroupsPanel) - start).Length < 1 && _groupTransitions.Count > 0, "A group preview jumped to its new slot.");
        var final = LayoutOrigin(movingView, GroupsPanel);
        await Task.Delay(70);
        var inFlight = movingView.TranslatePoint(new(), GroupsPanel);
        Check((inFlight - start).Length > 1 && (inFlight - final).Length > 1, "A group has no intermediate motion state.");
        Document.Preferences.Animate = false; RespectMotionPreference(); UpdateLayout();
        Check(_groupTransitions.Count == 0 && (movingView.TranslatePoint(new(), GroupsPanel) - final).Length < 1, "Disabling animation did not settle group motion.");
        ClearDropPreview(); _dragging = false;
        File.WriteAllText(Path.Combine(directory, "placement-timings.json"), JsonSerializer.Serialize(new
        {
            checks,
            stationarySamples = timings.Count * 500,
            stationaryPreviewMsPerSample = timings,
            note = "Synthetic UI-thread work with fictional apps; not a physical-input latency or display-frame-rate benchmark."
        }, LayoutStore.JsonOptions));
        return checks;
    }
}
