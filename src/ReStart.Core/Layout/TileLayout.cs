namespace ReStart.Core;

/// <summary>Describes one tile rectangle in logical grid cells.</summary>
/// <param name="Tile">Original tile referenced by this placement.</param>
/// <param name="Column">Zero-based local small-cell column.</param>
/// <param name="Row">Zero-based local small-cell row.</param>
/// <param name="Width">Tile width in small cells.</param>
/// <param name="Height">Tile height in small cells.</param>
public readonly record struct TilePlacement(Tile Tile, int Column, int Row, int Width, int Height);

/// <summary>Packs tiles into a logical grid without changing the input models.</summary>
public static class TileLayout
{
    /// <summary>Returns the logical cell width and height for a tile size.</summary>
    /// <param name="size">Small, Medium, Wide, or Large tile size.</param>
    /// <returns>Width and height in small cells for the requested size.</returns>
    public static (int Width, int Height) Dimensions(TileSize size) => size switch
    {
        TileSize.Small => (1, 1),
        TileSize.Wide => (4, 2),
        TileSize.Large => (4, 4),
        _ => (2, 2)
    };

    // Reserve user-positioned tiles first. Legacy tiles and collisions fill free cells.
    /// <summary>Reserves valid explicit positions first, then packs legacy or colliding tiles into free cells in input order.</summary>
    /// <param name="tiles">Tiles with optional explicit local coordinates, in packing priority order.</param>
    /// <param name="columns">Local small-cell column count, from 4 through 16.</param>
    /// <returns>Packed placements in tile-list order, retaining original tile references without writing their coordinates.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The column count is outside 4 through 16.</exception>
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
    /// <param name="x">Pointer-adjusted local X position in DIPs.</param>
    /// <param name="y">Pointer-adjusted local Y position in DIPs.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <param name="size">Size of the tile being positioned.</param>
    /// <param name="columns">Destination width in small cells.</param>
    /// <returns>A clamped local small-cell column and row that fit the tile width.</returns>
    public static (int Column, int Row) Snap(double x, double y, double cell, TileSize size, int columns)
    {
        var (width, _) = Dimensions(size);
        return (Math.Clamp((int)Math.Round(x / cell, MidpointRounding.AwayFromZero), 0, columns - width),
            Math.Clamp((int)Math.Round(y / cell, MidpointRounding.AwayFromZero), 0, 200));
    }

    /// <summary>Snaps a drag over equal-sized occupied slots without temporarily pushing a neighbor out at a partial-cell position.</summary>
    /// <param name="tiles">Destination root tiles or folder children; models are read without mutation.</param>
    /// <param name="moving">Dragged tile, which may originate outside this destination.</param>
    /// <param name="x">Pointer-adjusted local horizontal tile origin in DIPs.</param>
    /// <param name="y">Pointer-adjusted local vertical tile origin in DIPs.</param>
    /// <param name="cell">Positive small-cell pitch in DIPs.</param>
    /// <param name="columns">Destination small-cell column count, from 4 through 16.</param>
    /// <returns>A local cell origin. Equal-sized neighbors yield their complete slot after half-overlap; an internal move retains its old slot until then. Empty cells and mixed sizes use ordinary cell snapping.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The column count is outside 4 through 16.</exception>
    public static (int Column, int Row) SnapMove(IEnumerable<Tile> tiles, Tile moving, double x, double y, double cell, int columns)
    {
        var snapped = Snap(x, y, cell, moving.Size, columns);
        var placements = Pack(tiles, columns);
        var (width, height) = Dimensions(moving.Size);
        var conflicts = placements.Where(p => p.Tile.Id != moving.Id && p.Column < snapped.Column + width && p.Column + p.Width > snapped.Column
            && p.Row < snapped.Row + height && p.Row + p.Height > snapped.Row).ToArray();
        if (conflicts.Length == 0 || conflicts.Any(p => p.Width != width || p.Height != height)) return snapped;
        var left = Math.Clamp(x / cell, 0, columns - width); var top = Math.Clamp(y / cell, 0, 200);
        var recipient = conflicts.Select(p => (Placement: p, Area: Math.Max(0, Math.Min(left + width, p.Column + p.Width) - Math.Max(left, p.Column))
            * Math.Max(0, Math.Min(top + height, p.Row + p.Height) - Math.Max(top, p.Row))))
            .OrderByDescending(p => p.Area).ThenBy(p => p.Placement.Row).ThenBy(p => p.Placement.Column).First();
        if (recipient.Area >= width * height * .5) return (recipient.Placement.Column, recipient.Placement.Row);
        var original = placements.FirstOrDefault(p => p.Tile.Id == moving.Id);
        return original.Tile == null ? snapped : (original.Column, original.Row);
    }
}
