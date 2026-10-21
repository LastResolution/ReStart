namespace ReStart.Core;

/// <summary>Describes a group anchor, logical height and exact visible height, excluding trailing spacing.</summary>
/// <param name="Group">Original group referenced by this placement.</param>
/// <param name="Column">Zero-based board lane used as the legacy horizontal anchor.</param>
/// <param name="Row">Board anchor row in half-small-cell units.</param>
/// <param name="Rows">Occupied height rounded to half-small-cell rows.</param>
/// <param name="Height">Exact visible group height in DIPs, excluding trailing spacing.</param>
/// <param name="Left">Absolute visible left edge in DIPs.</param>
/// <param name="Width">Content-sized rectangular width in DIPs.</param>
/// <param name="Top">Absolute visible top edge in DIPs.</param>
public readonly record struct GroupPlacement(TileGroup Group, int Column, int Row, int Rows, double Height, double Left, double Width, double Top);

/// <summary>Places groups independently in columns, preserving explicit gaps and displacing only overlapping groups.</summary>
public static class GroupLayout
{
    /// <summary>Height of an inline group header in device-independent pixels.</summary>
    public const double HeaderHeight = 40;
    /// <summary>Space between adjacent groups in device-independent pixels.</summary>
    public const double Gap = 24;

    /// <summary>Returns the number of complete group columns available in a viewport, with at least one column.</summary>
    /// <param name="width">Available viewport width in DIPs.</param>
    /// <param name="tileColumns">Width of a group in small tile cells, normally 6 or 8.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <returns>A lane count clamped to 1 through 64.</returns>
    public static int ColumnCount(double width, int tileColumns, double cell) =>
        Math.Clamp((int)((Math.Max(0, width) + Gap) / (tileColumns * cell + Gap)), 1, 64);

    /// <summary>Rounds a canvas point to a group anchor, accounting for the original pointer grab offset.</summary>
    /// <param name="x">Pointer-adjusted horizontal board offset in DIPs.</param>
    /// <param name="y">Pointer-adjusted vertical anchor offset in DIPs.</param>
    /// <param name="columns">Number of available board lanes, from 1 through 64.</param>
    /// <param name="tileColumns">Width of a group in small tile cells, normally 6 or 8.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <returns>Clamped board lane and half-cell row anchors.</returns>
    public static (int Column, int Row) Snap(double x, double y, int columns, int tileColumns, double cell) =>
        (Math.Clamp((int)Math.Round(x / (tileColumns * cell + Gap), MidpointRounding.AwayFromZero), 0, columns - 1),
         Math.Clamp((int)Math.Round(y / (cell / 2), MidpointRounding.AwayFromZero), 0, 20000));

    /// <summary>Snaps beside a nearby visible edge, retaining group spacing and the ordinary cell gap between independent tiles.</summary>
    /// <param name="placements">Visible placements used as stationary alignment candidates.</param>
    /// <param name="moving">Group being moved; its own placement is excluded.</param>
    /// <param name="left">Proposed visible left edge in DIPs.</param>
    /// <param name="top">Proposed visible top edge in DIPs.</param>
    /// <param name="width">Moving group visible width in DIPs.</param>
    /// <param name="height">Moving group visible height in DIPs.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <returns>The nearest adjacent X position in DIPs, or the original left value if no edge is close.</returns>
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
    /// <param name="placements">Visible placements used as stationary alignment candidates.</param>
    /// <param name="moving">Group being moved; its own placement is excluded.</param>
    /// <param name="left">Proposed visible left edge in DIPs.</param>
    /// <param name="top">Proposed visible top edge in DIPs.</param>
    /// <param name="width">Moving group visible width in DIPs.</param>
    /// <param name="height">Moving group visible height in DIPs.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <returns>The nearest aligned X position in DIPs, or the original left value if no alignment is close.</returns>
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
    /// <param name="placements">Visible placements used as stationary vertical snap candidates.</param>
    /// <param name="moving">Moving group, excluded from the candidate set.</param>
    /// <param name="left">Proposed visible left edge in DIPs.</param>
    /// <param name="row">Proposed board row in half-small-cell units.</param>
    /// <param name="width">Moving group visible width in DIPs.</param>
    /// <param name="height">Moving group visible height in DIPs.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <returns>Nearest valid half-cell anchor row, or the proposed row if no edge is close.</returns>
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
    /// <param name="placements">Visible group rectangles; independent entries are not recipients.</param>
    /// <param name="x">Drop point X in board-local DIPs.</param>
    /// <param name="y">Drop point Y in board-local DIPs.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <returns>Nearest named group within the adjacent-cell tolerance, or null when no group qualifies.</returns>
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
    /// <param name="group">Group whose visible footprint is measured.</param>
    /// <param name="tileColumns">Width of a group in small tile cells, normally 6 or 8.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <param name="expandedFolderId">Open root-folder ID, or null to measure the closed group.</param>
    /// <returns>Content-sized rectangular bounds relative to the group origin, in DIPs.</returns>
    public static IReadOnlyList<GroupArea> OccupiedAreas(TileGroup group, int tileColumns, double cell, string? expandedFolderId = null)
    {
        var tiles = TileLayout.Pack(group.Tiles, tileColumns);
        var expansion = FolderExpansion.Create(tiles, tileColumns, expandedFolderId);
        if (group.IsStandalone && tiles.Count == 1 && expansion == null)
            return [new(0, 0, tiles[0].Width * cell, tiles[0].Height * cell)];
        var width = (expansion?.WidthInCells(tiles) ?? Math.Max(1, tiles.Count == 0 ? 0 : tiles.Max(t => t.Column + t.Width))) * cell;
        var rows = Math.Max(1, tiles.Count == 0 ? 0 : tiles.Max(t => t.Row + t.Height));
        var height = (group.IsStandalone ? 0 : HeaderHeight) + rows * cell + (expansion?.ExtraHeight(cell) ?? 0);
        return [new(0, 0, width, height)];
    }

    /// <summary>Returns group bounds without mutating anchors; folder expansion temporarily pushes neighbors around its fixed opening anchor.</summary>
    /// <param name="groups">Groups to place in their existing priority/order.</param>
    /// <param name="columns">Number of available board lanes, from 1 through 64.</param>
    /// <param name="tileColumns">Width of a group in small tile cells, normally 6 or 8.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <param name="priority">Optional moving group reserved before ordinary collision resolution.</param>
    /// <param name="expandedFolderId">Open root-folder ID, or null for the closed-layout baseline.</param>
    /// <param name="preserveColumns">Whether explicit horizontal lanes must remain fixed when resolving overlaps.</param>
    /// <param name="priorityIds">Optional stable group IDs reserved ahead of ordinary groups, for example a rigid selection.</param>
    /// <returns>Visible placements referencing the input groups; stored anchors are not modified.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The board lane count is outside 1 through 64, or the cell size is nonpositive or nonfinite.</exception>
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
    /// <param name="document">Original layout to inspect without modifying its groups or preferences.</param>
    /// <param name="group">Group whose board anchor is being moved.</param>
    /// <param name="column">Requested zero-based board lane.</param>
    /// <param name="row">Requested board row in half-small-cell units.</param>
    /// <param name="availableColumns">Number of available board lanes, from 1 through 64.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <param name="x">Optional precise horizontal group origin in small-cell units; null uses the lane anchor.</param>
    /// <returns>Proposed placements without changing the source document.</returns>
    /// <exception cref="ArgumentException">The moving group is not in the source document.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An explicit X origin is nonfinite or outside 0 through 10000 cells, or packing dimensions are invalid.</exception>
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
    /// <param name="document">Layout to mutate in memory; the caller is responsible for saving it.</param>
    /// <param name="group">Group whose board anchor is being moved.</param>
    /// <param name="column">Requested zero-based board lane.</param>
    /// <param name="row">Requested board row in half-small-cell units.</param>
    /// <param name="availableColumns">Number of available board lanes, from 1 through 64.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <param name="x">Optional precise horizontal group origin in small-cell units; null uses the lane anchor.</param>
    /// <exception cref="ArgumentException">The moving group is not in the source document.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The requested X origin or packing dimensions are invalid.</exception>
    public static void Move(LayoutDocument document, TileGroup group, int column, int row, int availableColumns, double cell, double? x = null)
    {
        foreach (var p in PreviewMove(document, group, column, row, availableColumns, cell, x))
        { p.Group.Column = p.Column; p.Group.Row = p.Row; }
        group.X = x;
    }
}

/// <summary>One occupied local rectangle used consistently for group collision and native hit testing.</summary>
/// <param name="Left">Left offset relative to the group origin, in DIPs.</param>
/// <param name="Top">Top offset relative to the group origin, in DIPs.</param>
/// <param name="Width">Visible rectangular width in DIPs.</param>
/// <param name="Height">Visible rectangular height in DIPs.</param>
public readonly record struct GroupArea(double Left, double Top, double Width, double Height)
{
    /// <summary>Gets the exclusive right edge.</summary>
    public double Right => Left + Width;
    /// <summary>Gets the exclusive bottom edge.</summary>
    public double Bottom => Top + Height;
}
