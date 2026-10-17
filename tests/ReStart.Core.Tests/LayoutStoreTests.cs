using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class LayoutStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ReStart-tests-" + Guid.NewGuid().ToString("N"));
    public LayoutStoreTests() => Directory.CreateDirectory(_directory);
    private string FilePath => Path.Combine(_directory, "layout.json");

    [Fact]
    public void PreservesFoldersAndSettingsAcrossSaves()
    {
        var store = new LayoutStore(FilePath);
        var doc = new LayoutDocument { Groups = [new() { Name = "仕事", Tiles = [new() { Label = "道具", Children = [new() { Label = "メモ帳", Target = "notepad.exe", Size = TileSize.Wide }] }] }] };
        doc.Preferences.Columns = 8;
        store.Save(doc);
        var restored = Assert.IsType<LayoutDocument>(store.Load());
        Assert.Equal("notepad.exe", restored.Groups[0].Tiles[0].Children[0].Target);
        Assert.Equal(TileSize.Wide, restored.Groups[0].Tiles[0].Children[0].Size);
        Assert.Equal(8, restored.Preferences.Columns);
    }

    [Fact]
    public void AtomicSaveKeepsThePreviousVersion()
    {
        var store = new LayoutStore(FilePath);
        store.Save(new() { Groups = [new() { Name = "before" }] });
        store.Save(new() { Groups = [new() { Name = "after" }] });
        Assert.Equal("before", LayoutStore.Read(FilePath + ".bak").Groups[0].Name);
        Assert.Equal("after", store.Load()!.Groups[0].Name);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void CorruptLayoutFallsBackToBackup()
    {
        var store = new LayoutStore(FilePath);
        store.Save(new() { Groups = [new() { Name = "safe" }] }); store.Save(new());
        File.WriteAllText(FilePath, "broken json");
        Assert.Equal("safe", store.Load()!.Groups[0].Name);
        Assert.NotNull(store.RecoveryMessage);
        Assert.Equal("broken json", File.ReadAllText(Assert.Single(Directory.GetFiles(_directory, "*.corrupt-*"))));
    }

    [Fact]
    public void CorruptLayoutWithoutBackupIsPreserved()
    {
        File.WriteAllText(FilePath, "broken json");
        var store = new LayoutStore(FilePath);
        Assert.Null(store.Load());
        Assert.Single(Directory.GetFiles(_directory, "*.corrupt-*"));
    }

    [Fact]
    public void FutureVersionIsNeverOverwritten()
    {
        var json = "{\"Version\":99,\"Groups\":[]}";
        File.WriteAllText(FilePath, json);
        var store = new LayoutStore(FilePath);
        Assert.Null(store.Load()); Assert.True(store.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => store.Save(new()));
        Assert.Equal(json, File.ReadAllText(FilePath));
    }

    [Fact]
    public void SchemaMigrationKeepsTheOriginalBeyondTheRotatingBackup()
    {
        const string original = "{\"Version\":1,\"Groups\":[{\"Name\":\"legacy\"}]}";
        File.WriteAllText(FilePath, original);
        var store = new LayoutStore(FilePath);
        var document = store.Load()!;
        Assert.Equal(2, document.Version);
        store.Save(document);
        var migrationPath = Assert.IsType<string>(store.MigrationBackupPath);
        document.Groups[0].Name = "updated";
        store.Save(document);
        Assert.Equal(original, File.ReadAllText(migrationPath));
        Assert.Equal(2, LayoutStore.Read(FilePath + ".bak").Version);
        Assert.Single(Directory.GetFiles(_directory, "*.schema1-*.bak"));
    }

    [Fact]
    public void RecoveringALegacyBackupPreservesThatSourceBeforeReplacingIt()
    {
        File.WriteAllText(FilePath, "invalid json");
        const string legacy = "{\"Version\":1,\"Groups\":[{\"Name\":\"recovered\"}]}";
        File.WriteAllText(FilePath + ".bak", legacy);
        var store = new LayoutStore(FilePath);
        var document = store.Load()!;
        store.Save(document);
        Assert.Equal(legacy, File.ReadAllText(store.MigrationBackupPath!));
        Assert.Equal("recovered", LayoutStore.Read(FilePath).Groups[0].Name);
    }

    [Fact]
    public void RepairsInvalidSizesAndDuplicateIds()
    {
        var doc = new LayoutDocument { Groups = [new() { Id = "same", Tiles = [new() { Id = "same", Size = (TileSize)999 }] }] };
        doc.Preferences.TileScale = 100; doc.Preferences.Columns = 999;
        doc.Normalize();
        Assert.NotEqual(doc.Groups[0].Id, doc.Groups[0].Tiles[0].Id);
        Assert.Equal(TileSize.Medium, doc.Groups[0].Tiles[0].Size);
        Assert.Equal(1.5, doc.Preferences.TileScale); Assert.Equal(6, doc.Preferences.Columns);
    }

    [Fact]
    public void RejectsNestedFolders()
    {
        var doc = new LayoutDocument { Groups = [new() { Tiles = [new() { Children = [new() { Children = [new()] }] }] }] };
        Assert.Throws<InvalidDataException>(doc.Normalize);
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
