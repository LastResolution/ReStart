namespace ReStart.Core;

/// <summary>Describes a group anchor, logical height and exact visible height, excluding trailing spacing.</summary>
public readonly record struct GroupPlacement(TileGroup Group, int Column, int Row, int Rows, double Height, double Left, double Width, double Top);

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

    /// <summary>Snaps beside a nearby visible edge, retaining group spacing and the ordinary cell gap between independent tiles.</summary>
    public static double SnapBeside(IEnumerable<GroupPlacement> placements, TileGroup moving, double left, double top, double width, double height, double cell)
    {
        var candidates = placements.Where(p => p.Group != moving && top < p.Top + p.Height && top + height > p.Top)
            .SelectMany(p =>
            {
                var gap = moving.IsStandalone && p.Group.IsStandalone ? 0 : Gap;
                return new[] { p.Left + p.Width + gap, p.Left - width - gap };
            })
            .Where(x => x >= 0 && Math.Abs(x - left) <= Gap + cell / 4)
            .OrderBy(x => Math.Abs(x - left)).ThenBy(x => x).ToArray();
        return candidates.Length == 0 ? left : candidates[0];
    }

    /// <summary>Aligns nearby left/right edges without rounding away a lane's non-grid origin; preserves vertical drag alignment when isolated.</summary>
    public static double SnapAlignedX(IEnumerable<GroupPlacement> placements, TileGroup moving, double left, double top, double width, double height, double cell)
    {
        var items = placements.ToArray();
        var candidates = items.Where(p => p.Group != moving && (top >= p.Top + p.Height || p.Top >= top + height)
                && Math.Max(0, Math.Max(p.Top - top - height, top - p.Top - p.Height)) <= cell)
            .SelectMany(p => new[] { p.Left, p.Left + p.Width - width });
        double? Closest(IEnumerable<double> values) => values.Where(x => x >= 0 && Math.Abs(x - left) <= cell / 4)
            .OrderBy(x => Math.Abs(x - left)).ThenBy(x => x).Select(x => (double?)x).FirstOrDefault();
        return Closest(candidates) ?? Closest(items.Where(p => p.Group == moving).Select(p => p.Left)) ?? left;
    }

    /// <summary>Snaps above or below a nearby group boundary, rounding outward to retain at least the group gap.</summary>
    public static int SnapRow(IEnumerable<GroupPlacement> placements, TileGroup moving, double left, int row, double width, double height, double cell)
    {
        var offset = moving.IsStandalone ? HeaderHeight : 0; var half = cell / 2;
        var top = offset + row * half;
        var candidates = placements.Where(p => p.Group != moving && !(moving.IsStandalone && p.Group.IsStandalone)
                && left < p.Left + p.Width && left + width > p.Left)
            .SelectMany(p => new[] { (int)Math.Ceiling((p.Top + p.Height + Gap - offset) / half),
                (int)Math.Floor((p.Top - height - Gap - offset) / half) })
            .Where(r => r is >= 0 and <= 20000 && Math.Abs(offset + r * half - top) <= Gap + half)
            .OrderBy(r => Math.Abs(r - row)).ThenBy(r => r).ToArray();
        return candidates.Length == 0 ? row : candidates[0];
    }

    /// <summary>Finds a named group within one small cell of its visible edges for a transient adjacent-cell insertion target.</summary>
    public static TileGroup? DropRecipient(IEnumerable<GroupPlacement> placements, double x, double y, double cell)
    {
        return placements.Where(p => !p.Group.IsStandalone)
            .Select(p => (p.Group, Dx: Math.Max(0, Math.Max(p.Left - x, x - p.Left - p.Width)),
                Dy: Math.Max(0, Math.Max(p.Top - y, y - p.Top - p.Height))))
            .Where(p => p.Dx <= cell && p.Dy <= cell)
            .OrderBy(p => p.Dx * p.Dx + p.Dy * p.Dy).ThenBy(p => p.Group.Id, StringComparer.Ordinal)
            .Select(p => p.Group).FirstOrDefault();
    }

    /// <summary>Returns the content-sized rectangular group footprint, including its header and any expanded folder.</summary>
    public static IReadOnlyList<GroupArea> OccupiedAreas(TileGroup group, int tileColumns, double cell, string? expandedFolderId = null)
    {
        var tiles = TileLayout.Pack(group.Tiles, tileColumns);
        var expansion = FolderExpansion.Create(tiles, tileColumns, expandedFolderId);
        if (group.IsStandalone && tiles.Count == 1 && expansion == null)
            return [new(0, 0, tiles[0].Width * cell, tiles[0].Height * cell)];
        var width = (expansion != null ? tileColumns : Math.Max(1, tiles.Count == 0 ? 0 : tiles.Max(t => t.Column + t.Width))) * cell;
        var rows = Math.Max(1, tiles.Count == 0 ? 0 : tiles.Max(t => t.Row + t.Height));
        var height = (group.IsStandalone ? 0 : HeaderHeight) + rows * cell + (expansion?.ExtraHeight(cell) ?? 0);
        return [new(0, 0, width, height)];
    }

    /// <summary>Returns group bounds without mutating anchors; folder expansion temporarily pushes neighbors around its fixed opening anchor.</summary>
    public static IReadOnlyList<GroupPlacement> Pack(IEnumerable<TileGroup> groups, int columns, int tileColumns, double cell, TileGroup? priority = null, string? expandedFolderId = null, bool preserveColumns = false, IReadOnlySet<string>? priorityIds = null)
    {
        if (columns is < 1 or > 64 || !double.IsFinite(cell) || cell <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        var items = groups.ToArray();
        var owner = expandedFolderId == null ? null : items.FirstOrDefault(g => g.Tiles.Any(t => t.Id == expandedFolderId && t.IsFolder));
        if (owner == null) return PackCore(items, columns, tileColumns, cell, priority, null, preserveColumns, priorityIds);
        var baseline = PackCore(items, columns, tileColumns, cell, null, null, preserveColumns);
        var shadows = baseline.Select(p => new TileGroup
        {
            IsStandalone = p.Group.IsStandalone,
            Tiles = p.Group.Tiles,
            Column = p.Column,
            Row = p.Row,
            X = p.Group.X ?? p.Column * (tileColumns * cell + Gap) / cell
        }).ToArray();
        var originals = shadows.Select((shadow, i) => (shadow, original: baseline[i].Group)).ToDictionary(p => p.shadow, p => p.original);
        var opening = shadows[Array.FindIndex(items, g => g == owner)];
        return PackCore(shadows, columns, tileColumns, cell, opening, expandedFolderId, true)
            .Select(p => p with { Group = originals[p.Group] }).ToArray();
    }

    private static IReadOnlyList<GroupPlacement> PackCore(TileGroup[] items, int columns, int tileColumns, double cell, TileGroup? priority, string? expandedFolderId, bool preserveColumns, IReadOnlySet<string>? priorityIds = null)
    {
        var placed = new Dictionary<TileGroup, GroupPlacement>();
        var regions = items.ToDictionary(g => g, g => OccupiedAreas(g, tileColumns, cell, expandedFolderId));
        (double Offset, double Width, double Height) Bounds(TileGroup group)
        {
            var tiles = TileLayout.Pack(group.Tiles, tileColumns);
            var compact = group.IsStandalone && tiles.Count == 1 && FolderExpansion.Create(tiles, tileColumns, expandedFolderId) == null;
            var areas = regions[group];
            return (compact ? tiles[0].Column * cell : 0, areas.Max(a => a.Right), areas.Max(a => a.Bottom));
        }
        int FreeRow(TileGroup group, double left, int row, double topOffset, bool automatic = false)
        {
            var localAreas = automatic ? new[] { new GroupArea(0, 0, Bounds(group).Width, Bounds(group).Height) } : regions[group];
            var expanding = expandedFolderId != null && group.Tiles.Any(t => t.Id == expandedFolderId);
            while (true)
            {
                var top = topOffset + row * cell / 2;
                var collisions = from a in localAreas
                                 from p in placed.Values
                                 from b in automatic ? new[] { new GroupArea(0, 0, p.Width, p.Height) } : regions[p.Group]
                                 let spacing = automatic || expanding || expandedFolderId != null && p.Group.Tiles.Any(t => t.Id == expandedFolderId) ? Gap : 0
                                 where left + a.Left < p.Left + b.Right && left + a.Right > p.Left + b.Left
                                    && top + a.Top < p.Top + b.Bottom + spacing && top + a.Bottom + spacing > p.Top + b.Top
                                 select (int)Math.Ceiling((p.Top + b.Bottom + spacing - a.Top - topOffset) / (cell / 2));
                var next = collisions.DefaultIfEmpty(row).Max();
                if (next <= row) return row;
                row = next;
            }
        }
        foreach (var group in items.Where(g => g.Column.HasValue && g.Row.HasValue).OrderByDescending(g => g == priority || priorityIds?.Contains(g.Id) == true)
                     .ThenBy(g => (g.IsStandalone ? HeaderHeight : 0) + g.Row!.Value * cell / 2))
        {
            var column = Math.Clamp(group.Column!.Value, 0, preserveColumns ? 63 : columns - 1);
            var (offset, width, height) = Bounds(group);
            var left = (group.X * cell ?? column * (tileColumns * cell + Gap)) + offset;
            var topOffset = group.IsStandalone ? HeaderHeight : 0;
            var row = FreeRow(group, left, Math.Max(0, group.Row!.Value), topOffset);
            placed[group] = new(group, column, row, (int)Math.Ceiling(height / (cell / 2)), height, left, width, topOffset + row * cell / 2);
        }
        foreach (var group in items.Where(g => !placed.ContainsKey(g)))
        {
            var (offset, width, height) = Bounds(group);
            var topOffset = group.IsStandalone ? HeaderHeight : 0;
            var target = Enumerable.Range(0, columns).Select(c => (Column: c, Left: c * (tileColumns * cell + Gap) + offset))
                .Select(p => (p.Column, p.Left, Row: FreeRow(group, p.Left, 0, topOffset, true))).OrderBy(p => p.Row).ThenBy(p => p.Column).First();
            placed[group] = new(group, target.Column, target.Row, (int)Math.Ceiling(height / (cell / 2)), height, target.Left, width, topOffset + target.Row * cell / 2);
        }
        return items.Select(g => placed[g]).ToArray();
    }

    /// <summary>Calculates the exact proposed drop layout without modifying group anchors or any tile contents.</summary>
    public static IReadOnlyList<GroupPlacement> PreviewMove(LayoutDocument document, TileGroup group, int column, int row, int availableColumns, double cell, double? x = null)
    {
        if (!document.Groups.Contains(group)) throw new ArgumentException("Unknown group.", nameof(group));
        if (x.HasValue && (!double.IsFinite(x.Value) || x is < 0 or > 10000)) throw new ArgumentOutOfRangeException(nameof(x));
        var current = Pack(document.Groups, availableColumns, document.Preferences.Columns, cell, preserveColumns: true);
        var shadows = current.Select(p => new TileGroup
        {
            IsStandalone = p.Group.IsStandalone,
            X = p.Group == group ? x : p.Group.X,
            Column = p.Group == group ? Math.Clamp(column, 0, availableColumns - 1) : p.Column,
            Row = p.Group == group ? Math.Clamp(row, 0, 20000) : p.Row,
            Tiles = p.Group.Tiles
        }).ToArray();
        var originals = shadows.Select((shadow, i) => (shadow, original: current[i].Group)).ToDictionary(p => p.shadow, p => p.original);
        var priority = shadows[Array.FindIndex(current.ToArray(), p => p.Group == group)];
        return Pack(shadows, availableColumns, document.Preferences.Columns, cell, priority, preserveColumns: true)
            .Select(p => p with { Group = originals[p.Group] }).ToArray();
    }

    /// <summary>Commits the proposed group layout while preserving every tile's identity, size and local cell coordinates.</summary>
    public static void Move(LayoutDocument document, TileGroup group, int column, int row, int availableColumns, double cell, double? x = null)
    {
        foreach (var p in PreviewMove(document, group, column, row, availableColumns, cell, x))
        { p.Group.Column = p.Column; p.Group.Row = p.Row; }
        group.X = x;
    }
}

/// <summary>One occupied local rectangle used consistently for group collision and native hit testing.</summary>
public readonly record struct GroupArea(double Left, double Top, double Width, double Height)
{
    /// <summary>Gets the exclusive right edge.</summary>
    public double Right => Left + Width;
    /// <summary>Gets the exclusive bottom edge.</summary>
    public double Bottom => Top + Height;
}
