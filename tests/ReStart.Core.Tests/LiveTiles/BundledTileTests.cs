using System.Runtime.Versioning;
using System.Text.Json;
using ReStart.App.Services;
using ReStart.Core;
using ReStart.TileHost;

namespace ReStart.Core.Tests;

[Collection("JavaScript runtime")]
public sealed class BundledTileTests
{
    [Fact]
    public void DefaultPackageNamesFollowLanguageAndExplicitCustomNamesSurvive()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ReStart-titles-" + Guid.NewGuid().ToString("N"));
        try
        {
            var packages = new ScriptTilePackages(directory);
            var tile = packages.Import(ScriptTilePackages.BundledManifestPath("cpu"));
            var manifest = packages.Read(tile.ScriptId).Manifest;
            foreach (var language in TextCatalog.SupportedLanguages) Assert.Equal(manifest.LocalizedTitles[language], packages.DisplayName(tile, language));
            tile.UseLocalizedName = false; tile.Label = "My processor";
            foreach (var language in TextCatalog.SupportedLanguages) Assert.Equal("My processor", packages.DisplayName(tile, language));
            tile.UseLocalizedName = null; tile.Label = manifest.Title;
            Assert.Equal(manifest.LocalizedTitles["ja"], packages.DisplayName(tile, "ja"));
            tile.UseLocalizedName = null; tile.Label = "Personal label";
            Assert.Equal("Personal label", packages.DisplayName(tile, "de")); Assert.False(tile.UseLocalizedName);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void LibraryDiscoversPackagesWithoutExecutingOrImportingThem()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ReStart-library-" + Guid.NewGuid().ToString("N"));
        var packages = new ScriptTilePackages(directory);
        try
        {
            Assert.Empty(packages.DiscoverLibrary());
            var valid = Path.Combine(packages.LibraryRoot, "valid"); Directory.CreateDirectory(valid);
            File.WriteAllText(Path.Combine(valid, "manifest.json"), "{\"title\":\"Local example\"}");
            File.WriteAllText(Path.Combine(valid, "tile.js"), "throw new Error('must not execute during discovery');");
            var broken = Path.Combine(packages.LibraryRoot, "broken"); Directory.CreateDirectory(broken);
            File.WriteAllText(Path.Combine(broken, "manifest.json"), "{");
            File.WriteAllText(Path.Combine(broken, "tile.js"), "");
            var entries = packages.DiscoverLibrary();
            Assert.Equal(2, entries.Count);
            Assert.Equal("Local example", entries.Single(e => e.Error == null).Manifest!.Title);
            Assert.Null(entries.Single(e => e.Error != null).Manifest);
            Assert.False(Directory.Exists(packages.Root));
            var tile = packages.Import(entries.Single(e => e.Error == null).ManifestPath);
            Assert.NotEmpty(tile.ScriptId); Assert.Single(Directory.EnumerateDirectories(packages.Root));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void ImportCopiesOnlySourceAndManifestWithoutHostConnections()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ReStart-import-" + Guid.NewGuid().ToString("N"));
        try
        {
            var input = Path.Combine(directory, "input"); Directory.CreateDirectory(input);
            File.WriteAllText(Path.Combine(input, "manifest.json"), "{\"title\":\"Data\",\"capabilities\":[\"data.sources\"],\"dataSources\":{\"service\":{}}}");
            File.WriteAllText(Path.Combine(input, "tile.js"), "function render(){return {text:'Ready'};}");
            File.WriteAllBytes(Path.Combine(input, "data-bindings.dat"), [1, 2, 3]);
            File.WriteAllText(Path.Combine(input, "host-bindings.json"), "{}");
            var packages = new ScriptTilePackages(Path.Combine(directory, "installed")); var tile = packages.Import(Path.Combine(input, "manifest.json"));
            Assert.Equal(new[] { "manifest.json", "tile.js" }, Directory.GetFiles(packages.Folder(tile.ScriptId)).Select(Path.GetFileName).Order());
            Assert.False(File.Exists(packages.DataBindingPath(tile.ScriptId)));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void EveryPackageRendersAllLanguagesAndSizesWithAndWithoutHostData()
    {
        Assert.Equal(19, ScriptTilePackages.BundledIds.Count);
        foreach (var id in ScriptTilePackages.BundledIds)
        {
            var manifest = ScriptTilePackages.BundledManifest(id);
            Assert.Equal(TextCatalog.SupportedLanguages.Order(), manifest.LocalizedTitles.Keys.Order());
            var source = File.ReadAllText(Path.Combine(Path.GetDirectoryName(ScriptTilePackages.BundledManifestPath(id))!, "tile.js"));
            foreach (var language in TextCatalog.SupportedLanguages)
                foreach (var size in new[] { "small", "medium", "wide", "large" })
                    foreach (var hostData in new[] { true, false })
                    {
                        var frame = JsTileRunner.Render(new(source, Context(manifest, language, size, hostData)));
                        frame.Validate(); Assert.NotEmpty(frame.Text);
                        var nativeCompatible = id is not ("weather-scene" or "system-dashboard" or "news-board" or "canvas-garden");
                        if (nativeCompatible) Assert.NotEmpty(frame.BackgroundEnd);
                        var expectedLayout = id == "slideshow" && size != "small" ? "caption" : id == "weather" && !hostData ? "centered"
                            : id is "feed" or "json-monitor" ? size is "small" or "medium" ? "centered" : "overlay"
                            : size switch { "small" or "medium" => "centered", "large" => "stacked", _ => "split" };
                        if (nativeCompatible) Assert.Equal(expectedLayout, frame.Layout);
                        if (nativeCompatible && size == "small") { Assert.Empty(frame.Detail); Assert.Empty(frame.Visual); }
                        var click = JsTileRunner.Render(new(source, Context(manifest, language, size, hostData, frame.State, true)));
                        click.Validate();
                        if (id == "focus") Assert.Equal(100000, click.State.GetProperty("pausedAt").GetInt64());
                        if (id == "clock") Assert.True(click.State.GetProperty("twelve").GetBoolean());
                        if (id == "slideshow") Assert.Equal(hostData ? "current" : "", frame.Image);
                    }
        }
    }

    private static JsonElement Context(ScriptTileManifest manifest, string language, string size, bool hostData, JsonElement? state = null, bool click = false) => JsonSerializer.SerializeToElement(new
    {
        now = "2026-10-18T09:41:00+09:00",
        language,
        locale = "en-US",
        size,
        settings = manifest.Settings,
        time = new { unixMs = 1792284060000L, monotonicMs = 100000, offsetMinutes = 540 },
        calendar = new { shortDate = "10/18/2026", weekday = "Sunday", month = "October", day = 18 },
        state = state ?? JsonSerializer.SerializeToElement(new { }),
        @event = click ? new { type = "click" } : null,
        data = hostData ? new
        {
            weather = new { status = "ready", value = new { temperature_2m = 18, weather_code = 2, wind_speed_10m = 12 }, stale = false },
            feed = new { status = "ready", value = new { title = "Sample news", items = new[] { new { title = "A headline", summary = "Summary" } } }, stale = false },
            service = new { status = "ready", value = new { text = "Connected", detail = "Sample service", progress = .75 }, stale = false }
        } : null,
        system = hostData ? new SystemMetricsSnapshot(DateTimeOffset.Now, 42.5, 16, 32UL << 30, 12UL << 30, 62.5, 91800,
            [new("C:\\", 512L << 30, 214L << 30, 58.2)], 425984, 65536, 74, false) : null,
        slideshow = new { available = hostData, count = hostData ? 2 : 0, index = 0 }
    }, ScriptTileProtocol.Json);

    [Theory]
    [InlineData("function render(){return {visual:'html'};}")]
    [InlineData("function render(){return {series:[-1]};}")]
    [InlineData("function render(){return {series:Array(33).fill(.5)};}")]
    [InlineData("function render(){return {image:'file:///private.png'};}")]
    [InlineData("function render(){return {accent:'red'};}")]
    [InlineData("function render(){return {backgroundEnd:null};}")]
    [InlineData("function render(){return {layout:'html'};}")]
    [InlineData("function render(){return {layout:null};}")]
    public void RejectsUnboundedOrExecutableDrawingData(string source) => Assert.ThrowsAny<Exception>(() => JsTileRunner.Render(new(source, JsonSerializer.SerializeToElement(new { }))));

    [Fact]
    public void RejectsUnknownAndDuplicateCapabilities()
    {
        Assert.Throws<InvalidDataException>(() => new ScriptTileManifest { Capabilities = ["files.read"] }.Validate());
        Assert.Throws<InvalidDataException>(() => new ScriptTileManifest { Capabilities = ["system.metrics", "system.metrics"] }.Validate());
        new ScriptTileManifest { Capabilities = ["system.metrics", "images.slideshow"] }.Validate();
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task PoolRendersMoreThanEightTilesAndDisposesWithQueuedRequests()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var pool = new ScriptTileWorkerPool(Path.Combine(AppContext.BaseDirectory, "ReStart.TileHost.exe"));
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(i => pool.RenderAsync("function render(ctx){ return {text:String(ctx.index),state:{index:ctx.index}}; }", new { index = i })));
        Assert.Equal(Enumerable.Range(0, 16).Select(i => i.ToString()), results.Select(r => r.Frame?.Text));
        Assert.All(results, result => Assert.Null(result.Error));
        var queued = Enumerable.Range(0, 16).Select(i => pool.RenderAsync("function render(){return {text:'done'};}", new { index = i })).ToArray();
        pool.Dispose(); await Task.WhenAll(queued).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null((await pool.RenderAsync("", new { })).Frame);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task SamplesHostMemoryAndCpuAndPausesWithoutExposingProcessData()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var sampler = new SystemMetricsSampler();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (sampler.Latest?.CpuPercent == null && DateTime.UtcNow < deadline) await Task.Delay(100);
        var snapshot = Assert.IsType<SystemMetricsSnapshot>(sampler.Latest);
        Assert.InRange(snapshot.CpuPercent!.Value, 0, 100); Assert.True(snapshot.TotalMemoryBytes > 0);
        Assert.InRange(snapshot.MemoryPercent!.Value, 0, 100); Assert.True(snapshot.LogicalProcessors > 0);
        sampler.SetEnabled(false); await Task.Delay(100); Assert.Null(sampler.Latest);
    }
}
