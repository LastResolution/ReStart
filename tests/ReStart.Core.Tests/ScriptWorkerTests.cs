using System.Diagnostics;
using System.Runtime.Versioning;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.Core.Tests;

[SupportedOSPlatform("windows")]
public sealed class ScriptWorkerTests
{
    [Fact]
    public async Task ConcurrentWorkersMeetDeadlinesWithoutBlockingThePool()
    {
        if (!OperatingSystem.IsWindows()) return;
        var sessions = Enumerable.Range(0, 4).Select(_ => new ScriptTileSession(Path.Combine(AppContext.BaseDirectory, "ReStart.TileHost.exe"))).ToArray();
        try
        {
            var ready = await Task.WhenAll(sessions.Select(session => session.RenderAsync("function render(){return {text:'ready'};}", new { })));
            Assert.All(ready, frame => Assert.NotNull(frame));
            var watch = Stopwatch.StartNew();
            var results = await Task.WhenAll(sessions.Select(session => session.RenderAsync("function render(){return {text:String(/(a+)+$/.test('a'.repeat(10000)+'!'))};}", new { })));
            Assert.All(results, frame => Assert.Null(frame));
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"Concurrent deadlines took {watch.Elapsed.TotalMilliseconds:F0} ms.");
        }
        finally { foreach (var session in sessions) session.Dispose(); }
    }
    [Fact]
    public async Task WorkerValidationErrorsFollowRequestedLocale()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var session = new ScriptTileSession(Path.Combine(AppContext.BaseDirectory, "ReStart.TileHost.exe"));
        const string invalid = "function render(){return {progress:2};}";
        Assert.Null(await session.RenderAsync(invalid, new { locale = "ja-JP" }));
        Assert.Contains(TextCatalog.Translate(K.ErrorScriptFrameInvalid, "ja"), session.Error);
        Assert.Null(await session.RenderAsync(invalid, new { locale = "en-US" }));
        Assert.Contains(TextCatalog.Translate(K.ErrorScriptFrameInvalid, "en"), session.Error);
    }
    [Fact]
    public async Task WorkerProcessHandlesErrorsAndRestartsAfterDeadline()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var session = new ScriptTileSession(Path.Combine(AppContext.BaseDirectory, "ReStart.TileHost.exe"));
        var good = await session.RenderAsync("function render(){return {text:'ready'};}", new { });
        Assert.NotNull(good); Assert.Null(session.Error);
        var watch = Stopwatch.StartNew();
        var runaway = await session.RenderAsync("function render(){return {text:String(/(a+)+$/.test('a'.repeat(10000)+'!'))};}", new { });
        Assert.Null(runaway); Assert.NotNull(session.Error);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"Worker returned after {watch.Elapsed.TotalMilliseconds:F0} ms: {session.Error}");
        Assert.NotNull(await session.RenderAsync("function render(){return {text:'recovered'};}", new { }));
        Assert.Null(session.Error);
    }
    [Fact]
    public void PackageImportUsesInstalledIdAndRejectsTraversal()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ReStart-tiles-" + Guid.NewGuid().ToString("N"));
        try
        {
            var source = Path.Combine(directory, "source"); Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "manifest.json"), "{\"title\":\"Sample\",\"refreshSeconds\":2}");
            File.WriteAllText(Path.Combine(source, "tile.js"), "function render(){return {text:'Hello'};}");
            var packages = new ScriptTilePackages(Path.Combine(directory, "installed"));
            var tile = packages.Import(Path.Combine(source, "manifest.json"));
            Assert.True(Guid.TryParseExact(tile.ScriptId, "N", out _));
            Assert.Equal("Sample", packages.Read(tile.ScriptId).Manifest.Title);
            Assert.Throws<InvalidDataException>(() => packages.Folder("../source"));
            Assert.Throws<InvalidDataException>(() => packages.Read("C:\\secret"));
        }
        finally { Directory.Delete(directory, true); }
    }
}
