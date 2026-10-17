using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class FolderExpansionTests
{
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
