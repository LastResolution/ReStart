using System.IO.Compression;
using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class ProfileBackupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ReStart-backup-tests-" + Guid.NewGuid().ToString("N"));
    private string Source => Path.Combine(_root, "source");
    private string Destination => Path.Combine(_root, "destination");
    private string Archive => Path.Combine(_root, "profile.zip");
    private LayoutDocument Seed()
    {
        Directory.CreateDirectory(Source); var id = Guid.NewGuid().ToString("N");
        var background = Guid.NewGuid().ToString("N");
        var package = Path.Combine(Source, "tiles", id); Directory.CreateDirectory(Path.Combine(package, "assets"));
        File.WriteAllText(Path.Combine(package, "manifest.json"), "{\"version\":2,\"renderer\":\"web\",\"title\":\"Private sample\",\"capabilities\":[\"web.network\"]}");
        File.WriteAllText(Path.Combine(package, "tile.js"), "function render(){return {text:'sample'}}");
        File.WriteAllText(Path.Combine(package, "index.html"), "<title>Sample</title>"); File.WriteAllText(Path.Combine(package, "tile.css"), "body{color:white}");
        File.WriteAllText(Path.Combine(package, "assets", "sample.svg"), "<svg/>");
        File.WriteAllText(Path.Combine(package, "data-bindings.dat"), "private-connection-sentinel");
        File.WriteAllText(Path.Combine(package, "host-bindings.json"), "private-photo-folder-sentinel");
        Directory.CreateDirectory(Path.Combine(Source, "TileLibrary", "Sample draft"));
        File.WriteAllText(Path.Combine(Source, "TileLibrary", "Sample draft", "manifest.json"), "{\"title\":\"Draft\"}");
        File.WriteAllText(Path.Combine(Source, "TileLibrary", "Sample draft", "tile.js"), "function render(){return {text:'draft'}}");
        Directory.CreateDirectory(Path.Combine(Source, "Backgrounds")); File.WriteAllBytes(Path.Combine(Source, "Backgrounds", background + ".png"), [1, 2, 3]);
        File.WriteAllText(Path.Combine(Source, "error.log"), "private-log-sentinel");
        Directory.CreateDirectory(Path.Combine(Source, "other-project")); File.WriteAllText(Path.Combine(Source, "other-project", "customer.json"), "private-customer-sentinel");
        return new() { Preferences = new() { Theme = ThemeMode.Light, CustomBackgroundId = background }, Groups = [new() { Name = "Sample", Tiles = [new() { Label = "Sample", Widget = "script", ScriptId = id, AllowWebNetwork = true }] }] };
    }
    [Fact]
    public void RoundTripIncludesOwnedResourcesAndRevokesNetworkWithoutCopyingConnectionsOrUnrelatedData()
    {
        var model = Seed(); var original = JsonSerializer.Serialize(model);
        ProfileBackup.Export(Archive, Source, model);
        Assert.Equal(original, JsonSerializer.Serialize(model));
        using (var zip = ZipFile.OpenRead(Archive))
        {
            Assert.Contains(zip.Entries, e => e.FullName.EndsWith("assets/sample.svg"));
            Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains("bindings") || e.FullName.Contains("other-project") || e.FullName == "error.log");
            foreach (var entry in zip.Entries) { using var read = new StreamReader(entry.Open()); Assert.DoesNotContain("private-", read.ReadToEnd()); }
        }
        Directory.CreateDirectory(Destination); File.WriteAllText(Path.Combine(Destination, "unrelated.txt"), "retained");
        using (var pending = ProfileBackup.PrepareRestore(Archive, Destination))
        {
            var tile = pending.Layout.Groups[0].Tiles[0]; Assert.NotEqual(model.Groups[0].Tiles[0].ScriptId, tile.ScriptId); Assert.False(tile.AllowWebNetwork);
            Assert.NotEqual(model.Preferences.CustomBackgroundId, pending.Layout.Preferences.CustomBackgroundId);
            Assert.True(File.Exists(Path.Combine(Destination, "tiles", tile.ScriptId, "assets", "sample.svg")));
            Assert.Single(Directory.GetDirectories(Path.Combine(Destination, "TileLibrary")));
            var store = new LayoutStore(Path.Combine(Destination, "layout.json")); pending.Commit(store.Save);
            Assert.Equal(ThemeMode.Light, store.Load()!.Preferences.Theme);
            Assert.Throws<InvalidOperationException>(() => pending.Commit(store.Save));
        }
        Assert.Equal("retained", File.ReadAllText(Path.Combine(Destination, "unrelated.txt")));
        Assert.True(File.Exists(Path.Combine(Destination, "layout.json")));
        Assert.Empty(Directory.GetDirectories(Destination, ".restore-*"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationOrFailedSaveRemovesOnlyFreshResources(bool fail)
    {
        var document = Seed(); ProfileBackup.Export(Archive, Source, document);
        Directory.CreateDirectory(Destination); File.WriteAllText(Path.Combine(Destination, "layout.json"), "original-sentinel");
        using (var pending = ProfileBackup.PrepareRestore(Archive, Destination))
            if (fail) Assert.Throws<IOException>(() => pending.Commit(_ => throw new IOException("simulated")));
        Assert.Equal("original-sentinel", File.ReadAllText(Path.Combine(Destination, "layout.json")));
        Assert.DoesNotContain(Directory.GetFiles(Destination, "*", SearchOption.AllDirectories), f => Path.GetFileName(f) != "layout.json");
    }
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("tiles/../outside.txt")]
    [InlineData("tiles/bad/data-bindings.dat")]
    [InlineData("layout.json/extra")]
    [InlineData("layout.json")]
    [InlineData("LAYOUT.JSON")]
    public void RejectsEscapingUnknownAndDuplicateEntriesBeforePersistence(string name)
    {
        ProfileBackup.Export(Archive, Source, Seed());
        using (var zip = ZipFile.Open(Archive, ZipArchiveMode.Update)) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write("unexpected"); }
        Assert.Throws<InvalidDataException>(() => ProfileBackup.PrepareRestore(Archive, Destination));
        Assert.False(File.Exists(Path.Combine(Destination, "layout.json")));
        Assert.False(File.Exists(Path.Combine(_root, "outside.txt")));
    }
    [Fact]
    public void RejectsTamperingAndMissingPackagesWithoutOverwritingExistingArchive()
    {
        var layout = Seed(); ProfileBackup.Export(Archive, Source, layout);
        var good = File.ReadAllBytes(Archive);
        File.Delete(Path.Combine(Source, "tiles", layout.Groups[0].Tiles[0].ScriptId, "tile.js"));
        Assert.Throws<InvalidDataException>(() => ProfileBackup.Export(Archive, Source, layout));
        Assert.Equal(good, File.ReadAllBytes(Archive));
        using (var zip = ZipFile.Open(Archive, ZipArchiveMode.Update))
        { zip.GetEntry("layout.json")!.Delete(); using var writer = new StreamWriter(zip.CreateEntry("layout.json").Open()); writer.Write("{}"); }
        Assert.Throws<InvalidDataException>(() => ProfileBackup.PrepareRestore(Archive, Destination));
    }
    [Fact]
    public void RealAtomicWriteFailureRetainsTheOriginalLayoutAndResources()
    {
        ProfileBackup.Export(Archive, Source, Seed()); Directory.CreateDirectory(Destination);
        var path = Path.Combine(Destination, "layout.json"); var store = new LayoutStore(path); store.Save(new() { Groups = [new() { Name = "Original" }] }); store.Load();
        var original = File.ReadAllBytes(path);
        using (var pending = ProfileBackup.PrepareRestore(Archive, Destination))
        {
            using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Assert.ThrowsAny<IOException>(() => pending.Commit(store.Save));
        }
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Empty(Directory.GetDirectories(Destination, ".restore-*"));
        Assert.False(Directory.Exists(Path.Combine(Destination, "tiles")) && Directory.GetDirectories(Path.Combine(Destination, "tiles")).Length > 0);
    }
    [Fact]
    public void RejectsCaseVariantBindingsInAssets()
    {
        var doc = Seed(); var package = Path.Combine(Source, "tiles", doc.Groups[0].Tiles[0].ScriptId, "assets");
        File.WriteAllText(Path.Combine(package, "HOST-BINDINGS.JSON"), "private-sentinel");
        Assert.Throws<InvalidDataException>(() => ProfileBackup.Export(Archive, Source, doc));
        Assert.False(File.Exists(Archive));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
