using System.Text.Json;
using System.Windows;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private int VerifyDropProjection(string directory)
    {
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        var original = Document; Content = null; Root.Width = 1280; Root.Height = 800;
        using var host = HiddenRenderHost(Root, 1280, 800);
        var script = _scriptPackages.Import(ScriptTilePackages.BundledManifestPath("clock"));
        try
        {
            foreach (var kind in new[] { "group", "folder", "independent" })
                foreach (var live in new[] { false, true })
                    foreach (var sourceSize in Enum.GetValues<TileSize>())
                        foreach (var targetSize in Enum.GetValues<TileSize>())
                        {
                            var source = Item("Moving", sourceSize); var target = Item("Displaced", targetSize);
                            var folder = new Tile { Label = "Open folder", Children = [target] };
                            var owner = new TileGroup { Name = "Destination", Column = 0, Row = 0, X = 0, IsStandalone = kind == "independent", Tiles = [kind == "folder" ? folder : target] };
                            Document = new()
                            {
                                Preferences = new() { FreeTilePlacement = true, Columns = 8, Animate = false, ShowWallpaper = false, CreateFoldersOnDrop = false, GroupTilesOnDrop = false },
                                Groups = [owner, new() { Column = 0, Row = 0, X = 20, IsStandalone = true, Tiles = [source] }]
                            };
                            _openFolder = kind == "folder" ? folder : null;
                            ClearDropPreview(); RenderGroups(); Root.Measure(new(1280, 800)); Root.Arrange(new Rect(0, 0, 1280, 800)); FitPanels(); UpdateLayout();
                            var panel = kind == "folder" ? VisualChildren<Controls.TilePanel>(GroupsPanel).Single(p => p.FolderOwner == folder) : RootPanelFor(owner);
                            var (width, height) = TileLayout.Dimensions(source.Size);
                            _dragOffset = new((width * Cell - 4) / 2, (height * Cell - 4) / 2);
                            var point = panel.TranslatePoint(_dragOffset, GroupsPanel);
                            var before = JsonSerializer.Serialize(Document.Groups, LayoutStore.JsonOptions);
                            var initial = Position(target.Id);
                            Check(PreviewSingleTileDrop(source, point, source.Id, 0), "A displacement preview was rejected.");
                            var projected = Position(target.Id);
                            Check((projected - initial).Length > 1, $"An overlapping {kind} tile did not move during preview: {sourceSize}/{targetSize}/{live}; hover={_folderHover.TargetId}; plan={_dropPlan != null}; transforms={_tileTransitions.Count}; destination={_dropPlanKey?.Group}.");
                            Check(before == JsonSerializer.Serialize(Document.Groups, LayoutStore.JsonOptions), "A transient displacement changed the saved layout.");
                            PreviewSingleTileDrop(source, point, source.Id, 0);
                            Check((Position(target.Id) - projected).Length < .1, "A stationary tile preview oscillated between positions.");
                            ClearDropPreview();
                            Check((Position(target.Id) - initial).Length < .1, "Cancelling a tile preview left a displaced visual.");
                            PreviewSingleTileDrop(source, point, source.Id, 0);
                            Check(CommitSingleTileDrop(source, point, source.Id), "A previewed tile move could not commit.");
                            UpdateLayout();
                            Check((Position(target.Id) - projected).Length < 1, $"A tile's projected and committed locations disagree: {kind}/{sourceSize}/{targetSize}/{live}; projected={projected}; actual={Position(target.Id)}.");

                            Tile Item(string name, TileSize size) => new()
                            {
                                Label = name,
                                Size = size,
                                Column = 0,
                                Row = 0,
                                Widget = live ? "script" : "",
                                ScriptId = live ? script.ScriptId : ""
                            };
                        }
        }
        finally { ClearDropPreview(); _openFolder = null; host.RootVisual = null; Content = Root; Document = original; RenderGroups(); }
        return checks;

        Point Position(string id) => _selectionViews[id].Button.TranslatePoint(new(), GroupsPanel);
    }
}
