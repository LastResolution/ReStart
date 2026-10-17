using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class DragPlacementTests
{
    [Fact]
    public void EmptyCellsRemainEmptyAfterMoveAndJsonRoundTrip()
    {
        var tile = new Tile(); var group = new TileGroup { Tiles = [tile] };
        var doc = new LayoutDocument { Groups = [group] };
        LayoutEditor.MoveToCell(doc, tile, group, 4, 6);
        var json = System.Text.Json.JsonSerializer.Serialize(doc, LayoutStore.JsonOptions);
        var restored = System.Text.Json.JsonSerializer.Deserialize<LayoutDocument>(json, LayoutStore.JsonOptions)!;
        var placement = Assert.Single(TileLayout.Pack(restored.Groups[0].Tiles, 6));
        Assert.Equal((4, 6), (placement.Column, placement.Row));
    }
    [Fact]
    public void DroppingOntoAnotherTileDisplacesOnlyTheCollision()
    {
        var a = new Tile { Column = 0, Row = 0 }; var b = new Tile { Column = 2, Row = 0 }; var c = new Tile { Column = 4, Row = 4 };
        var group = new TileGroup { Tiles = [a, b, c] }; var doc = new LayoutDocument { Groups = [group] };
        LayoutEditor.MoveToCell(doc, a, group, 2, 0);
        Assert.Equal((2, 0), (a.Column, a.Row)); Assert.Equal((4, 4), (c.Column, c.Row));
        Assert.Equal((0, 0), (b.Column, b.Row)); Assert.Equal(3, group.Tiles.Count);
    }
    [Fact]
    public void MovingAcrossGroupsKeepsIdentityAndDestinationCell()
    {
        var tile = new Tile { Size = TileSize.Wide }; var a = new TileGroup { Tiles = [tile] }; var b = new TileGroup();
        var doc = new LayoutDocument { Groups = [a, b] };
        LayoutEditor.MoveToCell(doc, tile, b, 100, 3);
        Assert.Empty(a.Tiles); Assert.Same(tile, Assert.Single(b.Tiles)); Assert.Equal((2, 3), (tile.Column, tile.Row));
    }
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(104, 208, 2, 4)]
    [InlineData(-100, -100, 0, 0)]
    [InlineData(10000, 10000, 4, 192)]
    public void SnapsToVisibleLogicalCells(double x, double y, int col, int row) =>
        Assert.Equal((col, row), TileLayout.Snap(x, y, 52, TileSize.Medium, 6));
    [Fact]
    public void ExplicitTilesAreReservedBeforeLegacyAutoTiles()
    {
        var auto = new Tile(); var fixedTile = new Tile { Column = 0, Row = 0 };
        var packed = TileLayout.Pack([auto, fixedTile], 6);
        Assert.Equal(2, packed[0].Column); Assert.Equal(0, packed[1].Column);
    }
}
