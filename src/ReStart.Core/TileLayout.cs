namespace ReStart.Core;

public readonly record struct TilePlacement(Tile Tile, int Column, int Row, int Width, int Height);

public static class TileLayout
{
    public static (int Width, int Height) Dimensions(TileSize size) => size switch
    {
        TileSize.Small => (1, 1),
        TileSize.Wide => (4, 2),
        TileSize.Large => (4, 4),
        _ => (2, 2)
    };

    // First-fit packing in the Windows 10 logical 1x1 grid. Small tiles fill gaps.
    public static IReadOnlyList<TilePlacement> Pack(IEnumerable<Tile> tiles, int columns)
    {
        if (columns < 4 || columns > 16) throw new ArgumentOutOfRangeException(nameof(columns));
        var occupied = new HashSet<(int X, int Y)>();
        var result = new List<TilePlacement>();
        foreach (var tile in tiles)
        {
            var (width, height) = Dimensions(tile.Size);
            for (var row = 0; ; row++)
            {
                var placed = false;
                for (var col = 0; col <= columns - width; col++)
                {
                    var free = true;
                    for (var y = 0; y < height && free; y++)
                        for (var x = 0; x < width; x++)
                            if (occupied.Contains((col + x, row + y))) { free = false; break; }
                    if (!free) continue;
                    for (var y = 0; y < height; y++)
                        for (var x = 0; x < width; x++) occupied.Add((col + x, row + y));
                    result.Add(new(tile, col, row, width, height));
                    placed = true;
                    break;
                }
                if (placed) break;
            }
        }
        return result;
    }
}
