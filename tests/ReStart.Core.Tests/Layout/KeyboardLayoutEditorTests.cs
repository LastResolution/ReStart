using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class KeyboardLayoutEditorTests
{
    [Fact]
    public void NudgeKeepsSelectionOffsetsAndLeavesSourceUntouched()
    {
        var a = new Tile { Column = 0, Row = 0 }; var b = new Tile { Column = 2, Row = 0 };
        var doc = new LayoutDocument { Groups = [new() { Tiles = [a, b] }] };
        var before = JsonSerializer.Serialize(doc);
        var plan = KeyboardLayoutEditor.Nudge(doc, [a.Id, b.Id], 1, 1, 3);
        Assert.NotNull(plan);
        Assert.Equal((1, 1), (plan[0].Tiles.Single(t => t.Id == a.Id).Column, plan[0].Tiles.Single(t => t.Id == a.Id).Row));
        Assert.Equal((3, 1), (plan[0].Tiles.Single(t => t.Id == b.Id).Column, plan[0].Tiles.Single(t => t.Id == b.Id).Row));
        Assert.Null(KeyboardLayoutEditor.Nudge(doc, [a.Id], -1, 0, 3));
        Assert.Equal(before, JsonSerializer.Serialize(doc));
    }

    [Fact]
    public void FolderChildrenRemainInTheirFolderAndMixedContainersAreRejected()
    {
        var child = new Tile(); var folder = new Tile { Children = [child] }; var root = new Tile();
        var doc = new LayoutDocument { Groups = [new() { Tiles = [folder, root] }] };
        var plan = KeyboardLayoutEditor.Nudge(doc, [child.Id], 1, 1, 3);
        Assert.NotNull(plan);
        var moved = plan[0].Tiles.Single(t => t.Id == folder.Id).Children.Single();
        Assert.Equal((1, 1), (moved.Column, moved.Row));
        Assert.Null(KeyboardLayoutEditor.Nudge(doc, [child.Id, root.Id], 0, 1, 3));
        Assert.Null(KeyboardLayoutEditor.Resize(doc, [folder.Id, child.Id], TileSize.Small, 3));
    }

    [Fact]
    public void IndependentMoveAndGroupMovePreserveVisibleAnchors()
    {
        var tile = new Tile(); var group = new TileGroup { IsStandalone = true, X = 2.5, Column = 0, Row = 4, Tiles = [tile] };
        var doc = new LayoutDocument { Groups = [group] };
        var plan = KeyboardLayoutEditor.Nudge(doc, [tile.Id], 1, 1, 3);
        Assert.NotNull(plan); Assert.Equal(3.5, plan[0].X); Assert.Equal(6, plan[0].Row);
        var movedGroup = KeyboardLayoutEditor.MoveGroup(doc, tile.Id, 1, 1, 3);
        Assert.NotNull(movedGroup); Assert.Equal(group.Id, movedGroup[0].Id);
        Assert.Equal(3.5, movedGroup[0].X); Assert.Equal(6, movedGroup[0].Row);
    }

    [Fact]
    public void LocksBlockKeyboardMovesAndResizeAndEditsAreOneUndo()
    {
        var tile = new Tile(); var group = new TileGroup { Tiles = [tile] }; var doc = new LayoutDocument { Groups = [group] };
        doc.Preferences.LockedGroupIds.Add(group.Id);
        Assert.Null(KeyboardLayoutEditor.Nudge(doc, [tile.Id], 0, 1, 3));
        Assert.Null(KeyboardLayoutEditor.MoveGroup(doc, tile.Id, 1, 0, 3));
        Assert.Null(KeyboardLayoutEditor.Resize(doc, [tile.Id], TileSize.Large, 3));
        doc.Preferences.LockedGroupIds.Clear();
        var history = new LayoutHistory(); history.Reset(doc);
        doc.Groups = KeyboardLayoutEditor.Resize(doc, [tile.Id], TileSize.Large, 3)!; history.Record(doc);
        Assert.Equal(TileSize.Large, doc.Groups[0].Tiles[0].Size);
        Assert.True(history.Undo(doc)); Assert.Equal(TileSize.Medium, doc.Groups[0].Tiles[0].Size);
    }
}
