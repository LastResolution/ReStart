using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    internal async Task<int> VerifyTileArtworkOnlyAsync(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("Artwork verification requires preview mode.");
        var directory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "artwork-" + Guid.NewGuid().ToString("N"));
        var fixture = new MainWindow(new LayoutStore(Path.Combine(directory, "layout.json")), true, () => Task.FromResult(new List<AppEntry>()))
        { Owner = this, Width = 1280, Height = 800, Topmost = false };
        try
        {
            fixture.Present(); await fixture.InitializeAsync(); fixture._clock.Stop();
            return await VerifyTileArtworkAsync(fixture, directory);
        }
        finally { fixture.Exit(); }
    }

    private static async Task<int> VerifyTileArtworkAsync(MainWindow fixture, string directory)
    {
        var checks = 0;
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        var root = Path.Combine(directory, "owned-artwork"); Directory.CreateDirectory(root);
        var executable = Path.Combine(root, "Owned.exe"); File.WriteAllText(executable, "Not an executable; artwork fixture only.");
        File.WriteAllText(Path.ChangeExtension(executable, ".VisualElementsManifest.xml"),
            "<Application><VisualElements Square70x70Logo='small.png' Square150x150Logo='medium.png' Wide310x150Logo='wide.png' Square310x310Logo='large.png' BackgroundColor='#123456' ForegroundText='light'/></Application>");
        foreach (var (name, width, height) in new[] { ("small", 71, 71), ("medium", 150, 150), ("wide", 310, 150), ("large", 310, 310) })
            foreach (var light in new[] { false, true })
            {
                var pixels = new byte[width * height * 4];
                for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = 160; pixels[i + 1] = light ? (byte)220 : (byte)100; pixels[i + 2] = 40; pixels[i + 3] = 255; }
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4)));
                using var stream = File.Create(Path.Combine(root, name + ".scale-100_theme-" + (light ? "light" : "dark") + ".png")); encoder.Save(stream);
            }
        const string appId = "ReStart.OwnedArtwork_123!Main";
        var app = new AppEntry(appId, "Owned tile", executable, false);
        foreach (var light in new[] { false, true })
        {
            var frames = await TileArtworkLoader.ReadAsync(Enum.GetValues<TileSize>().Select(size => new TileArtworkLoader.Request(app, size, 400, light, "en-US")).ToArray(), CancellationToken.None);
            Check(frames.Count == 4, "Installed artwork was not resolved for all four declared sizes.");
            foreach (var frame in frames.Values)
            {
                Check(frame.Image.IsFrozen && frame.Background == "#123456", "An artwork bitmap was not frozen or lost its declared color.");
                var pixel = new byte[4]; new FormatConvertedBitmap(frame.Image, PixelFormats.Bgra32, null, 0).CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 4, 0);
                Check(pixel[1] == (light ? 220 : 100), "Installed artwork selected the wrong theme-qualified file.");
            }
            Check(frames[(appId, TileSize.Wide)].Image.PixelWidth > frames[(appId, TileSize.Wide)].Image.PixelHeight,
                "Wide artwork lost its aspect ratio.");
        }
        File.WriteAllText(Path.ChangeExtension(executable, ".VisualElementsManifest.xml"), "<Application><VisualElements Square150x150Logo='../outside.png'/></Application>");
        Check((await TileArtworkLoader.ReadAsync([new(app, TileSize.Medium, 150, false, "en-US")], CancellationToken.None)).Count == 0,
            "An artwork manifest escaped its installed app directory.");
        File.WriteAllText(Path.Combine(root, "fake.png"), "<svg xmlns='http://www.w3.org/2000/svg'><image href='https://example.com/fixture.png'/></svg>");
        Check(LegacyTileAssets.LoadRaster(Path.Combine(root, "fake.png"), root) == null, "Active image content disguised as a raster was accepted.");
        File.WriteAllText(Path.ChangeExtension(executable, ".VisualElementsManifest.xml"), "<Application><VisualElements Square150x150Logo='medium.png' Square70x70Logo='small.png' Wide310x150Logo='wide.png' Square310x310Logo='large.png' BackgroundColor='#123456' ForegroundText='light'/></Application>");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try { await TileArtworkLoader.ReadAsync([new(app, TileSize.Small, 70, false, "en-US")], cancelled.Token); Check(false, "Cancelled artwork reads completed."); }
            catch (OperationCanceledException) { checks++; }
        }
        var reads = 0;
        fixture._artworkFixture = async (requests, token) =>
        {
            reads++;
            return await TileArtworkLoader.ReadAsync(requests.Select(r => r with { App = app }).ToArray(), token);
        };
        fixture.Document.Preferences.UseAppTileImages = true;
        fixture.Document.Preferences.LegacyUwpLiveTiles = false;
        fixture.Document.Preferences.Language = "en";
        fixture.ResetToPinnedView();
        var tiles = Enum.GetValues<TileSize>().Select(size => new Tile { AppId = appId, Target = executable, Label = "Owned tile", Size = size }).ToList();
        foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
            foreach (var scale in new[] { .7, 1, 1.5 })
            {
                fixture.Document.Preferences.Theme = theme; fixture.Document.Preferences.TileScale = scale;
                fixture.Document.Groups = [new() { Name = "App tile artwork", Tiles = tiles }];
                fixture.ApplyAppearance(); fixture.RenderGroups(); await fixture._artworkReadTask!; fixture.UpdateLayout();
                foreach (var view in fixture._artworkViews)
                {
                    Check(view.Current != null && view.Surface.Children.OfType<Image>().Single().Source == view.Current.Image,
                        "Declared artwork did not replace its icon at a supported size, scale and theme.");
                    var bounds = view.Surface.TransformToAncestor(view.Button).TransformBounds(new Rect(view.Surface.RenderSize));
                    Check(bounds.Width <= view.Button.ActualWidth + 1 && bounds.Height <= view.Button.ActualHeight + 1
                        && bounds.Left >= -1 && bounds.Top >= -1, "An artwork image extends beyond its tile.");
                }
                var sameReads = reads; fixture.RenderGroups(); await fixture._artworkReadTask!;
                Check(reads == sameReads, "Rearranging unchanged pins unnecessarily reloaded their artwork.");
                SaveFixtureElement(fixture.Root, Path.Combine(directory, "app-artwork-" + theme + "-" + scale.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ".png"));
            }
        fixture.Document.Preferences.UseAppTileImages = false; fixture.RenderGroups();
        Check(fixture._artworkFrames.Count == 0 && fixture._artworkViews.All(v => v.Current == null && v.Surface.Children.Contains(v.Icon)),
            "Disabling app artwork did not restore the existing icons.");
        var offReads = reads; fixture.RenderGroups(); Check(reads == offReads, "Disabled artwork still read app metadata.");
        fixture.Document.Preferences.UseAppTileImages = true;
        fixture._artworkHighContrastFixture = true; fixture.RenderGroups();
        Check(fixture._artworkFrames.Count == 0 && fixture._artworkViews.All(v => v.Current == null) && reads == offReads,
            "High contrast used app-provided backgrounds or read unnecessary artwork.");
        fixture._artworkHighContrastFixture = null;
        fixture.Document.Groups = [new() { Tiles = [new() { Label = "Owned folder", Children = tiles }] }];
        fixture.RenderGroups(); await fixture._artworkReadTask!; fixture.UpdateLayout();
        Check(fixture._artworkViews.Count == 4 && fixture._artworkViews.All(v => v.Tile.Size == TileSize.Small && v.Current != null),
            "Closed folder previews failed to use the small artwork layout.");
        Check(tiles.Select(t => t.Size).SequenceEqual(Enum.GetValues<TileSize>()), "Folder artwork mutated persistent child sizes.");

        fixture.Document.Groups = [new() { Tiles = tiles }];
        fixture.Document.Preferences.LegacyUwpLiveTiles = true;
        fixture._legacyTileFixture = (_, _) => Task.FromResult(new LegacyNotificationStore.Snapshot(true,
            new Dictionary<string, LegacyNotificationStore.Frame> { [appId] = new(LegacyTileContent.Read("<tile><visual><binding template='TileSmall'><text>7</text></binding><binding template='TileMedium'><text>Owned update</text></binding><binding template='TileWide'><text>Owned update</text></binding><binding template='TileLarge'><text>Owned update</text></binding></visual></tile>"), "") }));
        fixture._legacyRequested = ""; fixture._legacyNextPoll = 0;
        fixture.RenderGroups(); await fixture._artworkReadTask!;
        var deadline = Environment.TickCount64 + 3000;
        while (!fixture._legacySnapshot.Apps.ContainsKey(appId) && Environment.TickCount64 < deadline) await Task.Delay(20);
        fixture.ApplyLegacyFrames();
        Check(fixture._legacyViews.Count == 4 && fixture._legacyViews.All(v => v.View.HasTileContent && v.Icon.Visibility == Visibility.Collapsed),
            "Static app artwork covered a live Windows tile update.");
        fixture.Document.Preferences.LegacyUwpLiveTiles = false; fixture.RefreshLegacyTiles();
        Check(fixture._legacyViews.All(v => !v.View.HasTileContent && v.Icon.Visibility == Visibility.Visible), "Missing live data did not restore static artwork.");

        var pending = new TaskCompletionSource<IReadOnlyDictionary<(string App, TileSize Size), TileArtworkLoader.Frame>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture._artworkFixture = (_, _) => pending.Task;
        fixture._artworkRequested = ""; fixture.RenderGroups(); var oldRead = fixture._artworkReadTask!;
        fixture.Document.Preferences.UseAppTileImages = false; fixture.RenderGroups();
        pending.SetResult(new Dictionary<(string, TileSize), TileArtworkLoader.Frame>());
        await oldRead;
        Check(fixture._artworkFrames.Count == 0 && fixture._artworkViews.All(v => v.Current == null), "A stale asynchronous read undid the disabled preference.");
        fixture._artworkFixture = null; fixture._legacyTileFixture = null; fixture.StopLegacyTiles();
        fixture.Document.Preferences.UseAppTileImages = true;
        return checks;
    }
}
