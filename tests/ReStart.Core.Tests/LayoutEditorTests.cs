using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class LayoutEditorTests
{
    [Fact]
    public void MovingAcrossGroupsPreservesIdentityAndNeverDuplicates()
    {
        var tile = new Tile { Label = "A" };
        var a = new TileGroup { Tiles = [tile] }; var b = new TileGroup();
        var document = new LayoutDocument { Groups = [a, b] };
        LayoutEditor.Move(document, tile, b);
        Assert.Empty(a.Tiles); Assert.Same(tile, Assert.Single(b.Tiles));
        LayoutEditor.Move(document, tile, b);
        Assert.Single(b.Tiles);
    }
    [Fact]
    public void ReorderingInTheSameGroupAccountsForTheRemovedIndex()
    {
        var tiles = new[] { new Tile { Label = "A" }, new Tile { Label = "B" }, new Tile { Label = "C" } };
        var group = new TileGroup { Tiles = [.. tiles] }; var document = new LayoutDocument { Groups = [group] };
        LayoutEditor.Move(document, tiles[0], group, tiles[2]);
        Assert.Equal(new[] { "B", "A", "C" }, group.Tiles.Select(t => t.Label));
    }
    [Fact]
    public void MergingCreatesAFolderInTheTargetsPosition()
    {
        var a = new Tile { Label = "A" }; var b = new Tile { Label = "B" };
        var group = new TileGroup { Tiles = [a, b] }; var document = new LayoutDocument { Groups = [group] };
        LayoutEditor.MergeFolder(document, a, b);
        var folder = Assert.Single(group.Tiles); Assert.True(folder.IsFolder);
        Assert.Equal(new[] { b, a }, folder.Children);
        document.Normalize();
    }
    [Fact]
    public void MovingTheLastChildOutRemovesTheEmptyFolder()
    {
        var child = new Tile(); var folder = new Tile { Children = [child] };
        var group = new TileGroup { Tiles = [folder] }; var document = new LayoutDocument { Groups = [group] };
        LayoutEditor.Move(document, child, group);
        Assert.Same(child, Assert.Single(group.Tiles));
    }
    [Fact]
    public void FolderCannotBeNestedInsideAnotherFolder()
    {
        var source = new Tile { Children = [new()] }; var target = new Tile { Children = [new()] };
        var group = new TileGroup { Tiles = [source, target] }; var document = new LayoutDocument { Groups = [group] };
        LayoutEditor.MergeFolder(document, source, target);
        Assert.Equal(2, group.Tiles.Count); Assert.Single(target.Children);
    }

    [Fact]
    public void MergingAChildIntoItsOwnFolderCannotDetachTheFolder()
    {
        var child = new Tile(); var folder = new Tile { Children = [child] };
        var group = new TileGroup { Tiles = [folder] }; var document = new LayoutDocument { Groups = [group] };
        LayoutEditor.MergeFolder(document, child, folder);
        Assert.Same(folder, Assert.Single(group.Tiles));
        Assert.Same(child, Assert.Single(folder.Children));
    }
}
