using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ReStart.App.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private async Task<int> VerifyWebScrollingAsync()
    {
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        using var first = new HtmlTileView(_scriptPackages.BrowserDataRoot, Guid.NewGuid().ToString("N"), offscreen: true);
        using var second = new HtmlTileView(_scriptPackages.BrowserDataRoot, Guid.NewGuid().ToString("N"), offscreen: true);
        const string html = "<html><head><link rel='stylesheet' href='/tile.css'></head><body><div id='story' data-restart-scroll><div id='long'>Story</div></div><div id='horizontal'><div>Horizontal story</div></div><script src='/tile.js'></script></body></html>";
        const string css = "html,body{margin:0;width:100%;height:100%;overflow:hidden}#story{position:absolute;left:0;top:10px;width:100px;height:80px;overflow:auto}#long{height:600px}#horizontal{position:absolute;left:110px;top:10px;width:80px;height:80px;overflow:auto}#horizontal>div{width:600px}";
        var context = new { state = new { }, size = "wide", bounds = new { width = 200, height = 96, scale = 1 } };
        const string top = "document.getElementById('story').scrollTop";
        foreach (var view in new[] { first, second })
        { var (frame, error) = await view.RenderAsync(html, css, "function render(){return {text:'Scroll fixture',state:{}}}", false, context); Check(frame != null, "Scroll fixture failed: " + error); }
        Check(await first.ScrollAsync(new(20, 30), -120), "A scroll region did not own its wheel.");
        Check(await first.InspectAsync(top) == "48", "The addressed story did not move by one wheel step.");
        Check(await second.InspectAsync(top) == "0", "A neighboring tile scrolled.");
        Check(await first.ScrollAsync(new(20, 30), 120) && await first.InspectAsync(top) == "0", "Scrolling upward failed.");
        Check(await first.ScrollAsync(new(20, 30), 120), "The upper edge leaked input to the board.");
        await first.InspectAsync("document.getElementById('story').scrollTop=10000");
        var end = await first.InspectAsync(top);
        Check(await first.ScrollAsync(new(20, 30), -120) && await first.InspectAsync(top) == end, "The lower edge leaked input to the board.");
        Check(await first.ScrollAsync(new(115, 25), -120, horizontal: true), "A horizontal region rejected Shift+wheel.");
        Check(await first.InspectAsync("document.getElementById('horizontal').scrollLeft") == "48", "Horizontal scrolling failed.");
        Check(await second.InspectAsync("document.getElementById('horizontal').scrollLeft") == "0", "A neighboring tile moved horizontally.");
        Check(!await first.ScrollAsync(new(-5, 20), -120), "A pointer outside the tile affected its content.");
        Check(!await first.ScrollAsync(new(20, 30), -120, horizontal: true), "A vertical story accidentally accepted horizontal input.");
        await first.InspectAsync("document.getElementById('story').scrollTop=0");
        Check(await first.ScrollAsync(new(180, 94), -120) && await first.InspectAsync(top) == "48", "A marked story did not own its fixed footer.");
        Check(await first.ScrollAsync(new(20, 30), -120, page: true) && await first.InspectAsync(top) == "116", "Page Down did not use the story viewport.");

        // Exercise the launcher's routing with a board that can scroll in both axes.
        var content = ContentScroll.Content; var width = ContentScroll.Width; var height = ContentScroll.Height;
        var parent = (Panel)ContentScroll.Parent; var index = parent.Children.IndexOf(ContentScroll);
        parent.Children.Remove(ContentScroll);
        using var boardHost = HiddenRenderHost(ContentScroll, 320, 200);
        ContentScroll.Width = 320; ContentScroll.Height = 200; ContentScroll.Content = new Border { Width = 2000, Height = 2000 };
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
        ContentScroll.Measure(new(320, 200)); ContentScroll.Arrange(new Rect(0, 0, 320, 200)); ContentScroll.UpdateLayout();
        _htmlTiles.Add("scroll-fixture", first);
        try
        {
            ContentScroll.ScrollToVerticalOffset(300); ContentScroll.ScrollToHorizontalOffset(300); ContentScroll.UpdateLayout();
            Check(ContentScroll.ScrollableHeight > 0 && ContentScroll.ScrollableWidth > 0, "The routing fixture did not provide an overflowing board.");
            var vertical = ContentScroll.VerticalOffset; var horizontal = ContentScroll.HorizontalOffset;
            foreach (var delta in new[] { 120, -120, -12000, -120, 12000, 120 }) await RouteWebTileScrollAsync(first, new(20, 30), delta);
            await RouteWebTileScrollAsync(first, new(115, 25), -120, horizontal: true);
            ContentScroll.UpdateLayout();
            Check(ContentScroll.VerticalOffset == vertical && ContentScroll.HorizontalOffset == horizontal, "A tile gesture scrolled the launcher board.");
            Check(await second.InspectAsync(top) == "0", "Launcher routing affected a different tile.");
            await first.InspectAsync("document.getElementById('story').removeAttribute('data-restart-scroll')");
            Check(!await first.ScrollAsync(new(199, 95), -120), "A blank region accidentally claimed wheel input.");
            await RouteWebTileScrollAsync(first, new(199, 95), -120);
            await Dispatcher.InvokeAsync(() => ContentScroll.UpdateLayout(), System.Windows.Threading.DispatcherPriority.Background);
            Check(ContentScroll.VerticalOffset > vertical, $"A non-scrollable tile surface prevented ordinary board scrolling ({vertical} -> {ContentScroll.VerticalOffset}, dismissing={_dismissing}, exit={_exit}, dragging={_dragging}, editing={_layoutEditing}).");
            await first.InspectAsync("document.body.addEventListener('wheel',event=>event.preventDefault(),{passive:false})");
            Check(await first.ScrollAsync(new(199, 95), -120), "A browser wheel handler could not retain its own gesture.");
        }
        finally
        {
            _htmlTiles.Remove("scroll-fixture"); boardHost.RootVisual = null; ContentScroll.Content = content;
            ContentScroll.Width = width; ContentScroll.Height = height; parent.Children.Insert(index, ContentScroll);
        }

        foreach (var id in new[] { "feed", "news-board" })
        {
            var manifest = ScriptTilePackages.BundledManifest(id); var folder = Path.GetDirectoryName(ScriptTilePackages.BundledManifestPath(id))!;
            var web = ScriptTilePackages.ReadWebFolder(folder); var source = File.ReadAllText(Path.Combine(folder, "tile.js"));
            var samples = System.Text.Json.Nodes.JsonNode.Parse(TileStudioContext.Sample(manifest))!;
            samples["data"]!["feed"]!["value"]!["items"]![0]!["title"] = "A longer headline demonstrates reading the complete story inside a fixed-size tile";
            samples["data"]!["feed"]!["value"]!["items"]![0]!["summary"] = string.Concat(Enumerable.Repeat("Offline sample content for scrolling. ", 12));
            var news = TileStudioContext.Build(manifest, TileSize.Wide, 1, samples.ToJsonString(), JsonSerializer.SerializeToElement(new { }), false, DateTimeOffset.Now, preview: true);
            var (frame, error) = await first.RenderAsync(web.Html, web.Css, source, false, news);
            Check(frame != null, "A scrollable news sample failed: " + error);
            await first.ScrollAsync(new(50, 30), -120);
            var before = await first.InspectAsync(top);
            Check(before != "0", "The news sample did not expose its complete story through scrolling: " + id + "; " + await first.InspectAsync("JSON.stringify({html:document.getElementById('story').outerHTML,height:document.getElementById('story').clientHeight,scroll:document.getElementById('story').scrollHeight})"));
            await first.RenderAsync(web.Html, web.Css, source, false, news);
            Check(await first.InspectAsync(top) == before, "A periodic news update reset the reader's position: " + id);
        }
        return checks;
    }
}
