namespace ReStart.Core;

/// <summary>Describes one tile rectangle in logical grid cells.</summary>
public readonly record struct TilePlacement(Tile Tile, int Column, int Row, int Width, int Height);

/// <summary>Packs tiles into a logical grid without changing the input models.</summary>
public static class TileLayout
{
    /// <summary>Returns the logical cell width and height for a tile size.</summary>
    public static (int Width, int Height) Dimensions(TileSize size) => size switch
    {
        TileSize.Small => (1, 1),
        TileSize.Wide => (4, 2),
        TileSize.Large => (4, 4),
        _ => (2, 2)
    };

    // Reserve user-positioned tiles first. Legacy tiles and collisions fill free cells.
    /// <summary>Reserves valid explicit positions first, then packs legacy or colliding tiles into free cells in input order.</summary>
    public static IReadOnlyList<TilePlacement> Pack(IEnumerable<Tile> tiles, int columns)
    {
        if (columns < 4 || columns > 16) throw new ArgumentOutOfRangeException(nameof(columns));
        var items = tiles.ToArray();
        var occupied = new HashSet<(int X, int Y)>();
        var positions = new Dictionary<Tile, TilePlacement>();
        void Place(Tile tile, int col, int row, int width, int height)
        {
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++) occupied.Add((col + x, row + y));
            positions[tile] = new(tile, col, row, width, height);
        }
        bool Free(int col, int row, int width, int height)
        {
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++) if (occupied.Contains((col + x, row + y))) return false;
            return true;
        }
        foreach (var tile in items)
        {
            var (width, height) = Dimensions(tile.Size);
            if (tile.Column is int col && tile.Row is int row && col >= 0 && col + width <= columns && row is >= 0 and <= 200 && Free(col, row, width, height))
                Place(tile, col, row, width, height);
        }
        foreach (var tile in items.Where(t => !positions.ContainsKey(t)))
        {
            var (width, height) = Dimensions(tile.Size);
            for (var row = 0; ; row++)
            {
                var placed = false;
                for (var col = 0; col <= columns - width; col++)
                {
                    if (!Free(col, row, width, height)) continue;
                    Place(tile, col, row, width, height);
                    placed = true;
                    break;
                }
                if (placed) break;
            }
        }
        return items.Select(t => positions[t]).ToArray();
    }

    /// <summary>Rounds a pointer-relative position to a cell and clamps the tile inside the configured columns and row limit.</summary>
    public static (int Column, int Row) Snap(double x, double y, double cell, TileSize size, int columns)
    {
        var (width, _) = Dimensions(size);
        return (Math.Clamp((int)Math.Round(x / cell, MidpointRounding.AwayFromZero), 0, columns - width),
            Math.Clamp((int)Math.Round(y / cell, MidpointRounding.AwayFromZero), 0, 200));
    }
}
