using ReStart.App.Services;

namespace ReStart.Core.Tests;

public sealed class ScriptPackageReadTests
{
    [Fact]
    public async Task RenderSnapshotsObserveExternalEditsAndReloadedAssets()
    {
        using var temporary = new TemporaryDirectory();
        var packages = new ScriptTilePackages(temporary.Path);
        var tile = packages.Import(ScriptTilePackages.BundledManifestPath("clock"));
        var directory = packages.Folder(tile.ScriptId);
        var assets = Path.Combine(directory, "assets"); Directory.CreateDirectory(assets);
        File.WriteAllText(Path.Combine(assets, "sample.json"), "{\"value\":1}");
        var first = await packages.ReadForRenderAsync(tile.ScriptId);
        File.WriteAllText(Path.Combine(directory, "tile.js"), "function render(){return {text:'Changed'}}");
        File.WriteAllText(Path.Combine(directory, "index.html"), "<main>Changed</main>");
        File.WriteAllText(Path.Combine(directory, "tile.css"), "main{color:red}");
        File.WriteAllText(Path.Combine(assets, "sample.json"), "{\"value\":2}");
        packages.InvalidateTitle(tile.ScriptId);
        var second = await packages.ReadForRenderAsync(tile.ScriptId);
        Assert.NotEqual(first.Script, second.Script);
        Assert.Equal("<main>Changed</main>", second.Html);
        Assert.Equal("main{color:red}", second.Css);
        Assert.Equal("{\"value\":1}", System.Text.Encoding.UTF8.GetString(first.Assets["/assets/sample.json"].Bytes));
        Assert.Equal("{\"value\":2}", System.Text.Encoding.UTF8.GetString(second.Assets["/assets/sample.json"].Bytes));
    }

    [Theory]
    [InlineData("tile.js", 70000)]
    [InlineData("index.html", 262145)]
    [InlineData("tile.css", 262145)]
    public async Task BackgroundRenderReadsRetainSourceBoundsAndRecoverAfterRepair(string name, int length)
    {
        using var temporary = new TemporaryDirectory();
        var packages = new ScriptTilePackages(temporary.Path);
        var tile = packages.Import(ScriptTilePackages.BundledManifestPath("clock"));
        var path = Path.Combine(packages.Folder(tile.ScriptId), name);
        var original = File.ReadAllText(path);
        File.WriteAllText(path, new string('x', length));
        await Assert.ThrowsAsync<InvalidDataException>(() => packages.ReadForRenderAsync(tile.ScriptId));
        File.Delete(path);
        await Assert.ThrowsAsync<InvalidDataException>(() => packages.ReadForRenderAsync(tile.ScriptId));
        File.WriteAllText(path, original);
        Assert.Equal("web", (await packages.ReadForRenderAsync(tile.ScriptId)).Manifest.Renderer);
        await Assert.ThrowsAsync<InvalidDataException>(() => packages.ReadForRenderAsync("../invalid"));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ReStart-RenderRead-" + Guid.NewGuid().ToString("N"));
        internal TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            var root = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            if (System.IO.Path.GetFullPath(Path).StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(Path)) Directory.Delete(Path, true);
        }
    }
}
