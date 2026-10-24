using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Checks the browser boundary and renders the authoring workspace without showing a native window or reading a user catalog.</summary>
    internal async Task RenderStudioDocumentationAsync(string directory)
    {
        if (!_preview) throw new InvalidOperationException("Studio documentation requires preview mode.");
        Document.Preferences.Language = "en"; Document.Preferences.Theme = ReStart.Core.ThemeMode.Dark; Document.Preferences.Animate = false;
        _ready = true; ApplyAppearance();
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        using (var browser = new HtmlTileView(_scriptPackages.BrowserDataRoot, Guid.NewGuid().ToString("N"), offscreen: true) { Width = 200, Height = 200 })
        {
            using var sourceHost = HiddenRenderHost(browser, 200, 200);
            try
            {
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                browser.Measure(new(200, 200)); browser.Arrange(new Rect(0, 0, 200, 200)); browser.UpdateLayout();
                const string html = "<!doctype html><html><head><title>Browser fixture</title><link rel='stylesheet' href='/tile.css'></head><body><svg viewBox='0 0 200 200'><circle cx='100' cy='100' r='80' fill='#0078d7'/><text x='100' y='108' text-anchor='middle' fill='white'>ReStart</text></svg><script src='/tile.js'></script></body></html>";
                const string css = "html,body{margin:0;width:100%;height:100%;overflow:hidden;background:transparent}svg{width:100%;height:100%}";
                const string source = """
                    async function render(ctx) {
                      let denied=0; try { denied=(await fetch('/data-bindings.dat')).status; } catch {}
                      let evalBlocked=false, constructorBlocked=false, fileBlocked=false, networkBlocked=false;
                      try { eval('1'); } catch { evalBlocked=true; }
                      try { ({}).constructor.constructor('return 1')(); } catch { constructorBlocked=true; }
                      try { await fetch('file:///ReStart-boundary-fixture.txt'); } catch { fileBlocked=true; }
                      try { await fetch('https://restart-boundary.invalid/'); } catch { networkBlocked=true; }
                      return {text:'Browser fixture',state:{count:(ctx.state.count||0)+1,denied,evalBlocked,constructorBlocked,fileBlocked,networkBlocked,
                        nodeAbsent:typeof require==='undefined' && typeof process==='undefined' && typeof Buffer==='undefined'}};
                    }
                    """;
                var (frame, error) = await browser.RenderAsync(html, css, source, false, new { state = new { count = 0 }, size = "large", bounds = new { width = 200, height = 200, scale = 1 } });
                Check(frame != null, "Web tile rendering failed: " + error);
                Check(frame!.State.GetProperty("denied").GetInt32() == 404, "The browser exposed a host binding file.");
                Check(frame.State.GetProperty("count").GetInt32() == 1, "Browser state did not round-trip.");
                foreach (var key in new[] { "evalBlocked", "constructorBlocked", "fileBlocked", "networkBlocked", "nodeAbsent" })
                    Check(frame.State.GetProperty(key).GetBoolean(), "The web tile boundary failed: " + key);
                Check(browser.HostObjectsDisabled, "Web tiles may access native host objects.");
                Check(!IsVisible, "A documentation browser unexpectedly opened a window.");
                File.WriteAllText(System.IO.Path.Combine(directory, "browser.json"), await browser.InspectAsync("JSON.stringify({width:innerWidth,height:innerHeight,visibility:document.visibilityState,body:document.body.innerHTML})").WaitAsync(TimeSpan.FromSeconds(5)));
                await Task.Delay(100);
                using var image = File.Create(System.IO.Path.Combine(directory, "html-circle.png")); await browser.CaptureAsync(image).WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally { sourceHost.RootVisual = null; }
        }
        var studio = new TileStudioWindow(this, offscreen: true);
        try
        {
            var content = (FrameworkElement)studio.Content;
            using var sourceHost = HiddenRenderHost(content, (int)studio.Width, (int)studio.Height);
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            content.Measure(new(studio.Width, studio.Height)); content.Arrange(new Rect(0, 0, studio.Width, studio.Height)); content.UpdateLayout();
            await studio.PreviewAsync();
            Check(studio.PreviewSucceeded && studio.PreviewCount == 4, "Tile Studio did not render all four sizes: " + studio.PreviewError);
            checks += await VerifyStudioEditingAsync(studio, directory);
            await studio.FreezeForDocumentationAsync();
            SaveFixtureElement(studio, System.IO.Path.Combine(directory, "tile-studio-dark.png"));
            Check(!studio.IsVisible, "Tile Studio unexpectedly opened a window.");
        }
        finally { studio.Close(); }
        checks += VerifyUpdateNotices(directory);
        checks += VerifyPresentationAssets(directory);
        checks += VerifyDropProjection(directory);
        checks += VerifyDragGhost(directory);
        checks += await VerifyWebSamplesAsync(directory);
        checks += await VerifyWebScrollingAsync();
        checks += await VerifyFolderPreviewsAsync(directory);
        checks += await VerifyWebTileRecoveryAsync(directory);
        checks += await VerifyTileReliabilityAsync(directory);
        checks += await VerifyWebLayoutMatrixAsync(directory);
        await RenderTileGalleryAsync(directory);
        _documentationHtmlRendering = true; _startupPreparing = true; Root.Width = 1920; Root.Height = 900;
        _documentationNow = new DateTimeOffset(2026, 10, 18, 9, 41, 0, TimeSpan.Zero);
        Content = null;
        using var galleryHost = HiddenRenderHost(Root, 1920, 900);
        try { await RenderCreativeGalleryAsync(directory); checks++; }
        finally { galleryHost.RootVisual = null; Content = Root; _startupPreparing = false; _documentationNow = null; _documentationSampleConnections = false; }
        File.WriteAllText(System.IO.Path.Combine(directory, "verification.json"), JsonSerializer.Serialize(new { success = true, checks }));
    }

    private static HwndSource HiddenRenderHost(System.Windows.Media.Visual content, int width, int height)
    {
        var source = new HwndSource(new HwndSourceParameters("ReStart offscreen renderer")
        { Width = width, Height = height, WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x08000080 });
        source.RootVisual = content; return source;
    }
}
