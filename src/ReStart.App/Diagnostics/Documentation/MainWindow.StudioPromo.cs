using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private async Task RenderCreativeGalleryAsync(string directory, ReStart.Core.ThemeMode theme = ReStart.Core.ThemeMode.Dark)
    {
        _documentationSampleConnections = true;
        var tiles = new[] { "weather-scene", "system-dashboard", "news-board", "canvas-garden" }
            .Select(id => _scriptPackages.Import(ScriptTilePackages.BundledManifestPath(id))).ToList();
        foreach (var tile in tiles) { tile.Size = TileSize.Large; tile.Column = 0; tile.Row = 0; }
        Document = new()
        {
            Preferences = new() { Language = "en", Theme = theme, Columns = 8, TileScale = 1.5, ShowWallpaper = false, Animate = false },
            Groups = tiles.Select((tile, index) => new TileGroup { IsStandalone = true, Column = 0, X = 2 + index * 4.7, Row = 5, Tiles = [tile] }).ToList()
        };
        ApplyAppearance(); ResetToPinnedView(); RenderGroups(); SetStatus("");
        Root.Measure(new(Root.Width, Root.Height)); Root.Arrange(new Rect(0, 0, Root.Width, Root.Height)); FitPanels(); UpdateLayout();
        await WaitForDocumentationFramesAsync();
        Require(GroupsPanel.Children.Count == 4 && GroupsPanel.ActualWidth > 1000 && ContentScroll.ViewportHeight > 500, "The creative gallery was clipped by an earlier fixture viewport.");
        SaveScreenshot(Path.Combine(directory, "web-examples.png"));
    }

    private async Task<BitmapSource[]> RenderStudioPromoScenesAsync(string directory, ReStart.Core.ThemeMode theme)
    {
        var result = new List<BitmapSource>();
        foreach (var document in new[] { 1, 2, 0 })
        {
            Document.Preferences.Theme = theme; ApplyAppearance();
            var studio = new TileStudioWindow(this, offscreen: true) { Width = 1600, Height = 760 };
            try
            {
                studio.PrepareDocumentation("orbit", document);
                var content = (FrameworkElement)studio.Content;
                using var host = HiddenRenderHost(content, (int)studio.Width, (int)studio.Height);
                content.Measure(new(studio.Width, studio.Height)); content.Arrange(new Rect(0, 0, studio.Width, studio.Height)); content.UpdateLayout();
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                await studio.PreviewAsync();
                Require(studio.PreviewSucceeded && studio.PreviewCount == 4, "The demonstration authoring preview failed: " + studio.PreviewError);
                await studio.FreezeForDocumentationAsync();
                var path = Path.Combine(directory, $"studio-{result.Count}.png"); SaveFixtureElement(studio, path);
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(path); image.EndInit(); image.Freeze();
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(studio.Background, null, new Rect(0, 0, 1920, 900));
                    var scale = Math.Min(1920d / image.PixelWidth, 900d / image.PixelHeight);
                    dc.DrawImage(image, new Rect((1920 - image.PixelWidth * scale) / 2, (900 - image.PixelHeight * scale) / 2, image.PixelWidth * scale, image.PixelHeight * scale));
                }
                var frame = new RenderTargetBitmap(1920, 900, 96, 96, PixelFormats.Pbgra32); frame.Render(visual); frame.Freeze(); result.Add(frame);
                Require(!studio.IsVisible && !IsVisible, "Product authoring opened a visible window.");
                host.RootVisual = null;
            }
            finally { studio.Close(); }
        }
        Document.Preferences.Theme = theme; ApplyAppearance();
        return result.ToArray();
    }
}
