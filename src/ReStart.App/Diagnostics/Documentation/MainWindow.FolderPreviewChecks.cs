using System.Windows;
using System.Windows.Controls;
using ReStart.App.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private async Task<int> VerifyFolderPreviewsAsync(string directory)
    {
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        Content = null; Root.Width = 1280; Root.Height = 800;
        using var host = HiddenRenderHost(Root, 1280, 800);
        var original = Document;
        _documentationHtmlRendering = _startupPreparing = _documentationSampleConnections = true;
        try
        {
            foreach (var size in Enum.GetValues<TileSize>())
                foreach (var folderDestination in new[] { false, true })
                {
                    var source = new Tile { Label = "Source", Size = size };
                    var existing = new Tile { Label = "Existing", Size = size, Column = 0, Row = 0 };
                    var folder = new Tile { Label = "Folder", Children = [existing] };
                    var owner = new TileGroup { Column = 0, Row = 0, X = 0, Tiles = [folderDestination ? folder : existing] };
                    Document = new()
                    {
                        Preferences = new() { Columns = 8, Animate = false, ShowWallpaper = false },
                        Groups = [owner, new() { IsStandalone = true, X = 20, Column = 0, Row = 0, Tiles = [source] }]
                    };
                    _openFolder = folderDestination ? folder : null;
                    RenderGroups(); UpdateLayout();
                    var destination = folderDestination ? VisualChildren<TilePanel>(GroupsPanel).Single(p => p.FolderOwner == folder) : RootPanelFor(owner);
                    var (width, height) = TileLayout.Dimensions(size);
                    _dragOffset = new((width * Cell - 4) / 2, (height * Cell - 4) / 2);
                    var point = destination.TranslatePoint(new(width * Cell + _dragOffset.X, _dragOffset.Y), GroupsPanel);
                    var drop = ResolveSingleTileDrop(source, point, source.Id, 0);
                    Check(drop?.Group == owner && drop.Panel?.FolderOwner == (folderDestination ? folder : null) && drop.Column == width && drop.Row == 0,
                        $"A compact {(folderDestination ? "folder" : "group")} rejected a horizontal {size} drop.");
                    Check(CommitSingleTileDrop(source, point, source.Id), "A horizontal drop could not be committed.");
                    var packed = TileLayout.Pack(folderDestination ? folder.Children : owner.Tiles, 8);
                    Check(packed.Single(p => p.Tile == source).Column == width && packed.Single(p => p.Tile == source).Row == 0,
                        "A horizontal drop wrapped or changed ownership after commit.");
                }
            var children = new[] { "orbit", "calendar", "feed", "system-dashboard" }.Select(id => _scriptPackages.Import(ScriptTilePackages.BundledManifestPath(id))).ToArray();
            for (var i = 0; i < children.Length; i++) children[i].Size = (TileSize)i;
            var sizes = children.Select(t => t.Size).ToArray();
            var liveFolder = new Tile { Label = "Live folder", Size = TileSize.Large, Children = children.ToList() };
            Document = new()
            {
                Preferences = new() { Language = "en", Columns = 8, ShowWallpaper = false, Animate = false },
                Groups = [new() { Name = "Folder previews", Column = 0, Row = 0, Tiles = [liveFolder] }]
            };
            _openFolder = null; RenderGroups(); UpdateLayout();
            await WaitForDocumentationFramesAsync();
            Check(_scriptViews.Count == 4 && _scriptViews.All(v => v.Tile.Size == TileSize.Medium), "Mixed child sizes leaked into miniature geometry.");
            Check(children.Select(t => t.Size).SequenceEqual(sizes), "Rendering miniatures mutated saved child sizes.");
            Check(_htmlTiles.Values.All(view => view.BrowserReady && view.Children.OfType<Image>().Any(i => i.Source != null)), "A closed folder used generic icons instead of rendered web previews.");
            RenderGroups(); UpdateLayout();
            Check(ScriptsReady && ScriptFrameCount == 4, "An unchanged board discarded successful browser frames.");
            var clock = children[0];
            var retained = _scriptSessions[clock.Id];
            retained.State = System.Text.Json.JsonSerializer.SerializeToElement(new { seconds = true });
            retained.Inputs.Enqueue(new("click", 123));
            _documentationHtmlRendering = false;
            HtmlView(clock);
            _documentationHtmlRendering = true;
            HtmlView(clock, thumbnail: true);
            Check(_scriptSessions[clock.Id].State.GetProperty("seconds").GetBoolean() && _scriptSessions[clock.Id].Inputs.Count == 1,
                "Switching between a miniature and an interactive browser discarded state or input.");
            RenderGroups(); UpdateLayout(); await WaitForDocumentationFramesAsync();
            SaveScreenshot(Path.Combine(directory, "live-folder-closed.png"));
            OpenFolder(liveFolder); UpdateLayout(); await WaitForDocumentationFramesAsync();
            Check(_scriptViews.Count == 4 && _scriptViews.Select(v => v.Tile.Size).Order().SequenceEqual(sizes.Order()), "Opening the folder did not restore actual tile sizes.");
            SaveScreenshot(Path.Combine(directory, "live-folder-open.png"));
            CloseFolder(this, new()); UpdateLayout(); await WaitForDocumentationFramesAsync();
            Check(_scriptViews.Count == 4 && ScriptsReady, "Closing a live folder stopped its previews.");
            foreach (var view in _htmlTiles.Values) Check(await view.InspectAsync("document.documentElement.dataset.size") == "\"medium\"", "A reopened miniature kept its former large viewport.");
            var counter = _scriptPackages.Import(ScriptTilePackages.BundledManifestPath("clock"));
            File.WriteAllText(Path.Combine(_scriptPackages.Folder(counter.ScriptId), "tile.js"),
                "function render(ctx){const count=(ctx.state.count||0)+1;document.getElementById('value').textContent=String(count);return {text:String(count),state:{count}}}");
            liveFolder.Children[0] = counter;
            RenderGroups(); UpdateLayout(); await WaitForDocumentationFramesAsync();
            Check(_scriptViews.All(view => view.Body.IsVisible), "A closed folder's live previews were excluded from normal visible-tile updates.");
            var beforeCount = _scriptFrames[counter.Id].State.GetProperty("count").GetInt32();
            var beforeImage = _htmlTiles[counter.Id].Children.OfType<Image>().Single().Source;
            await Task.Delay(1100);
            await UpdateScriptTilesAsync();
            Check(_scriptFrames[counter.Id].State.GetProperty("count").GetInt32() > beforeCount, "A closed folder stopped scheduled live updates after its first frame.");
            Check(!ReferenceEquals(beforeImage, _htmlTiles[counter.Id].Children.OfType<Image>().Single().Source), "A closed folder updated its data but left a frozen miniature image.");
            var seed = CreateInitialLayout();
            Check(seed.Groups.SelectMany(g => g.Tiles).All(t => t.Widget is not ("clock" or "calendar")), "The initial layout retained legacy widgets.");
            Check(seed.Groups.SelectMany(g => g.Tiles).Count(t => t.Widget == "script") == 2, "The initial layout did not install its bundled clock/calendar.");
        }
        finally
        {
            _openFolder = null; _startupPreparing = _documentationSampleConnections = false;
            host.RootVisual = null; Content = Root; Document = original; RenderGroups();
        }
        return checks;
    }
}
