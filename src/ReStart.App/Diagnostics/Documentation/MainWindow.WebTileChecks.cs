using System.Text.Json;
using System.Windows;
using ReStart.App.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private async Task<int> VerifyWebSamplesAsync(string directory)
    {
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        foreach (var id in ScriptTilePackages.BundledIds)
        {
            Document.Preferences.Language = "en"; LocalizationResources.Apply("en");
            var manifest = ScriptTilePackages.BundledManifest(id);
            var folder = Path.GetDirectoryName(ScriptTilePackages.BundledManifestPath(id))!;
            var web = ScriptTilePackages.ReadWebFolder(folder); var source = File.ReadAllText(Path.Combine(folder, "tile.js"));
            using var browser = new HtmlTileView(_scriptPackages.BrowserDataRoot, Guid.NewGuid().ToString("N"), offscreen: true) { Width = 200, Height = 200 };
            using var host = HiddenRenderHost(browser, 200, 200);
            try
            {
                foreach (var size in Enum.GetValues<TileSize>())
                {
                    var context = TileStudioContext.Build(manifest, size, 1, TileStudioContext.Sample(manifest), JsonSerializer.SerializeToElement(new { }), false,
                        DateTimeOffset.Now.AddSeconds(-10), reducedMotion: true, preview: true);
                    var (frame, error) = await browser.RenderAsync(web.Html, web.Css, source, false, context);
                    Check(frame != null, $"Web sample {id}/{size} failed: {error}");
                    var (width, height) = TileLayout.Dimensions(size);
                    using var geometry = JsonDocument.Parse(JsonSerializer.Deserialize<string>(await browser.InspectAsync("JSON.stringify({width:innerWidth,height:innerHeight,size:document.documentElement.dataset.size})"))!);
                    Check(Math.Abs(geometry.RootElement.GetProperty("width").GetDouble() - (width * 52 - 4)) <= 1 &&
                        Math.Abs(geometry.RootElement.GetProperty("height").GetDouble() - (height * 52 - 4)) <= 1,
                        $"Web sample {id}/{size} used a stale viewport.");
                    if (size == TileSize.Large && id is "weather-scene" or "system-dashboard" or "news-board" or "canvas-garden")
                    { using var image = File.Create(Path.Combine(directory, id + ".png")); await browser.CaptureAsync(image); }
                }
                foreach (var language in L.SupportedLanguages)
                {
                    Document.Preferences.Language = language; LocalizationResources.Apply(language);
                    var context = TileStudioContext.Build(manifest, TileSize.Wide, 1, TileStudioContext.Sample(manifest), JsonSerializer.SerializeToElement(new { }), false,
                        DateTimeOffset.Now, preview: true, showTitle: false);
                    var (frame, error) = await browser.RenderAsync(web.Html, web.Css, source, false, context);
                    Check(frame != null, $"Web sample {id}/{language} failed: {error}");
                    Check(await browser.InspectAsync("document.documentElement.lang") == JsonSerializer.Serialize(language), "A web document did not follow its UI language.");
                    Check(await browser.InspectAsync("[...document.querySelectorAll('[data-restart-title]')].every(x=>getComputedStyle(x).display==='none')") == "true",
                        "A bundled sample ignored the live tile name preference.");
                }
                if (web.Css.Contains(".card", StringComparison.Ordinal))
                    foreach (var color in new[] { "#FFFFFF", "#000000" })
                        foreach (var size in Enum.GetValues<TileSize>())
                        {
                            var context = TileStudioContext.Build(manifest, size, 1, TileStudioContext.Sample(manifest), JsonSerializer.SerializeToElement(new { }), false,
                                DateTimeOffset.Now, preview: true, customColor: color);
                            var (frame, error) = await browser.RenderAsync(web.Html, web.Css, source, false, context);
                            Check(frame != null, "A custom-color sample did not render: " + error);
                            Check(await browser.InspectAsync("getComputedStyle(document.querySelector('.card')).color") ==
                                JsonSerializer.Serialize(color == "#FFFFFF" ? "rgb(0, 0, 0)" : "rgb(255, 255, 255)"), "A custom tile color did not propagate readable text: " + id);
                        }
                if (id == "orbit")
                {
                    var context = TileStudioContext.Build(manifest, TileSize.Large, 1, TileStudioContext.Sample(manifest), JsonSerializer.SerializeToElement(new { }), false,
                        DateTimeOffset.Now, theme: "light", preview: true);
                    await browser.RenderAsync(web.Html, web.Css, source, false, context);
                    Check(await browser.InspectAsync("getComputedStyle(document.querySelector('.card')).color") == "\"rgb(21, 43, 59)\"", "The transparent clock kept white text on a light launcher.");
                    using var image = File.Create(Path.Combine(directory, "orbit-light.png")); await browser.CaptureAsync(image);
                }
            }
            finally { host.RootVisual = null; }
        }
        Document.Preferences.Language = "en"; LocalizationResources.Apply("en");
        // A deliberately stale host snapshot must not make the SVG second hand stale.
        using (var browser = new HtmlTileView(_scriptPackages.BrowserDataRoot, Guid.NewGuid().ToString("N"), offscreen: true) { Width = 200, Height = 96 })
        using (var host = HiddenRenderHost(browser, 200, 96))
        {
            var manifest = ScriptTilePackages.BundledManifest("orbit"); var folder = Path.GetDirectoryName(ScriptTilePackages.BundledManifestPath("orbit"))!;
            var web = ScriptTilePackages.ReadWebFolder(folder);
            var context = TileStudioContext.Build(manifest, TileSize.Wide, 1, TileStudioContext.Sample(manifest), JsonSerializer.SerializeToElement(new { }), false,
                DateTimeOffset.Now.AddSeconds(-10), reducedMotion: false);
            var (frame, error) = await browser.RenderAsync(web.Html, web.Css, File.ReadAllText(Path.Combine(folder, "tile.js")), false, context);
            Check(frame != null, "The animated SVG clock failed: " + error);
            await Task.Delay(120);
            const string hand = "(()=>{const n=[...document.querySelectorAll('#visual line')].at(-1);return [Number(n.getAttribute('x2')),Number(n.getAttribute('y2'))]})()";
            var first = await browser.InspectAsync(hand);
            await Task.Delay(350);
            Check(first != await browser.InspectAsync(hand), "The second hand waited for another host update.");
            Check(await browser.InspectAsync("(()=>{const n=[...document.querySelectorAll('#visual line')].at(-1),d=new Date(),a=(d.getSeconds()+d.getMilliseconds()/1000)*Math.PI/30;return Math.hypot(Number(n.getAttribute('x2'))-(50+Math.sin(a)*36),Number(n.getAttribute('y2'))-(50-Math.cos(a)*36))<3})()") == "true",
                "The second hand displayed the old host timestamp instead of PC time.");
            host.RootVisual = null;
        }
        return checks;
    }
}
