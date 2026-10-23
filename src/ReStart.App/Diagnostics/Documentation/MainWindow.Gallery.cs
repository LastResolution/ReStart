using System.Text.Json;
using System.Windows;
using ReStart.App.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private async Task RenderTileGalleryAsync(string directory)
    {
        foreach (var theme in new[] { "dark", "light" })
            foreach (var id in new[] { "orbit", "cpu", "memory", "calendar", "weather-scene", "canvas-garden" })
            {
                var manifest = ScriptTilePackages.BundledManifest(id);
                var folder = Path.GetDirectoryName(ScriptTilePackages.BundledManifestPath(id))!;
                var web = ScriptTilePackages.ReadWebFolder(folder);
                var context = TileStudioContext.Build(manifest, TileSize.Large, 724d / 208,
                    TileStudioContext.Sample(manifest), JsonSerializer.SerializeToElement(new { history = new[] { .25, .3, .28, .4, .37 } }),
                    false, new DateTimeOffset(2026, 10, 18, 9, 41, 30, TimeSpan.Zero), theme: theme, preview: true);
                using var browser = new HtmlTileView(_scriptPackages.BrowserDataRoot, Guid.NewGuid().ToString("N"), offscreen: true) { Width = 720, Height = 720 };
                using var host = HiddenRenderHost(browser, 720, 720);
                try
                {
                    var background = theme == "light" ? "#eeeeee" : "#1f1f1f";
                    var (frame, error) = await browser.RenderAsync(web.Html, web.Css + "\nhtml,body{background:" + background + "}", File.ReadAllText(Path.Combine(folder, "tile.js")), false, context);
                    Require(frame != null, "The themed tile gallery failed: " + id + "/" + theme + ": " + error);
                    await browser.FreezeForDocumentationAsync();
                    using var image = File.Create(Path.Combine(directory, "tile-" + id + "-" + theme + ".png"));
                    await browser.CaptureAsync(image);
                }
                finally { host.RootVisual = null; }
            }
    }
}
