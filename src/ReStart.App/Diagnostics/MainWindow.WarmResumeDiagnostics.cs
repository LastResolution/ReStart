using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Exercises warm presentation without timer help, retained surfaces, idle workers and delayed wallpaper reads in isolated state.</summary>
    /// <param name="snapshotPath">Owned output path for fictional light/dark screenshots and aggregate timings.</param>
    /// <param name="idleSeconds">Actual hidden idle duration, from 1 to 1800 seconds, before the final timer-free reopening.</param>
    /// <returns>Asserted invariants and local latency observations; no hardware-independent timing guarantee is implied.</returns>
    internal async Task<WarmResumeSmokeResult> VerifyWarmResumeAsync(string snapshotPath, int idleSeconds = 3)
    {
        if (!_preview) throw new InvalidOperationException("Warm resume verification requires preview mode.");
        if (idleSeconds is < 1 or > 1800) throw new ArgumentOutOfRangeException(nameof(idleSeconds));
        var directory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var packages = new ScriptTilePackages(directory);
        var nativeDirectory = Path.Combine(directory, "native"); Directory.CreateDirectory(nativeDirectory);
        File.WriteAllText(Path.Combine(nativeDirectory, "manifest.json"), JsonSerializer.Serialize(new ScriptTileManifest { Title = "Resume sample", RefreshSeconds = 60 }, ScriptTileProtocol.Json));
        File.WriteAllText(Path.Combine(nativeDirectory, "tile.js"), "function render(ctx){return {text:'Ready',state:{ticks:(ctx.state.ticks||0)+1}}}");
        var native = packages.Import(Path.Combine(nativeDirectory, "manifest.json"));
        var tiles = new[] { packages.Import(ScriptTilePackages.BundledManifestPath("orbit")), packages.Import(ScriptTilePackages.BundledManifestPath("calendar")), native };
        var store = new LayoutStore(Path.Combine(directory, "layout.json"));
        store.Save(new() { Preferences = new() { Language = "en", ShowWallpaper = false, Animate = false, Columns = 8 }, Groups = [new() { Name = "Resume samples", Tiles = [.. tiles] }] });
        var fixture = new MainWindow(store, true, () => Task.FromResult(new List<AppEntry> { new("sample-editor", "Editor sample", "") }))
        { Owner = this, Width = 1280, Height = 800, Topmost = false };
        var language = L.Language; var checks = 0; var measurements = new List<object>();
        using var allowRead = new ManualResetEventSlim();
        using var enteredRead = new ManualResetEventSlim();
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        async Task Until(Func<bool> condition, string message)
        {
            var watch = Stopwatch.StartNew();
            while (!condition()) { if (watch.ElapsedMilliseconds > 8000) throw new InvalidOperationException(message); await Task.Delay(10); }
        }
        try
        {
            await fixture.InitializeAsync(); fixture._clock.Stop(); fixture.Show(); fixture.UpdateLayout();
            await fixture.UpdateScriptTilesAsync();
            await Until(() => fixture.ScriptsReady && fixture._scriptSessions.Values.All(s => !s.Rendering), "Initial resume samples did not settle.");
            foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
            {
                fixture.Document.Preferences.Theme = theme; fixture.ApplyAppearance();
                Check(fixture._htmlTiles.Values.All(view => view.RetainedPresentation == null), "An appearance change retained an image from the previous palette.");
                fixture.RenderGroups(); fixture.UpdateLayout();
                foreach (var session in fixture._scriptSessions.Values) session.NextUpdate = DateTimeOffset.MinValue;
                await fixture.UpdateScriptTilesAsync();
                var browsers = fixture._htmlTiles.ToDictionary(p => p.Key, p => p.Value);
                await Until(() => browsers.Values.All(view => HasRetainedPixels(view.RetainedPresentation)), "Visible browser surfaces did not produce retained pixels.");
                var workers = fixture._scriptWorkerPool;
                for (var cycle = 0; cycle < 6; cycle++)
                {
                    var oldTicks = fixture._scriptSessions[native.Id].State.GetProperty("ticks").GetInt32();
                    var attempts = fixture._scriptSessions.ToDictionary(p => p.Key, p => p.Value.Attempts);
                    fixture.Hide();
                    await Task.Delay(cycle % 3 == 0 ? 700 : cycle % 3 == 1 ? 10 : 80);
                    Check(fixture._scriptWorkerPool == workers, "Hiding discarded the reusable idle worker pool.");
                    Check(fixture.ScriptFrameCount == tiles.Length && fixture._scriptSessions[native.Id].State.GetProperty("ticks").GetInt32() == oldTicks,
                        "Hidden tiles changed state or lost their successful frames.");
                    if (cycle % 3 == 0)
                        Check(browsers.Values.All(view => HasRetainedPixels(view.RetainedPresentation)), "Suspension did not retain visible browser pixels for reopening.");
                    var watch = Stopwatch.StartNew();
                    fixture.Present(); var presentMs = watch.Elapsed.TotalMilliseconds;
                    fixture.UpdateLayout();
                    if (cycle == 0) fixture.SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"resume-{theme}-retained.png"));
                    Check(fixture.IsVisible && fixture.SearchBox.IsKeyboardFocused && fixture._startupWindow == null,
                        "Warm presentation lost focus or repeated cold startup.");
                    await Until(() => tiles.All(tile => fixture._scriptSessions[tile.Id].Attempts > attempts[tile.Id] && !fixture._scriptSessions[tile.Id].Rendering),
                        "Reopening waited for the stopped periodic timer instead of refreshing visible tiles.");
                    Check(browsers.All(pair => fixture._htmlTiles[pair.Key] == pair.Value && !pair.Value.ViewportPaused), "Reopening recreated or left visible browsers paused.");
                    Check(fixture._scriptSessions[native.Id].State.GetProperty("ticks").GetInt32() == oldTicks + 1, "Warm resume lost state or evaluated the same timer update twice.");
                    measurements.Add(new { theme = theme.ToString(), cycle, presentMs, tilesReadyMs = watch.Elapsed.TotalMilliseconds });
                }
                fixture.SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"resume-{theme}.png"));
            }

            var idleWorkers = fixture._scriptWorkerPool;
            var idleTicks = fixture._scriptSessions[native.Id].State.GetProperty("ticks").GetInt32();
            fixture.Hide(); await Task.Delay(TimeSpan.FromSeconds(idleSeconds));
            Check(fixture._scriptWorkerPool == idleWorkers && fixture.ScriptFrameCount == tiles.Length
                && fixture._scriptSessions[native.Id].State.GetProperty("ticks").GetInt32() == idleTicks, "Idle retention restarted workers or evaluated hidden tiles.");
            var idleWatch = Stopwatch.StartNew(); fixture.Present();
            await Until(() => fixture._scriptSessions[native.Id].State.GetProperty("ticks").GetInt32() == idleTicks + 1 && fixture._scriptSessions.Values.All(s => !s.Rendering),
                "Reopening after idle failed to refresh retained tile state.");
            measurements.Add(new { idleSeconds, tilesReadyMs = idleWatch.Elapsed.TotalMilliseconds });

            var reads = 0; var activeReads = 0; var maxReads = 0;
            var image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 90, 120, 170, 255 }, 4); image.Freeze();
            fixture.Document.Preferences.ShowWallpaper = true;
            fixture._openingWallpaperFixture = enabled =>
            {
                var active = Interlocked.Increment(ref activeReads); Interlocked.Increment(ref reads); maxReads = Math.Max(maxReads, active);
                enteredRead.Set(); allowRead.Wait(TimeSpan.FromSeconds(8)); Interlocked.Decrement(ref activeReads);
                return enabled ? new(null, default, 0, image) : DesktopWallpaperSource.Empty;
            };
            fixture.Hide(); fixture.Present();
            await Until(() => enteredRead.IsSet, "Opening did not start the background wallpaper refresh.");
            var task = fixture._openingWallpaperRefresh;
            fixture.ComposeFixtureText("Editor");
            Check(fixture.SearchBox.Text == "Editor" && fixture.SearchResults.SelectedItem is Button && !task!.IsCompleted,
                "A blocked wallpaper lookup blocked search input or navigation.");
            for (var i = 0; i < 8; i++) { fixture.Hide(); fixture.Present(); }
            Check(reads == 1, "Repeated opening flooded background wallpaper reads.");
            fixture.Document.Preferences.ShowWallpaper = false; fixture.RefreshWallpaper();
            allowRead.Set(); await task!;
            Check(maxReads == 1 && reads == 2 && fixture.Wallpaper.Source == null,
                "Coalesced wallpaper work overlapped or replaced a newer disabled preference with stale pixels.");
            return new(checks, measurements);
        }
        finally
        {
            allowRead.Set();
            if (fixture._openingWallpaperRefresh is { } pendingWallpaper) await pendingWallpaper;
            fixture._openingWallpaperFixture = null; fixture.Exit(); L.SetLanguage(language); ApplyAppearance(); Activate();
        }
    }

    private static bool HasRetainedPixels(ImageSource? source)
    {
        if (source is not BitmapSource bitmap) return false;
        var pixels = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var bytes = new byte[pixels.PixelWidth * pixels.PixelHeight * 4];
        pixels.CopyPixels(bytes, pixels.PixelWidth * 4, 0);
        // Transparent package backgrounds are valid. Require actual content,
        // rather than assuming the whole browser surface must be opaque.
        return Enumerable.Range(0, bytes.Length / 4).Count(i => bytes[i * 4 + 3] > 0) > Math.Max(32, pixels.PixelWidth * pixels.PixelHeight / 100);
    }
}

internal sealed record WarmResumeSmokeResult(int Checks, object Timings);
