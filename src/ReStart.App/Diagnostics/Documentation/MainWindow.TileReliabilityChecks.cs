using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    internal int ReliabilitySoakSeconds { get; set; } = 3;
    /// <summary>Exercises actual isolated browser crashes and viewport scheduling with 50/100 fictional tiles.</summary>
    /// <param name="directory">Owned report directory; records aggregate timings, memory and counts only.</param>
    /// <returns>Number of asserted recovery, state and visibility invariants; no production configuration or processes are touched.</returns>
    internal async Task<int> VerifyTileReliabilityAsync(string directory)
    {
        if (!_preview) throw new InvalidOperationException("Reliability checks require an isolated preview.");
        Directory.CreateDirectory(directory); _clock.Stop(); _ready = true;
        var original = Document; var preparing = _startupPreparing; var rendering = _documentationHtmlRendering;
        var width = Root.Width; var height = Root.Height; var checks = 0; var stage = "load";
        var measurements = new List<object>();
        void Check(bool value, string message) { Require(value, message); checks++; }
        async Task Until(Func<bool> condition, string message, int timeout = 20000)
        {
            var deadline = Environment.TickCount64 + timeout;
            while (!condition())
            {
                if (Environment.TickCount64 > deadline) throw new InvalidOperationException(message);
                await UpdateScriptTilesAsync(); await Task.Delay(30);
            }
        }
        Content = null; Root.Width = 1280; Root.Height = 800; _startupPreparing = _documentationHtmlRendering = true;
        using var host = HiddenRenderHost(Root, 1280, 800);
        try
        {
            var sample = _scriptPackages.Import(ScriptTilePackages.BundledManifestPath("clock"));
            File.WriteAllText(Path.Combine(_scriptPackages.Folder(sample.ScriptId), "tile.js"), "function render(ctx){return {text:'Sample',state:{ticks:(ctx.state.ticks||0)+1}}}");
            _verifyViewportScheduling = true;
            foreach (var count in new[] { 50, 100 })
            {
                PauseScripts(); RetireHtmlTiles(all: true); _liveSessions.Dispose(); _liveSessions = new();
                Document = new() { Preferences = new() { Language = "en", ShowWallpaper = false, Animate = false }, Groups = [new() { Name = "Load sample", Tiles = Enumerable.Range(0, count).Select(i => new Tile { Widget = "script", ScriptId = sample.ScriptId, Size = TileSize.Wide, Row = i * 2, Column = 0 }).ToList() }] };
                ApplyAppearance(); RenderGroups(); Root.UpdateLayout(); ContentScroll.ScrollToTop(); Root.UpdateLayout();
                var started = Stopwatch.StartNew(); await UpdateScriptTilesAsync();
                await Until(() => ScriptsReady, "Visible load-test tiles did not warm up.");
                var warmup = started.ElapsedMilliseconds; var scrollTimer = Stopwatch.StartNew();
                var active = _scriptViews.Where(v => TileInViewport(v.Button)).Select(v => v.Tile.Id).ToHashSet();
                Check(active.Count is > 0 and < 20 && active.Count < count, "Viewport filtering initialized the whole board.");
                Check(_htmlTiles.Count(v => v.Value.BrowserReady) <= active.Count, "Offscreen browsers initialized unnecessarily.");
                var first = active.First(); var oldAttempts = _scriptSessions[first].Attempts;
                ContentScroll.ScrollToBottom(); Root.UpdateLayout(); await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded); Root.UpdateLayout();
                await UpdateScriptTilesAsync(); await Until(() => ScriptsReady, "Scrolling did not resume visible tiles.");
                var scrollMs = scrollTimer.ElapsedMilliseconds;
                Check(!_scriptViews.Where(v => TileInViewport(v.Button)).Any(v => v.Tile.Id == first), "The scroll fixture did not leave its original viewport.");
                Check(_htmlTiles[first].ViewportPaused && _scriptSessions[first].Attempts == oldAttempts, "Scrolling out did not suspend the old tile.");
                await Task.Delay(3000); // Exclude cold browser initialization from steady-state CPU sampling.
                var statsBefore = ResourcesUsed(); var clock = Stopwatch.StartNew();
                var duration = count == 100 ? ReliabilitySoakSeconds : 3;
                while (clock.Elapsed.TotalSeconds < duration) { await UpdateScriptTilesAsync(); await Task.Delay(250); }
                var statsAfter = ResourcesUsed();
                Check(_scriptSessions[first].Attempts == oldAttempts, "An offscreen tile resumed during the soak period.");
                measurements.Add(new
                {
                    tiles = count,
                    active = active.Count,
                    initialized = _htmlTiles.Count(v => v.Value.BrowserReady),
                    warmupMs = warmup,
                    scrollReadyMs = scrollMs,
                    sampleSeconds = clock.Elapsed.TotalSeconds,
                    cpuPercent = Math.Max(0, statsAfter.Cpu - statsBefore.Cpu) / clock.Elapsed.TotalMilliseconds / Environment.ProcessorCount * 100,
                    privateBytes = statsAfter.Memory,
                    privateBytesGrowth = statsAfter.Memory - statsBefore.Memory
                });
                Document.Preferences.PauseOffscreenLiveTiles = false;
                Check(_scriptViews.All(v => TileInViewport(v.Button)), "The offscreen-pause setting cannot be disabled.");
                Document.Preferences.PauseOffscreenLiveTiles = true;
            }

            PauseScripts(); RetireHtmlTiles(all: true); _liveSessions.Dispose(); _liveSessions = new(); _verifyViewportScheduling = false;
            File.WriteAllText(Path.Combine(_scriptPackages.Folder(sample.ScriptId), "tile.js"), "async function render(ctx){if(ctx.event){globalThis.reliabilityWaiting=true;await new Promise(r=>setTimeout(r,750));}return {text:'Sample',state:{ticks:(ctx.state.ticks||0)+1,clicks:(ctx.state.clicks||0)+(ctx.event?1:0)}}}");
            Document = new() { Preferences = new() { Language = "en", ShowWallpaper = false, Animate = false }, Groups = [new() { Name = "Recovery sample", Tiles = [sample] }] };
            sample.Size = TileSize.Wide; ApplyAppearance(); RenderGroups(); Root.UpdateLayout(); await UpdateScriptTilesAsync();
            await Until(() => ScriptsReady, "Crash fixture did not initialize.");
            var originalLayout = JsonSerializer.Serialize(Document, LayoutStore.JsonOptions);
            var old = _htmlTiles[sample.Id]; var ticks = _scriptSessions[sample.Id].State.GetProperty("ticks").GetInt32();
            stage = "renderer crash";
            _scriptSessions[sample.Id].Inputs.Enqueue(new("click", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            _scriptSessions[sample.Id].NextUpdate = DateTimeOffset.MinValue;
            var inFlight = UpdateScriptTilesAsync();
            for (var i = 0; i < 100 && await old.InspectAsync("globalThis.reliabilityWaiting===true") != "true"; i++) await Task.Delay(10);
            await old.CrashRendererForVerificationAsync();
            await inFlight;
            await Until(() => _htmlTiles[sample.Id] != old && ScriptsReady, "Renderer crash did not recover automatically.");
            Check(_scriptSessions[sample.Id].State.GetProperty("ticks").GetInt32() > ticks, "Renderer recovery lost host-managed state.");
            Check(_scriptSessions[sample.Id].State.GetProperty("clicks").GetInt32() == 1 && _scriptSessions[sample.Id].Inputs.Count == 0, "Renderer recovery lost the queued click or acknowledged it twice.");
            Check(JsonSerializer.Serialize(Document, LayoutStore.JsonOptions) == originalLayout, "Renderer recovery changed saved configuration.");
            old = _htmlTiles[sample.Id]; var browserId = old.BrowserProcessId;
            stage = "browser crash";
            Check(browserId != 0 && old.OwnedProcessIds.Contains((int)browserId), "The browser crash target is outside the isolated environment.");
            using (var browser = Process.GetProcessById((int)browserId)) browser.Kill();
            await Until(() => _htmlTiles[sample.Id] != old && ScriptsReady, "Browser process exit did not recreate its controls and environment.");
            Check(JsonSerializer.Serialize(Document, LayoutStore.JsonOptions) == originalLayout, "Browser recovery changed saved configuration.");
            var budget = _liveSessions.Budget(sample.Id); budget.Fail(Environment.TickCount64, true);
            Check(budget.Paused, "Repeated browser failures did not exhaust their retry budget.");
            var paused = _htmlTiles[sample.Id]; await UpdateScriptTilesAsync();
            Check(paused.ViewportPaused, "A repeatedly failing tile was not suspended.");
            _liveSessions.Recovery.Remove(sample.Id); await UpdateScriptTilesAsync();
            Check(!paused.ViewportPaused, "An explicit retry did not resume presentation.");
            Document.Preferences.AutoRecoverLiveTiles = false; old = _htmlTiles[sample.Id];
            await old.CrashRendererForVerificationAsync(); await Task.Delay(100); await UpdateScriptTilesAsync();
            Check(_liveSessions.Budget(sample.Id).Paused && _htmlTiles[sample.Id] == old, "Disabling automatic recovery still recreated a crashed tile.");
            Check(!IsVisible, "The reliability fixture showed a window.");
            File.WriteAllText(Path.Combine(directory, "reliability.json"), JsonSerializer.Serialize(new { success = true, checks, rendererCrash = true, browserCrash = true, measurements }, new JsonSerializerOptions { WriteIndented = true }));
            return checks;
        }
        catch (Exception ex) { Console.Error.WriteLine(stage + ": " + ex); throw; }
        finally
        {
            PauseScripts(); RetireHtmlTiles(all: true); _liveSessions.Dispose(); _liveSessions = new();
            _verifyViewportScheduling = false; _startupPreparing = preparing; _documentationHtmlRendering = rendering;
            host.RootVisual = null; Content = Root; Document = original; Root.Width = width; Root.Height = height;
            ApplyAppearance(); RenderGroups();
        }

        (double Cpu, long Memory) ResourcesUsed()
        {
            var ids = _htmlTiles.Values.SelectMany(v => v.OwnedProcessIds).Append(Environment.ProcessId).Distinct(); double cpu = 0; long memory = 0;
            foreach (var id in ids)
                try { using var process = Process.GetProcessById(id); cpu += process.TotalProcessorTime.TotalMilliseconds; memory += process.PrivateMemorySize64; }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
            return (cpu, memory);
        }
    }
}
