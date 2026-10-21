using System.Text.Json;
using ReStart.App.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private async Task<int> VerifyWebLayoutMatrixAsync(string directory)
    {
        var checks = 0; var failures = new List<object>();
        var now = new DateTimeOffset(2026, 10, 18, 9, 41, 0, TimeSpan.Zero);
        foreach (var id in ScriptTilePackages.BundledIds)
        {
            var manifest = ScriptTilePackages.BundledManifest(id);
            var folder = Path.GetDirectoryName(ScriptTilePackages.BundledManifestPath(id))!;
            var web = ScriptTilePackages.ReadWebFolder(folder); var source = File.ReadAllText(Path.Combine(folder, "tile.js"));
            using var browser = new HtmlTileView(_scriptPackages.BrowserDataRoot, Guid.NewGuid().ToString("N"), offscreen: true) { Width = 200, Height = 200 };
            using var host = HiddenRenderHost(browser, 200, 200);
            try
            {
                foreach (var scale in new[] { .7, 1, 1.5 })
                    foreach (var size in Enum.GetValues<TileSize>())
                        foreach (var language in L.SupportedLanguages)
                            foreach (var theme in new[] { "dark", "light" })
                                foreach (var showTitle in new[] { true, false })
                                {
                                    LocalizationResources.Apply(language);
                                    var context = TileStudioContext.Build(manifest, size, scale, TileStudioContext.Sample(manifest), JsonSerializer.SerializeToElement(new { }), false,
                                        now, theme: theme, reducedMotion: true, preview: true, showTitle: showTitle);
                                    var (frame, error) = await browser.RenderAsync(web.Html, web.Css, source, false, context);
                                    var geometry = frame == null ? JsonSerializer.Serialize(new[] { error ?? "No browser frame" })
                                        : JsonSerializer.Deserialize<string>(await browser.InspectAsync(WebLayoutGeometry))!;
                                    checks++;
                                    if (geometry != "[]") failures.Add(new { id, size = size.ToString(), scale, language, theme, showTitle, geometry });
                                    if (scale == 1 && language == "en" && theme == "dark" && showTitle && frame != null)
                                    {
                                        using var image = File.Create(Path.Combine(directory, $"matrix-{id}-{size}.png")); await browser.CaptureAsync(image);
                                    }
                                }
                if (manifest.Capabilities.Contains("data.sources"))
                    foreach (var status in new[] { "unconfigured", "loading", "error" })
                        foreach (var size in Enum.GetValues<TileSize>())
                            foreach (var language in L.SupportedLanguages)
                            {
                                LocalizationResources.Apply(language);
                                var samples = System.Text.Json.Nodes.JsonNode.Parse(TileStudioContext.Sample(manifest))!;
                                foreach (var pair in samples["data"]!.AsObject())
                                { pair.Value!["status"] = status; pair.Value["value"] = null; }
                                var context = TileStudioContext.Build(manifest, size, 1, samples.ToJsonString(), JsonSerializer.SerializeToElement(new { }), false, now, preview: true);
                                var (frame, error) = await browser.RenderAsync(web.Html, web.Css, source, false, context);
                                var geometry = frame == null ? JsonSerializer.Serialize(new[] { error ?? "No browser frame" })
                                    : JsonSerializer.Deserialize<string>(await browser.InspectAsync(WebLayoutGeometry))!;
                                checks++;
                                if (geometry != "[]") failures.Add(new { id, size = size.ToString(), scale = 1, language, status, geometry });
                            }
                if (id == "weather-scene")
                    foreach (var code in new[] { 2, 61, 73 })
                        foreach (var scale in new[] { .7, 1, 1.5 })
                            foreach (var size in Enum.GetValues<TileSize>())
                                foreach (var language in L.SupportedLanguages)
                                    foreach (var theme in new[] { "dark", "light" })
                                    {
                                        LocalizationResources.Apply(language);
                                        var sample = System.Text.Json.Nodes.JsonNode.Parse(TileStudioContext.Sample(manifest))!;
                                        sample["data"]!["weather"]!["value"]!["weather_code"] = code;
                                        sample["data"]!["weather"]!["value"]!["temperature_2m"] = -18;
                                        var context = TileStudioContext.Build(manifest, size, scale, sample.ToJsonString(), JsonSerializer.SerializeToElement(new { }), false, now, theme: theme, preview: true);
                                        var (frame, error) = await browser.RenderAsync(web.Html, web.Css, source, false, context);
                                        var geometry = frame == null ? JsonSerializer.Serialize(new[] { error ?? "No weather frame" }) : JsonSerializer.Deserialize<string>(await browser.InspectAsync(WebLayoutGeometry))!;
                                        var readable = frame != null && await browser.InspectAsync("document.getElementById('temperature').scrollWidth<=document.getElementById('temperature').clientWidth+1") == "true";
                                        checks++;
                                        if (geometry != "[]" || !readable) failures.Add(new { id, code, size = size.ToString(), scale, language, theme, geometry, readable });
                                        if (code == 2 && scale == 1 && language == "en" && theme == "dark" && frame != null)
                                        { using var image = File.Create(Path.Combine(directory, $"weather-cloud-{size}.png")); await browser.CaptureAsync(image); }
                                    }
            }
            finally { host.RootVisual = null; }
        }
        LocalizationResources.Apply("en");
        File.WriteAllText(Path.Combine(directory, "layout-matrix.json"), JsonSerializer.Serialize(new { checks, failures }));
        Require(failures.Count == 0, "Web tile text/artwork exceeded or overlapped its layout: " + JsonSerializer.Serialize(failures.Take(4)));
        return checks;
    }

    private const string WebLayoutGeometry = """
        (() => {
          const root = document.querySelector('.card,.tile') || document.body;
          const issues = [], visible = element => element.getClientRects().length && getComputedStyle(element).visibility !== 'hidden';
          const leaves = [...root.querySelectorAll('*')].filter(element => element.namespaceURI === 'http://www.w3.org/1999/xhtml' && !element.children.length && element.textContent.trim() && visible(element));
          const boxes = leaves.map(element => {
            const bounds = element.getBoundingClientRect();
            const box = {left:bounds.left,top:bounds.top,right:bounds.right,bottom:bounds.bottom,width:bounds.width,height:bounds.height};
            // Deliberately scrollable stories are clipped by their own viewport.
            // Hidden overflow elsewhere remains a failure, not an exemption.
            for (let parent=element.parentElement; parent && parent!==root; parent=parent.parentElement) {
              const style=getComputedStyle(parent), clip=parent.getBoundingClientRect();
              if (/^(auto|scroll)$/.test(style.overflowY)) { box.top=Math.max(box.top,clip.top); box.bottom=Math.min(box.bottom,clip.bottom); }
              if (/^(auto|scroll)$/.test(style.overflowX)) { box.left=Math.max(box.left,clip.left); box.right=Math.min(box.right,clip.right); }
            }
            box.width=Math.max(0,box.right-box.left); box.height=Math.max(0,box.bottom-box.top);
            return {element,box,id:element.id || element.className || element.tagName};
          }).filter(item=>item.box.width>.1 && item.box.height>.1);
          for (const {element, box, id} of boxes) {
            if (box.width < .1 || box.height < .1) continue;
            if (box.left < -.75 || box.top < -.75 || box.right > innerWidth + .75 || box.bottom > innerHeight + .75) issues.push('outside:' + id);
            const style = getComputedStyle(element);
            if (style.textOverflow !== 'ellipsis' && Number(style.webkitLineClamp) <= 0 && element.scrollHeight > element.clientHeight + 1) issues.push('clipped:' + id);
          }
          for (let i=0;i<boxes.length;i++) for (let j=i+1;j<boxes.length;j++) {
            const a=boxes[i], b=boxes[j];
            if (Math.min(a.box.right,b.box.right)-Math.max(a.box.left,b.box.left)>1 && Math.min(a.box.bottom,b.box.bottom)-Math.max(a.box.top,b.box.top)>1) issues.push('overlap:' + a.id + '/' + b.id);
          }
          for (const svg of root.querySelectorAll('svg')) if (visible(svg)) {
            const box=svg.getBoundingClientRect();
            if (box.left < -.75 || box.top < -.75 || box.right > innerWidth + .75 || box.bottom > innerHeight + .75) issues.push('outside:SVG');
          }
          const copy=root.querySelector('.weather-copy'), art=root.querySelector('.weather-art');
          if (copy && art && document.documentElement.dataset.size!=='small' && copy.getBoundingClientRect().right>art.getBoundingClientRect().left+.75) issues.push('weather-art-overlaps-copy');
          const sun=root.querySelector('.weather-art .sun');
          if (sun && art) {
            const shape=sun.getBoundingClientRect(), clip=art.getBoundingClientRect();
            if (Math.abs(shape.width-shape.height)>.75 || shape.left<clip.left-.75 || shape.top<clip.top-.75 || shape.right>clip.right+.75 || shape.bottom>clip.bottom+.75) issues.push('clipped-or-distorted-sun');
          }
          return JSON.stringify([...new Set(issues)]);
        })()
        """;
}
