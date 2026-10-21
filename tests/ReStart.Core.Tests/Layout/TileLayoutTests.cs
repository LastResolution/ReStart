using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class TileLayoutTests
{
    [Theory]
    [InlineData(TileSize.Small, 1, 1)]
    [InlineData(TileSize.Medium, 2, 2)]
    [InlineData(TileSize.Wide, 4, 2)]
    [InlineData(TileSize.Large, 4, 4)]
    public void UsesWindows10LogicalSizes(TileSize size, int width, int height) =>
        Assert.Equal((width, height), TileLayout.Dimensions(size));

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    public void MixedSizesNeverOverlapOrLeaveTheGroup(int columns)
    {
        var random = new Random(42);
        var tiles = Enumerable.Range(0, 200).Select(_ => new Tile { Size = (TileSize)random.Next(4) }).ToList();
        var positions = TileLayout.Pack(tiles, columns);
        Assert.Equal(tiles.Count, positions.Count);
        var cells = new HashSet<(int, int)>();
        foreach (var position in positions)
        {
            Assert.InRange(position.Column + position.Width, 1, columns);
            Assert.True(position.Row >= 0);
            for (var y = position.Row; y < position.Row + position.Height; y++)
                for (var x = position.Column; x < position.Column + position.Width; x++)
                    Assert.True(cells.Add((x, y)), "Tiles must not overlap.");
        }
    }

    [Fact]
    public void SmallTilesFillTheGapBesideALargeTile()
    {
        var positions = TileLayout.Pack([new() { Size = TileSize.Large }, new() { Size = TileSize.Medium }, new() { Size = TileSize.Small }], 6);
        Assert.Equal((4, 2), (positions[2].Column, positions[2].Row));
    }

    [Fact]
    public void RejectsAnImpossibleGrid() => Assert.Throws<ArgumentOutOfRangeException>(() => TileLayout.Pack([], 3));
}
