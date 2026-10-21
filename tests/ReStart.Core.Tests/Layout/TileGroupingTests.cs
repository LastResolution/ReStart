using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class TileGroupingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreatingHeaderDoesNotPushAnEarlierNeighborBelowTheNewGroup(bool independentNeighbor)
    {
        var source = new Tile(); var target = new Tile();
        var upper = new TileGroup { IsStandalone = independentNeighbor, Column = 1, X = 8, Row = 0, Tiles = [new()] };
        var destination = new TileGroup { IsStandalone = true, Column = 1, X = 8, Row = 5, Tiles = [target] };
        var lower = new TileGroup { Column = 1, X = 8, Row = 30, Tiles = [new()] };
        var document = new LayoutDocument { Groups = [new() { IsStandalone = true, Column = 0, X = 0, Row = 0, Tiles = [source] }, upper, destination, lower] };
        var before = GroupLayout.Pack(document.Groups, 3, 6, 52, preserveColumns: true).ToDictionary(p => p.Group.Id);
        var plan = TileGrouping.Plan(document, [source], target.Id, 3, 52)!;
        var after = GroupLayout.Pack(plan, 3, 6, 52, preserveColumns: true).ToDictionary(p => p.Group.Id);
        Assert.Equal(before[upper.Id].Top, after[upper.Id].Top);
        Assert.Equal(before[lower.Id].Top, after[lower.Id].Top);
        Assert.True(after[upper.Id].Top + after[upper.Id].Height <= after[destination.Id].Top);
        Assert.InRange(after[destination.Id].Top + GroupLayout.HeaderHeight - before[destination.Id].Top, 0, 26);
    }
    [Theory]
    [InlineData(TileSize.Small, TileSize.Large, .7)]
    [InlineData(TileSize.Medium, TileSize.Medium, 1)]
    [InlineData(TileSize.Wide, TileSize.Small, 1.5)]
    [InlineData(TileSize.Large, TileSize.Small, 1)]
    public void IndependentOverlapCreatesGroupAtTargetWithoutChangingInput(TileSize sourceSize, TileSize targetSize, double scale)
    {
        var source = new Tile { Label = "Source", Size = sourceSize, ScriptId = "sample", Color = "#123456", UseCustomColor = true };
        var target = new Tile { Label = "Target", Size = targetSize, Column = 2, Row = 0 };
        var destination = new TileGroup { IsStandalone = true, Name = "", X = 8, Row = 6, Tiles = [target] };
        var document = new LayoutDocument { Groups = [new() { IsStandalone = true, X = 0, Row = 0, Tiles = [source] }, destination] };
        var cell = 52 * scale;
        var before = JsonSerializer.Serialize(document.Groups, LayoutStore.JsonOptions);
        var origin = GroupLayout.Pack(document.Groups, 3, 6, cell, preserveColumns: true).Single(p => p.Group == destination);
        var plan = TileGrouping.Plan(document, [source], target.Id, 3, cell)!;
        Assert.Equal(before, JsonSerializer.Serialize(document.Groups, LayoutStore.JsonOptions));
        var group = Assert.Single(plan); Assert.False(group.IsStandalone); Assert.Equal(destination.Id, group.Id); Assert.Equal("", group.Name);
        Assert.Equal(new[] { target.Id, source.Id }, group.Tiles.Select(t => t.Id)); Assert.All(group.Tiles, t => Assert.False(t.IsFolder));
        Assert.Equal(source.ScriptId, group.Tiles[1].ScriptId); Assert.Equal(source.Color, group.Tiles[1].Color); Assert.Equal(sourceSize, group.Tiles[1].Size);
        var placed = Assert.Single(GroupLayout.Pack(plan, 3, 6, cell, preserveColumns: true));
        Assert.Equal(origin.Left, placed.Left, 6); Assert.Equal(origin.Top, placed.Top + GroupLayout.HeaderHeight, 6);
        var packed = TileLayout.Pack(group.Tiles, 6);
        Assert.False(packed[0].Column < packed[1].Column + packed[1].Width && packed[0].Column + packed[0].Width > packed[1].Column
            && packed[0].Row < packed[1].Row + packed[1].Height && packed[0].Row + packed[0].Height > packed[1].Row);
    }

    [Fact]
    public void CollectingChildrenKeepsSingleChildFolderAndUnrelatedEmptyGroup()
    {
        var moved = new Tile(); var remaining = new Tile(); var folder = new Tile { Children = [moved, remaining] };
        var target = new Tile(); var empty = new TileGroup { Name = "Reserved" };
        var document = new LayoutDocument { Groups = [new() { Tiles = [folder] }, empty, new() { IsStandalone = true, X = 16, Tiles = [target] }] };
        var plan = TileGrouping.Plan(document, [moved], target.Id, 3, 52)!;
        var retained = Assert.Single(plan.SelectMany(g => g.Tiles), t => t.Id == folder.Id);
        Assert.Equal(remaining.Id, Assert.Single(retained.Children).Id); Assert.Contains(plan, g => g.Id == empty.Id);
        Assert.Equal(1, plan.SelectMany(g => g.Tiles).Count(t => t.Id == moved.Id));
    }

    [Fact]
    public void CollectsFolderAndNewCatalogTileAsRootsWithoutNesting()
    {
        var child = new Tile(); var folder = new Tile { Children = [child] }; var target = new Tile(); var catalog = new Tile { AppId = "sample" };
        var document = new LayoutDocument { Groups = [new() { Tiles = [folder] }, new() { IsStandalone = true, X = 8, Tiles = [target] }] };
        var group = Assert.Single(TileGrouping.Plan(document, [folder, catalog], target.Id, 3, 52)!);
        Assert.Equal(3, group.Tiles.Count); Assert.Equal(child.Id, Assert.Single(group.Tiles.Single(t => t.Id == folder.Id).Children).Id);
        Assert.Equal(catalog.Id, group.Tiles[2].Id);
        Assert.Null(TileGrouping.Plan(document, [folder, child], target.Id, 3, 52));
        Assert.Null(TileGrouping.Plan(document, [target], target.Id, 3, 52));
    }

    [Fact]
    public void ExistingFoldersAreNotConvertedIntoOrdinaryGroups()
    {
        var folder = new Tile { Children = [new(), new()] }; var source = new Tile();
        var document = new LayoutDocument { Groups = [new() { IsStandalone = true, Tiles = [folder] }] };
        Assert.Null(TileGrouping.Plan(document, [source], folder.Id, 3, 52));
    }
}
