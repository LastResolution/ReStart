namespace ReStart.Core;

/// <summary>Resolves occupied-cell moves locally, reusing space released by the same move before extending a container.</summary>
internal static class TileReflow
{
    internal static void Apply(List<Tile> target, IReadOnlyList<TilePlacement> previous,
        IReadOnlyList<TilePlacement> moving, int columns)
    {
        var ids = moving.Select(p => p.Tile.Id).ToHashSet();
        var retained = target.Select(t => t.Id).ToHashSet();
        var occupied = new HashSet<(int X, int Y)>();
        static IEnumerable<(int X, int Y)> Cells(TilePlacement p)
        {
            for (var y = p.Row; y < p.Row + p.Height; y++)
                for (var x = p.Column; x < p.Column + p.Width; x++) yield return (x, y);
        }
        void Place(Tile tile, int column, int row)
        {
            tile.Column = column; tile.Row = row;
            var (width, height) = TileLayout.Dimensions(tile.Size);
            occupied.UnionWith(Cells(new(tile, column, row, width, height)));
        }
        bool Free(int column, int row, int width, int height)
        {
            for (var y = row; y < row + height; y++)
                for (var x = column; x < column + width; x++)
                    if (occupied.Contains((x, y))) return false;
            return true;
        }

        foreach (var p in moving) Place(p.Tile, p.Column, p.Row);
        var displaced = new List<TilePlacement>();
        foreach (var p in previous.Where(p => retained.Contains(p.Tile.Id) && !ids.Contains(p.Tile.Id)))
        {
            if (Free(p.Column, p.Row, p.Width, p.Height)) Place(p.Tile, p.Column, p.Row);
            else displaced.Add(p);
        }

        // Only cells vacated in this destination are a source vacancy. Moving
        // from another group/folder must not exchange unrelated board entries.
        var vacancy = previous.Where(p => ids.Contains(p.Tile.Id)).SelectMany(Cells).ToHashSet();
        vacancy.ExceptWith(occupied);
        var widthLimit = Math.Clamp(previous.Concat(moving).Select(p => p.Column + p.Width).DefaultIfEmpty(4).Max(), 1, columns);
        var bottom = previous.Concat(moving).Select(p => p.Row + p.Height).DefaultIfEmpty(0).Max();
        var searchBottom = bottom + displaced.Sum(p => p.Height);
        foreach (var p in displaced.OrderBy(p => p.Row).ThenBy(p => p.Column))
        {
            var vacated = vacancy.OrderBy(c => c.Y).ThenBy(c => c.X).Where(c => c.X + p.Width <= columns)
                .FirstOrDefault(c => Free(c.X, c.Y, p.Width, p.Height)
                    && Cells(new(p.Tile, c.X, c.Y, p.Width, p.Height)).All(vacancy.Contains), (X: -1, Y: -1));
            if (vacated != (-1, -1)) { Place(p.Tile, vacated.X, vacated.Y); continue; }

            // Keep the content width stable. Find nearby room inside its
            // existing extent before adding rows; do not jump to a spare lane.
            (int Column, int Row)? best = null;
            var bestScore = (Growth: int.MaxValue, Distance: int.MaxValue, Row: int.MaxValue, Column: int.MaxValue);
            for (var row = 0; row <= searchBottom; row++)
                for (var col = 0; col <= Math.Max(widthLimit, p.Width) - p.Width; col++)
                {
                    if (!Free(col, row, p.Width, p.Height)) continue;
                    var score = (Growth: Math.Max(0, row + p.Height - bottom),
                        Distance: Math.Abs(col - p.Column) + Math.Abs(row - p.Row), Row: row, Column: col);
                    if (score.CompareTo(bestScore) >= 0) continue;
                    best = (col, row); bestScore = score;
                }
            var cell = best ?? throw new InvalidOperationException("No free tile position was found.");
            Place(p.Tile, cell.Column, cell.Row);
        }
    }
}
