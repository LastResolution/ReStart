using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private static async Task<int> VerifyLegacyTileRenderingAsync(MainWindow fixture, string directory)
    {
        fixture.ResetToPinnedView();
        fixture.Document.Preferences.Language = "en"; L.SetLanguage("en"); fixture.ApplyAppearance();
        const string appId = "ReStart.OwnedTile_123!Main";
        const string xml = "<tile><visual><binding template='TileSmall'><text>7</text></binding>"
            + "<binding template='TileMedium'><text hint-style='subtitle'>Native tile</text><text>Owned update</text></binding>"
            + "<binding template='TileWide'><group><subgroup><text hint-style='subtitle'>Wide tile</text><text>Left column</text></subgroup><subgroup><image src='Assets/owned.png'/></subgroup></group></binding>"
            + "<binding template='TileLarge'><text hint-style='title'>Native live tile</text><text hint-wrap='true'>This is fictional, local notification data for the renderer fixture.</text></binding></visual></tile>";
        var checks = 0; var reads = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        var rasterPath = Path.Combine(directory, "legacy-owned.scale-200.png");
        var pixels = new byte[32 * 16 * 4];
        for (var pixel = 0; pixel < pixels.Length; pixel += 4) { pixels[pixel] = 255; pixels[pixel + 1] = 160; pixels[pixel + 2] = 50; pixels[pixel + 3] = 255; }
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(32, 16, 96, 96, PixelFormats.Bgra32, null, pixels, 128)));
        using (var stream = File.Open(rasterPath, FileMode.CreateNew)) encoder.Save(stream);
        Check(LegacyTileAssets.ResolveExistingAsset(Path.Combine(directory, "legacy-owned.png"), directory, true) == rasterPath,
            "A package image qualifier could not be resolved within its owning directory.");
        Check(LegacyTileAssets.ResolveExistingAsset(Path.Combine(directory, "legacy-owned.png"), directory, false) == null,
            "An app-local image source enumerated unrelated resource variants.");
        var raster = LegacyTileAssets.LoadRaster(rasterPath, directory);
        Check(raster is { IsFrozen: true, PixelWidth: 32, PixelHeight: 16 }, "An owned native raster lost its aspect ratio or background-thread ownership.");
        fixture._legacyTileFixture = async (_, token) =>
        {
            reads++; await Task.Delay(30, token);
            return new(true, new Dictionary<string, LegacyNotificationStore.Frame> { [appId] = new(LegacyTileContent.Read(xml), "12") });
        };
        fixture.Document.Preferences.LegacyUwpLiveTiles = true;
        var tiles = Enum.GetValues<TileSize>().Select(size => new Tile { AppId = appId, Target = appId, Label = "Owned package", Size = size }).ToList();
        fixture.Document.Groups = [new() { Name = "Windows tile compatibility", Tiles = tiles }];
        fixture.RenderGroups(); fixture.UpdateLayout(); fixture.RefreshLegacyTiles();
        Check(fixture.GroupsPanel.IsVisible && fixture.ResultsPanel.Visibility == Visibility.Collapsed,
            "The native tile renderer fixture is hidden behind another launcher view.");
        var deadline = Environment.TickCount64 + 3000;
        while (fixture._legacySnapshot.Apps.Count == 0 && Environment.TickCount64 < deadline) await Task.Delay(20);
        Check(fixture._legacySnapshot.Apps.Count == 1, "The owned legacy tile provider did not reach its views.");
        fixture._legacyImages = new Dictionary<(string, string), BitmapSource> { [(appId, "Assets/owned.png")] = raster! }; fixture.ApplyLegacyFrames();
        fixture.UpdateLayout(); await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
        Check(fixture._legacyViews.Where(v => v.Tile.Size == TileSize.Wide).All(v => VisualChildren<Image>(v.View).Any(i => i.Source == raster)),
            "A bounded native raster did not reach its adaptive column.");
        Check(fixture._legacyViews.Count == 4 && fixture._legacyViews.All(v => v.View.HasTileContent && v.Icon.Visibility == Visibility.Collapsed),
            "A supported native binding failed to replace the icon at one of the four tile sizes.");
        fixture._legacyNextPoll = Environment.TickCount64 + 60_000;
        foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
        {
            fixture.Document.Preferences.Theme = theme; fixture.ApplyAppearance(); fixture.RenderGroups(); fixture.UpdateLayout();
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
            Check(fixture._legacyViews.All(v => v.View.HasTileContent), "Native tile content was lost on a theme change.");
            SaveFixtureElement(fixture.Root, Path.Combine(directory, "legacy-tiles-" + theme + ".png"));
        }
        var retained = fixture._legacyViews.Select(v => v.View.Content).ToArray();
        fixture.RefreshLegacyTiles(); fixture.RefreshLegacyTiles();
        Check(retained.SequenceEqual(fixture._legacyViews.Select(v => v.View.Content)), "Unchanged native frames rebuilt their visuals.");
        var folder = new Tile { Label = "Owned folder", Children = [new() { AppId = appId, Target = appId, Size = TileSize.Large }] };
        fixture.Document.Groups[0].Tiles.Add(folder); fixture.RenderGroups(); fixture.UpdateLayout();
        Check(fixture._legacyViews.Count(v => v.Tile.Size == TileSize.Small && v.View.HasTileContent) >= 2,
            "A closed folder preview did not use the native small binding.");
        fixture.Dismiss(immediately: true); fixture.RefreshLegacyTiles();
        Check(reads == 1 && fixture._legacySnapshot.Apps.Count == 1, "Hidden native tiles read new data or discarded their retained frame.");
        fixture.Present();
        deadline = Environment.TickCount64 + 2000;
        while ((reads < 2 || fixture._legacyReadCancellation != null) && Environment.TickCount64 < deadline) await Task.Delay(20);
        Check(reads == 2 && fixture._legacySnapshot.Apps.Count == 1, "Reopening did not refresh the retained native frames.");
        fixture._legacyNextPoll = Environment.TickCount64 + 60_000;
        fixture._legacySnapshot = new(true, new Dictionary<string, LegacyNotificationStore.Frame>
        { [appId] = new(LegacyTileContent.Read(xml).Where(b => b.Size == TileSize.Wide).ToArray(), "") });
        fixture.ApplyLegacyFrames();
        Check(fixture._legacyViews.Where(v => v.Tile.Size != TileSize.Wide).All(v => !v.View.HasTileContent && v.Icon.Visibility == Visibility.Visible),
            "A missing native size binding stretched another size instead of restoring the app icon.");
        Check(fixture.TileMenu(tiles[0]).Items.OfType<MenuItem>().Any(m => Equals(m.Tag, "live-more")), "An enabled native tile has no live-update toggle.");
        fixture.Document.Preferences.LegacyUwpLiveTiles = false; fixture.RenderGroups(); fixture.RefreshLegacyTiles();
        Check(fixture._legacyViews.Count == 0 && fixture._legacySnapshot.Apps.Count == 0 && fixture._legacyReadCancellation == null,
            "Disabling native tiles retained a data reader or notification content.");
        Check(reads == 2, "Rendering or refreshing native tiles unnecessarily reread the notification provider.");
        fixture._legacyTileFixture = null;
        return checks;
    }
}
