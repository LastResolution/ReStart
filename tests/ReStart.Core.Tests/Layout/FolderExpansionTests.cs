using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class FolderExpansionTests
{
    [Theory]
    [InlineData(6, .7)]
    [InlineData(6, 1)]
    [InlineData(6, 1.5)]
    [InlineData(8, .7)]
    [InlineData(8, 1)]
    [InlineData(8, 1.5)]
    public void ExpansionDoesNotReserveEmptyWrapColumnsOrDisplaceASideNeighbor(int columns, double scale)
    {
        var folder = new Tile { Column = 4, Row = 0, Children = [new(), new(), new()] };
        var owner = new TileGroup { Column = 0, X = 0, Row = 0, Tiles = [new() { Column = 0, Row = 0 }, new() { Column = 2, Row = 0 }, folder] };
        var neighbor = new TileGroup { Column = 0, X = 6.5, Row = 0, Tiles = [new() { Size = TileSize.Wide }] };
        var below = new TileGroup { Column = 0, X = 0, Row = 6, Tiles = [new()] };
        var cell = 52 * scale;
        var groups = new[] { owner, neighbor, below };
        var before = System.Text.Json.JsonSerializer.Serialize(groups);
        var closed = GroupLayout.Pack(groups, 3, columns, cell, preserveColumns: true);
        var opened = GroupLayout.Pack(groups, 3, columns, cell, expandedFolderId: folder.Id, preserveColumns: true);
        Assert.Equal(6 * cell, opened[0].Width);
        Assert.Equal(closed[1], opened[1]);
        Assert.True(opened[2].Top >= opened[0].Top + opened[0].Height + GroupLayout.Gap);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(groups));
        Assert.Equal(closed, GroupLayout.Pack(groups, 3, columns, cell, preserveColumns: true));
    }

    [Theory]
    [InlineData(false, 0.7, 6)]
    [InlineData(false, 1.0, 8)]
    [InlineData(false, 1.5, 6)]
    [InlineData(true, 0.7, 8)]
    [InlineData(true, 1.0, 6)]
    [InlineData(true, 1.5, 8)]
    public void OpeningAFolderKeepsItsAnchorEvenWhenALowerGroupComesFirst(bool standalone, double scale, int columns)
    {
        var cell = 52 * scale;
        var folder = new Tile { Column = standalone ? 2 : 0, Row = 0, Children = [new(), new(), new(), new()] };
        var owner = new TileGroup { Column = 0, Row = 0, IsStandalone = standalone, Tiles = [folder] };
        var below = new TileGroup { Column = 0, Row = 8, Tiles = [new()] };
        var neighbor = new TileGroup { Column = 2, Row = 0, Tiles = [new()] };
        var lowerNeighbor = new TileGroup { Column = 2, Row = 4, IsStandalone = true, Tiles = [new()] };
        var nextNeighbor = new TileGroup { Column = 2, Row = 8, IsStandalone = true, Tiles = [new()] };
        var groups = new[] { below, owner, neighbor, lowerNeighbor, nextNeighbor };
        var original = System.Text.Json.JsonSerializer.Serialize(groups);
        var closed = GroupLayout.Pack(groups, 3, columns, cell);
        var opened = GroupLayout.Pack(groups, 3, columns, cell, expandedFolderId: folder.Id);
        Assert.Equal(closed[1].Top, opened[1].Top);
        // Standalone root tiles keep their local column when their panel expands.
        Assert.Equal(closed[1].Left, opened[1].Left + (standalone ? 2 * cell : 0));
        Assert.True(opened[0].Top >= opened[1].Top + opened[1].Height + GroupLayout.Gap);
        Assert.Equal(closed[2], opened[2]);
        Assert.Equal(closed[3], opened[3]);
        Assert.Equal(closed[4], opened[4]);
        Assert.Equal(original, System.Text.Json.JsonSerializer.Serialize(groups));
        Assert.Equal(closed, GroupLayout.Pack(groups, 3, columns, cell));
    }

    [Fact]
    public void ExpansionWaitsForTheWholeRowAndNeverSplitsALargeTile()
    {
        var large = new Tile { Size = TileSize.Large, Column = 0, Row = 0 };
        var folder = new Tile { Column = 4, Row = 0, Children = [new()] };
        var below = new Tile { Column = 0, Row = 4 };
        var roots = TileLayout.Pack([large, folder, below], 6);
        var expansion = FolderExpansion.Create(roots, 6, folder.Id)!;
        Assert.Equal(4, expansion.Row);
        Assert.Equal(0, expansion.TileY(roots[0], 52));
        Assert.Equal(4 * 52 + expansion.ExtraHeight(52), expansion.TileY(roots[2], 52));
        Assert.Equal(4, below.Row);
    }

    [Fact]
    public void ANewFolderKeepsItsOwnAnchorAndResetsChildRootCoordinates()
    {
        var a = new Tile { Column = 0, Row = 20 };
        var b = new Tile { Column = 4, Row = 10 };
        var group = new TileGroup { Tiles = [a, b] };
        var doc = new LayoutDocument { Groups = [group] };
        LayoutEditor.MergeFolder(doc, a, b);
        var folder = Assert.Single(group.Tiles);
        Assert.Equal((4, 10), (folder.Column, folder.Row));
        Assert.Equal("", folder.Label);
        Assert.All(folder.Children, child => { Assert.Null(child.Column); Assert.Null(child.Row); });
        Assert.All(TileLayout.Pack(folder.Children, 6), p => Assert.Equal(0, p.Row));
    }

    [Fact]
    public void ExpansionMovesFollowingGroupsForDisplayAndClosingRestoresTheirAnchors()
    {
        var folder = new Tile { Children = [new(), new(), new(), new()] };
        var a = new TileGroup { Column = 0, Row = 0, Tiles = [folder] };
        var b = new TileGroup { Column = 0, Row = 7, Tiles = [new()] };
        var closed = GroupLayout.Pack([a, b], 2, 6, 52);
        var expanded = GroupLayout.Pack([a, b], 2, 6, 52, expandedFolderId: folder.Id);
        Assert.Equal(7, closed[1].Row);
        Assert.True(expanded[1].Row > closed[1].Row);
        Assert.Equal(7, b.Row);
        Assert.Equal(closed[1].Row, GroupLayout.Pack([a, b], 2, 6, 52)[1].Row);
    }

    [Fact]
    public void MovingTheOnlyChildWithinItsFolderKeepsTheParentInTheDocument()
    {
        var child = new Tile(); var folder = new Tile { Children = [child] };
        var group = new TileGroup { Tiles = [folder] }; var doc = new LayoutDocument { Groups = [group] };
        LayoutEditor.MoveIntoFolder(doc, child, folder, 2, 4);
        Assert.Same(folder, Assert.Single(group.Tiles));
        Assert.Same(child, Assert.Single(folder.Children));
        Assert.Equal((2, 4), (child.Column, child.Row));
    }

    [Fact]
    public void MovingIntoAnOpenFolderRetainsIdentityAndResolvesOnlyCollisions()
    {
        var a = new Tile { Column = 0, Row = 0 }; var b = new Tile { Column = 4, Row = 6 };
        var source = new Tile(); var folder = new Tile { Children = [a, b] };
        var group = new TileGroup { Tiles = [source, folder] }; var doc = new LayoutDocument { Groups = [group] };
        LayoutEditor.MoveIntoFolder(doc, source, folder, 0, 0);
        Assert.Equal((0, 0), (source.Column, source.Row));
        Assert.Equal((4, 6), (b.Column, b.Row));
        Assert.DoesNotContain(source, group.Tiles);
        Assert.Contains(source, folder.Children);
        Assert.NotEqual((source.Column, source.Row), (a.Column, a.Row));
    }

    [Fact]
    public void FolderDropsCannotCreateNestedFolders()
    {
        var a = new Tile { Children = [new()] }; var b = new Tile { Children = [new()] };
        var doc = new LayoutDocument { Groups = [new() { Tiles = [a, b] }] };
        Assert.Throws<ArgumentException>(() => LayoutEditor.MoveIntoFolder(doc, a, b, 0, 0));
    }
}
