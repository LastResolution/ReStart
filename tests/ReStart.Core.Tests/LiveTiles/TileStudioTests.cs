using System.Text.Json;
using ReStart.App.Services;
using ReStart.Core;
using ReStart.TileHost;

namespace ReStart.Core.Tests;

[Collection("JavaScript runtime")]
public sealed class TileStudioTests
{
    [Fact]
    public void LibrarySaveUpdatesOneDraftAndInstalledRepairKeepsBindings()
    {
        using var temporary = new TemporaryDirectory();
        var packages = new ScriptTilePackages(temporary.Path);
        const string manifest = "{\"title\":\"Draft\"}";
        const string first = "function render(){return {text:'First'};}";
        var path = packages.SaveLibrary(manifest, first);
        Assert.Single(packages.DiscoverLibrary());
        var id = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path));
        Assert.Equal(path, packages.SaveLibrary(manifest, first.Replace("First", "Second"), id));
        Assert.Single(packages.DiscoverLibrary());
        var tile = packages.Import(path);
        var binding = packages.SlideshowBindingPath(tile.ScriptId); File.WriteAllText(binding, "kept");
        File.Delete(System.IO.Path.Combine(packages.Folder(tile.ScriptId), "tile.js"));
        Assert.Throws<InvalidDataException>(() => packages.Read(tile.ScriptId));
        packages.SaveInstalled(tile.ScriptId, manifest, first);
        Assert.Equal(first, packages.Read(tile.ScriptId).Source);
        Assert.Equal("kept", File.ReadAllText(binding));
    }

    [Fact]
    public void InvalidOrOversizedDraftCannotReplaceGoodFiles()
    {
        using var temporary = new TemporaryDirectory(); var packages = new ScriptTilePackages(temporary.Path);
        var path = packages.SaveLibrary("{\"title\":\"Good\"}", "function render(){return {text:'OK'};}");
        var tile = packages.Import(path); var original = packages.Read(tile.ScriptId);
        Assert.Throws<InvalidDataException>(() => packages.SaveInstalled(tile.ScriptId, "{\"version\":99}", "changed"));
        Assert.Throws<InvalidDataException>(() => packages.SaveInstalled(tile.ScriptId, "{}", new string('\u20AC', 30000)));
        Assert.Throws<InvalidDataException>(() => packages.SaveLibrary("{}", "", "../invalid"));
        Assert.Equal(original.Source, packages.Read(tile.ScriptId).Source);
        if (OperatingSystem.IsWindows())
        {
            using var locked = File.Open(System.IO.Path.Combine(packages.Folder(tile.ScriptId), "manifest.json"), FileMode.Open, FileAccess.Read, FileShare.Read);
            var error = Record.Exception(() => packages.SaveInstalled(tile.ScriptId, "{\"title\":\"Changed\"}", "changed"));
            Assert.True(error is IOException or UnauthorizedAccessException);
            Assert.Equal(original.Source, packages.Read(tile.ScriptId).Source);
        }
    }

    [Fact]
    public void PreviewContextRendersEverySampleAtEverySizeWithoutConnections()
    {
        foreach (var id in ScriptTilePackages.BundledIds)
        {
            var manifest = ScriptTilePackages.BundledManifest(id);
            var source = File.ReadAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ScriptTilePackages.BundledManifestPath(id))!, "tile.js"));
            foreach (var size in Enum.GetValues<TileSize>())
            {
                var context = TileStudioContext.Build(manifest, size, 1, TileStudioContext.Sample(manifest), JsonSerializer.SerializeToElement(new { }), false, DateTimeOffset.Now);
                var (w, h) = TileLayout.Dimensions(size);
                Assert.Equal(w * 52 - 4, context.GetProperty("bounds").GetProperty("width").GetDouble());
                Assert.Equal(h * 52 - 4, context.GetProperty("bounds").GetProperty("height").GetDouble());
                JsTileRunner.Render(new(source, context)).Validate();
                if (!manifest.Capabilities.Contains("system.metrics")) Assert.Equal(JsonValueKind.Null, context.GetProperty("system").ValueKind);
            }
        }
    }

    [Fact]
    public void WebImportCopiesExplicitAssetsAndDoesNotAdoptBindingsOrNetworkPermission()
    {
        using var temporary = new TemporaryDirectory();
        var packages = new ScriptTilePackages(temporary.Path);
        var path = packages.SaveLibrary("{\"version\":2,\"renderer\":\"web\",\"title\":\"Web\",\"capabilities\":[\"web.network\"]}",
            "function render(){return {text:'Ready'};}", html: "<script src='/tile.js'></script>", css: "body{margin:0}");
        var input = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(Path.Combine(input, "assets", "nested"));
        File.WriteAllText(Path.Combine(input, "assets", "nested", "shape.svg"), "<svg xmlns='http://www.w3.org/2000/svg'/>");
        File.WriteAllText(Path.Combine(input, "data-bindings.dat"), "not an asset");
        File.WriteAllText(Path.Combine(input, "host-bindings.json"), "not an asset");
        var tile = packages.Import(path);
        Assert.False(tile.AllowWebNetwork);
        Assert.True(packages.IsWeb(tile.ScriptId));
        Assert.Equal("image/svg+xml", packages.WebAssets(tile.ScriptId)["/assets/nested/shape.svg"].ContentType);
        Assert.False(File.Exists(packages.DataBindingPath(tile.ScriptId)));
        Assert.False(File.Exists(packages.SlideshowBindingPath(tile.ScriptId)));
        using (var oversized = File.Create(Path.Combine(input, "assets", "too-big.bin"))) oversized.SetLength(4 * 1024 * 1024 + 1);
        Assert.Throws<InvalidDataException>(() => packages.Import(path));
    }

    [Fact]
    public void ExistingPreferencesKeepNamesEnabledAndStudioReflectsAnExplicitOffChoice()
    {
        Assert.True(JsonSerializer.Deserialize<Preferences>("{}", ScriptTileProtocol.Json)!.ShowLiveTileNames);
        var preferences = new Preferences { ShowLiveTileNames = false };
        Assert.False(JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences, ScriptTileProtocol.Json), ScriptTileProtocol.Json)!.ShowLiveTileNames);
        var manifest = ScriptTilePackages.BundledManifest("orbit");
        var context = TileStudioContext.Build(manifest, TileSize.Large, 1, "{}", JsonSerializer.SerializeToElement(new { }), false,
            DateTimeOffset.Now, theme: "light", showTitle: false);
        Assert.False(context.GetProperty("appearance").GetProperty("showTitle").GetBoolean());
        Assert.Equal("light", context.GetProperty("theme").GetProperty("mode").GetString());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ReStart-Studio-" + Guid.NewGuid().ToString("N"));
        internal TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            var root = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            if (System.IO.Path.GetFullPath(Path).StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(Path)) Directory.Delete(Path, true);
        }
    }
}
