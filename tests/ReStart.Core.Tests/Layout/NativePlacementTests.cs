using System.Text.Json;
using System.Text.Json.Nodes;

namespace ReStart.Core.Tests;

public sealed class NativePlacementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PinningRespectsPlacementModeWithoutDisplacingExistingGroups(bool free)
    {
        var stationary = new TileGroup { Name = "Existing apps", X = 0, Column = 0, Row = 0, Tiles = [new() { Label = "Existing app" }] };
        var document = new LayoutDocument { Preferences = new() { FreeTilePlacement = free }, Groups = [stationary] };
        var original = GroupLayout.Pack(document.Groups, 3, 6, 52, preserveColumns: true).Single();
        var pin = AppPinning.Pin(document, new("sample", "Sample app", "sample.exe"), availableColumns: 3, viewportWidth: 1200);
        var created = document.Groups.Single(g => g.Tiles.Contains(pin));
        Assert.Equal(free, created.IsStandalone); Assert.Empty(created.Name);
        var after = GroupLayout.Pack(document.Groups, 3, 6, 52, preserveColumns: true);
        Assert.Equal(original, after.Single(p => p.Group == stationary));
        var added = after.Single(p => p.Group == created);
        Assert.False(original.Left < added.Left + added.Width && original.Left + original.Width > added.Left
            && original.Top < added.Top + added.Height && original.Top + original.Height > added.Top);
    }

    [Theory]
    [InlineData(TileSize.Small)]
    [InlineData(TileSize.Medium)]
    [InlineData(TileSize.Wide)]
    [InlineData(TileSize.Large)]
    public void MovingFolderToNewGroupPreservesItsSizeChildrenAndIdentity(TileSize size)
    {
        var folder = new Tile { Label = "Saved folder", Size = size, Children = [new() { Label = "First app" }, new() { Label = "Second app" }] };
        var document = new LayoutDocument { Groups = [new() { Name = "Original group", Tiles = [folder] }] };
        var original = JsonSerializer.Serialize(folder.Children);
        var entry = LayoutEditor.Detach(document, folder, 1, 12, 0, 3, 52, 10, independent: false);
        Assert.False(entry.IsStandalone); Assert.Empty(entry.Name); Assert.Same(folder, Assert.Single(entry.Tiles));
        Assert.Equal(size, folder.Size); Assert.Equal("Saved folder", folder.Label); Assert.Equal(2, folder.Children.Count);
        Assert.Equal(original, JsonSerializer.Serialize(folder.Children));
        Assert.Equal(["First app", "Second app"], folder.Children.Select(t => t.Label));
    }

    [Fact]
    public void NativeSelectionCreatesOneGroupWhileFreePlacementRetainsSeparateEntries()
    {
        var first = new Tile { Label = "First" }; var second = new Tile { Label = "Second" };
        var document = new LayoutDocument { Groups = [new() { Name = "Apps", Tiles = [first, second] }] };
        var selected = new[] { new SelectedTilePosition(first.Id, 0, 0), new SelectedTilePosition(second.Id, 2, 0) };
        var original = JsonSerializer.Serialize(document.Groups);
        var native = TileBatchMove.Plan(document, selected, first.Id, null, null, 10, 10, 3, 52, independent: false)!;
        var group = Assert.Single(native); Assert.False(group.IsStandalone); Assert.Equal(2, group.Tiles.Count);
        var free = TileBatchMove.Plan(document, selected, first.Id, null, null, 10, 10, 3, 52, independent: true)!;
        Assert.Equal(2, free.Count); Assert.All(free, g => Assert.True(g.IsStandalone));
        Assert.Equal(original, JsonSerializer.Serialize(document.Groups));
    }

    [Fact]
    public void LegacyLayoutsAcquireNativeOperationDefaultsWithoutChangingStoredEntries()
    {
        var folder = new Tile
        {
            Label = "Saved folder",
            Size = TileSize.Large,
            Column = 0,
            Row = 0,
            Children = [new() { Label = "Sample editor", Target = "sample-editor.exe" }, new() { Label = "Sample tool", Target = "sample-tool.exe" }]
        };
        var document = new LayoutDocument
        {
            SavedWithAppVersion = "1.7.1",
            Groups = [new() { Name = "Named group", X = 0, Column = 0, Row = 0, Tiles = [folder] },
                new() { IsStandalone = true, X = 12.5, Column = 1, Row = 7, Tiles = [new() { Label = "Independent app", Column = 0, Row = 0 }] }]
        };
        document.Normalize();
        var original = JsonSerializer.Serialize(document.Groups, LayoutStore.JsonOptions);
        var json = JsonNode.Parse(JsonSerializer.Serialize(document, LayoutStore.JsonOptions))!;
        json["Preferences"]!.AsObject().Remove(nameof(Preferences.FreeTilePlacement));
        var restored = JsonSerializer.Deserialize<LayoutDocument>(json.ToJsonString(), LayoutStore.JsonOptions)!; restored.Normalize();
        Assert.False(restored.Preferences.FreeTilePlacement); Assert.True(restored.Preferences.CreateFoldersOnDrop);
        Assert.Equal(original, JsonSerializer.Serialize(restored.Groups, LayoutStore.JsonOptions));
        Assert.Equal(LayoutDocument.CurrentVersion, restored.Version);
    }
}
