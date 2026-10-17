namespace ReStart.Core;

/// <summary>Describes a group anchor, logical height and exact visible height, excluding trailing spacing.</summary>
public readonly record struct GroupPlacement(TileGroup Group, int Column, int Row, int Rows, double Height);

/// <summary>Places groups independently in columns, preserving explicit gaps and displacing only overlapping groups.</summary>
public static class GroupLayout
{
    /// <summary>Height of an inline group header in device-independent pixels.</summary>
    public const double HeaderHeight = 40;
    /// <summary>Space between adjacent groups in device-independent pixels.</summary>
    public const double Gap = 24;

    /// <summary>Returns the number of complete group columns available in a viewport, with at least one column.</summary>
    public static int ColumnCount(double width, int tileColumns, double cell) =>
        Math.Clamp((int)((Math.Max(0, width) + Gap) / (tileColumns * cell + Gap)), 1, 64);

    /// <summary>Rounds a canvas point to a group anchor, accounting for the original pointer grab offset.</summary>
    public static (int Column, int Row) Snap(double x, double y, int columns, int tileColumns, double cell) =>
        (Math.Clamp((int)Math.Round(x / (tileColumns * cell + Gap), MidpointRounding.AwayFromZero), 0, columns - 1),
         Math.Clamp((int)Math.Round(y / (cell / 2), MidpointRounding.AwayFromZero), 0, 20000));

    /// <summary>Returns group bounds without mutating stored anchors; narrow screens reflow off-screen columns for display.</summary>
    public static IReadOnlyList<GroupPlacement> Pack(IEnumerable<TileGroup> groups, int columns, int tileColumns, double cell, TileGroup? priority = null, string? expandedFolderId = null)
    {
        if (columns is < 1 or > 64 || !double.IsFinite(cell) || cell <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        var items = groups.ToArray();
        var placed = new Dictionary<TileGroup, GroupPlacement>();
        double Height(TileGroup group)
        {
            var tiles = TileLayout.Pack(group.Tiles, tileColumns);
            var rows = Math.Max(1, tiles.Count == 0 ? 0 : tiles.Max(t => t.Row + t.Height));
            var expansion = FolderExpansion.Create(tiles, tileColumns, expandedFolderId);
            return HeaderHeight + rows * cell + (expansion?.ExtraHeight(cell) ?? 0);
        }
        int FreeRow(int column, int row, double height, bool automatic = false)
        {
            var spacing = automatic ? Gap : 0;
            while (true)
            {
                var overlap = placed.Values.Where(p => p.Column == column && row * cell / 2 < p.Row * cell / 2 + p.Height + spacing
                    && row * cell / 2 + height + spacing > p.Row * cell / 2).ToArray();
                if (overlap.Length == 0) return row;
                row = (int)Math.Ceiling(overlap.Max(p => p.Row * cell / 2 + p.Height + spacing) / (cell / 2));
            }
        }
        foreach (var group in items.Where(g => g.Column.HasValue && g.Row.HasValue).OrderByDescending(g => g == priority))
        {
            var column = Math.Clamp(group.Column!.Value, 0, columns - 1);
            var height = Height(group);
            placed[group] = new(group, column, FreeRow(column, Math.Max(0, group.Row!.Value), height), (int)Math.Ceiling(height / (cell / 2)), height);
        }
        foreach (var group in items.Where(g => !placed.ContainsKey(g)))
        {
            var height = Height(group);
            var target = Enumerable.Range(0, columns).Select(c => (Column: c, Row: FreeRow(c, 0, height, true))).OrderBy(p => p.Row).ThenBy(p => p.Column).First();
            placed[group] = new(group, target.Column, target.Row, (int)Math.Ceiling(height / (cell / 2)), height);
        }
        return items.Select(g => placed[g]).ToArray();
    }

    /// <summary>Calculates the exact proposed drop layout without modifying group anchors or any tile contents.</summary>
    public static IReadOnlyList<GroupPlacement> PreviewMove(LayoutDocument document, TileGroup group, int column, int row, int availableColumns, double cell)
    {
        if (!document.Groups.Contains(group)) throw new ArgumentException("Unknown group.", nameof(group));
        var current = Pack(document.Groups, availableColumns, document.Preferences.Columns, cell);
        var shadows = current.Select(p => new TileGroup
        {
            Column = p.Group == group ? Math.Clamp(column, 0, availableColumns - 1) : p.Column,
            Row = p.Group == group ? Math.Clamp(row, 0, 20000) : p.Row,
            Tiles = p.Group.Tiles
        }).ToArray();
        var originals = shadows.Select((shadow, i) => (shadow, original: current[i].Group)).ToDictionary(p => p.shadow, p => p.original);
        var priority = shadows[Array.FindIndex(current.ToArray(), p => p.Group == group)];
        return Pack(shadows, availableColumns, document.Preferences.Columns, cell, priority)
            .Select(p => p with { Group = originals[p.Group] }).ToArray();
    }

    /// <summary>Commits the proposed group layout while preserving every tile's identity, size and local cell coordinates.</summary>
    public static void Move(LayoutDocument document, TileGroup group, int column, int row, int availableColumns, double cell)
    {
        foreach (var p in PreviewMove(document, group, column, row, availableColumns, cell))
        { p.Group.Column = p.Column; p.Group.Row = p.Row; }
    }
}
