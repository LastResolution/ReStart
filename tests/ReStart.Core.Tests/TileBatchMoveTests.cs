using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class TileBatchMoveTests
{
    [Fact]
    public void InvalidSelectionsAreRejectedWithoutChangingOwnership()
    {
        var tile = new Tile(); var doc = new LayoutDocument { Groups = [new() { Tiles = [tile] }] };
        var before = JsonSerializer.Serialize(doc);
        Assert.Null(TileBatchMove.Plan(doc, [new(tile.Id, 0, 0), new(tile.Id, 2, 0)], tile.Id, null, null, 0, 0, 3, 52));
        Assert.Null(TileBatchMove.Plan(doc, [new("missing", 0, 0)], "missing", null, null, 0, 0, 3, 52));
        Assert.Null(TileBatchMove.Plan(doc, [new(tile.Id, double.NaN, 0)], tile.Id, null, null, 0, 0, 3, 52));
        Assert.Null(TileBatchMove.Plan(doc, [new(tile.Id, 0, 0)], tile.Id, "missing-group", null, 0, 0, 3, 52));
        Assert.Equal(before, JsonSerializer.Serialize(doc));
    }

    [Fact]
    public void GroupMovePreservesOffsetsAndSizesAndReflowsOnlyConflicts()
    {
        var a = new Tile { Size = TileSize.Small }; var b = new Tile { Size = TileSize.Wide };
        var blocker = new Tile { Column = 0, Row = 3 }; var fixedTile = new Tile { Column = 4, Row = 8 };
        var destination = new TileGroup { Tiles = [blocker, fixedTile] };
        var doc = new LayoutDocument { Groups = [new() { Tiles = [a, b] }, destination] };
        var before = JsonSerializer.Serialize(doc);
        var plan = TileBatchMove.Plan(doc, [new(a.Id, 0, 0), new(b.Id, 0, 2)], a.Id, destination.Id, null, 1, 3, 3, 52);
        Assert.NotNull(plan); Assert.Equal(before, JsonSerializer.Serialize(doc));
        var target = Assert.Single(plan);
        var movedA = target.Tiles.Single(t => t.Id == a.Id); var movedB = target.Tiles.Single(t => t.Id == b.Id);
        Assert.Equal((1, 3), (movedA.Column, movedA.Row)); Assert.Equal((1, 5), (movedB.Column, movedB.Row));
        Assert.Equal(TileSize.Wide, movedB.Size);
        Assert.Equal((4, 8), (target.Tiles.Single(t => t.Id == fixedTile.Id).Column, target.Tiles.Single(t => t.Id == fixedTile.Id).Row));
        Assert.Equal(4, TileLayout.Pack(target.Tiles, 6).Count);
    }

    [Fact]
    public void RejectedWideSelectionAndNestedFoldersNeverMutateTheSource()
    {
        var a = new Tile(); var b = new Tile(); var folder = new Tile { Children = [new()] };
        var group = new TileGroup { Tiles = [a, b, folder] }; var doc = new LayoutDocument { Groups = [group] };
        var before = JsonSerializer.Serialize(doc);
        Assert.Null(TileBatchMove.Plan(doc, [new(a.Id, 0, 0), new(b.Id, 8, 0)], a.Id, group.Id, null, 0, 0, 3, 52));
        Assert.Null(TileBatchMove.Plan(doc, [new(folder.Id, 0, 0)], folder.Id, group.Id, folder.Id, 0, 0, 3, 52));
        Assert.Null(TileBatchMove.Plan(doc, [new(folder.Id, 0, 0), new(folder.Children[0].Id, 0, 2)], folder.Id, null, null, 0, 0, 3, 52));
        Assert.Equal(before, JsonSerializer.Serialize(doc));
    }

    [Fact]
    public void EmptyBoardMoveReservesTheWholeSelectionBeforeDisplacingNeighbors()
    {
        var a = new Tile(); var b = new Tile { Size = TileSize.Large };
        var existing = new TileGroup { IsStandalone = true, X = 2.5, Column = 0, Row = 0, Tiles = [new()] };
        var doc = new LayoutDocument { Groups = [new() { Tiles = [a, b] }, existing] };
        var plan = TileBatchMove.Plan(doc, [new(a.Id, 1, 1), new(b.Id, 3, 3)], a.Id, null, null, 2.5, 0, 3, 52)!;
        var moved = plan.Where(g => g.Tiles[0].Id == a.Id || g.Tiles[0].Id == b.Id).OrderBy(g => g.Row).ToArray();
        Assert.Equal(2.5, moved[0].X); Assert.Equal(4.5, moved[1].X);
        Assert.Equal(0, moved[0].Row); Assert.Equal(4, moved[1].Row);
        Assert.All(moved, g => Assert.True(g.IsStandalone));
        Assert.True(plan.Single(g => g.Id == existing.Id).Row >= 4);
        var packed = GroupLayout.Pack(plan, 3, 6, 52, preserveColumns: true);
        Assert.Equal(40, packed.Single(p => p.Group.Tiles[0].Id == a.Id).Top);
        Assert.Equal(144, packed.Single(p => p.Group.Tiles[0].Id == b.Id).Top);
    }

    [Fact]
    public void MovingLastChildrenOutPrunesTheEmptyFolderAndUndoRestoresIt()
    {
        var a = new Tile(); var b = new Tile(); var folder = new Tile { Children = [a, b] };
        var source = new TileGroup { Tiles = [folder] }; var destination = new TileGroup();
        var doc = new LayoutDocument { Groups = [source, destination] }; var history = new LayoutHistory(); history.Reset(doc);
        doc.Groups = TileBatchMove.Plan(doc, [new(a.Id, 0, 0), new(b.Id, 2, 0)], a.Id, destination.Id, null, 0, 0, 3, 52)!;
        history.Record(doc); Assert.Single(doc.Groups); Assert.Equal(2, doc.Groups[0].Tiles.Count);
        Assert.True(history.Undo(doc)); Assert.Equal(2, doc.Groups[0].Tiles[0].Children.Count);
        Assert.True(history.Redo(doc)); Assert.Equal(2, doc.Groups[0].Tiles.Count);
    }

    [Fact]
    public void BatchCanEnterAnOpenFolderWithoutLosingOtherChildren()
    {
        var a = new Tile(); var b = new Tile(); var child = new Tile { Column = 4, Row = 4 };
        var folder = new Tile { Children = [child] }; var group = new TileGroup { Tiles = [a, b, folder] };
        var doc = new LayoutDocument { Groups = [group] };
        var plan = TileBatchMove.Plan(doc, [new(a.Id, 0, 0), new(b.Id, 2, 0)], a.Id, group.Id, folder.Id, 0, 0, 3, 52)!;
        var result = Assert.Single(Assert.Single(plan).Tiles);
        Assert.Equal(3, result.Children.Count); Assert.Equal((4, 4), (result.Children.Single(t => t.Id == child.Id).Column, result.Children.Single(t => t.Id == child.Id).Row));
    }
}
