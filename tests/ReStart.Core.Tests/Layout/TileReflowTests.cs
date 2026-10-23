using System.Text.Json;

namespace ReStart.Core.Tests;

public sealed class TileReflowTests
{
    public static IEnumerable<object[]> SameSizeMoves() =>
        from size in Enum.GetValues<TileSize>()
        from vertical in new[] { false, true }
        from folder in new[] { false, true }
        select new object[] { size, vertical, folder };

    [Theory]
    [MemberData(nameof(SameSizeMoves))]
    public void ReorderingUsesTheVacatedSlotBeforeAnyEarlierHole(TileSize size, bool vertical, bool folder)
    {
        var (w, h) = TileLayout.Dimensions(size);
        var recipient = new Tile { Size = size, Column = 0, Row = 6 };
        var moving = new Tile { Size = size, Column = vertical ? 0 : w, Row = vertical ? 6 + h : 6 };
        var other = new Tile { Column = 0, Row = 16 };
        var children = new List<Tile> { recipient, moving, other };
        var container = new Tile { Children = folder ? children : [] };
        var group = new TileGroup { Tiles = folder ? [container] : children };
        var document = new LayoutDocument { Preferences = new() { Columns = 8 }, Groups = [group] };
        var oldMoving = (moving.Column, moving.Row);
        if (folder) LayoutEditor.MoveIntoFolder(document, moving, container, 0, 6);
        else LayoutEditor.MoveToCell(document, moving, group, 0, 6);
        Assert.Equal((0, 6), (moving.Column, moving.Row));
        Assert.Equal(oldMoving, (recipient.Column, recipient.Row));
        Assert.Equal((0, 16), (other.Column, other.Row));
        AssertValid(children, 8);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACompleteDragPathDoesNotTemporarilyGrowIntoASpareColumn(bool vertical)
    {
        var recipient = new Tile { Column = 0, Row = 0 };
        var moving = new Tile { Column = vertical ? 0 : 2, Row = vertical ? 2 : 0 };
        var companion = new Tile { Column = vertical ? 0 : 4, Row = vertical ? 4 : 0 };
        var group = new TileGroup { Tiles = [recipient, moving, companion] };
        var document = new LayoutDocument { Preferences = new() { Columns = 8 }, Groups = [group] };
        var before = JsonSerializer.Serialize(document.Groups);
        for (var step = 0; step <= 100; step++)
        {
            var position = 2 - step / 50d;
            var snap = TileLayout.SnapMove(group.Tiles, moving, vertical ? 0 : position * 52,
                vertical ? position * 52 : 0, 52, 8);
            var copy = new LayoutDocument { Preferences = document.Preferences, Groups = LayoutHistory.CopyGroups(document.Groups) };
            LayoutEditor.MoveToCell(copy, LayoutEditor.Find(copy, moving.Id)!, copy.Groups[0], snap.Column, snap.Row);
            var placed = TileLayout.Pack(copy.Groups[0].Tiles, 8);
            Assert.True(placed.Max(p => p.Column + p.Width) <= (vertical ? 2 : 6));
            Assert.True(placed.Max(p => p.Row + p.Height) <= (vertical ? 6 : 2));
            var neighbor = placed.Single(p => p.Tile.Id == recipient.Id);
            Assert.Contains(vertical ? neighbor.Row : neighbor.Column, new[] { 0, 2 });
            AssertValid(copy.Groups[0].Tiles, 8);
        }
        Assert.Equal(before, JsonSerializer.Serialize(document.Groups));
    }

    [Fact]
    public void AnExternalInsertionKeepsOtherGroupsAndUnaffectedTilesInPlace()
    {
        var inserted = new Tile(); var neighbor = new Tile { Column = 0, Row = 0 };
        var unaffected = new Tile { Column = 2, Row = 0 };
        var origin = new TileGroup { X = 20, Row = 0, Tiles = [inserted, new() { Column = 2, Row = 0 }] };
        var destination = new TileGroup { X = 0, Row = 0, Tiles = [neighbor, unaffected] };
        var document = new LayoutDocument { Preferences = new() { Columns = 8 }, Groups = [destination, origin] };
        LayoutEditor.MoveToCell(document, inserted, destination, 0, 0);
        Assert.Equal((0, 2), (neighbor.Column, neighbor.Row));
        Assert.Equal((2, 0), (unaffected.Column, unaffected.Row));
        Assert.Equal(20, origin.X); Assert.Equal(0, origin.Row); Assert.Single(origin.Tiles);
        AssertValid(destination.Tiles, 8);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectionReusesItsVacatedCellsAndCanBeCancelledWithoutMutation(bool folder)
    {
        var selected = new[] { new Tile { Column = 0, Row = 2 }, new Tile { Column = 2, Row = 2 } };
        var recipients = new[] { new Tile { Column = 0, Row = 6 }, new Tile { Column = 2, Row = 6 } };
        var tiles = selected.Concat(recipients).ToList(); var container = new Tile { Children = folder ? tiles : [] };
        var group = new TileGroup { Tiles = folder ? [container] : tiles };
        var document = new LayoutDocument { Preferences = new() { Columns = 8 }, Groups = [group] };
        var before = JsonSerializer.Serialize(document.Groups);
        var selection = selected.Select(t => new SelectedTilePosition(t.Id, t.Column!.Value, t.Row!.Value)).ToArray();
        var plan = TileBatchMove.Plan(document, selection, selected[0].Id, folder ? null : group.Id, folder ? container.Id : null, 0, 6, 3, 52)!;
        var result = folder ? plan[0].Tiles[0].Children : plan[0].Tiles;
        Assert.Equal(new[] { 2, 2 }, result.Where(t => recipients.Any(r => r.Id == t.Id)).Select(t => t.Row!.Value));
        AssertValid(result, 8);
        Assert.Equal(before, JsonSerializer.Serialize(document.Groups));
    }

    [Fact]
    public void MixedSizeInternalAndExternalMovesKeepAllTilesWithoutOverlap()
    {
        foreach (var sourceSize in Enum.GetValues<TileSize>())
            foreach (var targetSize in Enum.GetValues<TileSize>())
                foreach (var external in new[] { false, true })
                {
                    var moving = new Tile { Size = sourceSize, Column = 4, Row = 6 };
                    var target = new Tile { Size = targetSize, Column = 0, Row = 6 };
                    var untouched = new Tile { Size = TileSize.Small, Column = 0, Row = 0 };
                    var group = new TileGroup { Tiles = external ? [target, untouched] : [target, untouched, moving] };
                    var document = new LayoutDocument { Preferences = new() { Columns = 8 }, Groups = [group] };
                    if (external) document.Groups.Add(new() { Tiles = [moving] });
                    LayoutEditor.MoveToCell(document, moving, group, 0, 6);
                    Assert.Equal(3, group.Tiles.Count); Assert.Equal((0, 0), (untouched.Column, untouched.Row));
                    Assert.Equal((0, 6), (moving.Column, moving.Row)); AssertValid(group.Tiles, 8);
                }
    }

    private static void AssertValid(IReadOnlyList<Tile> tiles, int columns)
    {
        var occupied = new HashSet<(int X, int Y)>();
        foreach (var tile in TileLayout.Pack(tiles, columns))
        {
            Assert.Equal((tile.Column, tile.Row), (tile.Tile.Column, tile.Tile.Row));
            for (var y = tile.Row; y < tile.Row + tile.Height; y++)
                for (var x = tile.Column; x < tile.Column + tile.Width; x++) Assert.True(occupied.Add((x, y)));
        }
    }
}
