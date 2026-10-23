namespace ReStart.Core;

/// <summary>Applies layout mutations while preserving tile identifiers and folder ownership.</summary>
public static class LayoutEditor
{
    /// <summary>Adds a new independent pin in free board space without resizing groups or displacing existing entries.</summary>
    /// <param name="document">Document receiving the new pin.</param>
    /// <param name="tile">New tile, not already present in the document.</param>
    /// <param name="availableColumns">Number of group columns in the current viewport.</param>
    /// <param name="cell">Tile-cell size in DIPs.</param>
    /// <param name="viewportWidth">Available board width in DIPs.</param>
    /// <param name="preferredTop">Current vertical scroll offset in DIPs.</param>
    /// <returns>The newly created independent entry.</returns>
    /// <exception cref="ArgumentException">The tile ID is already pinned in the document.</exception>
    /// <exception cref="InvalidOperationException">No free board position can be found within the supported bounds.</exception>
    public static TileGroup PinIndependent(LayoutDocument document, Tile tile, int availableColumns, double cell, double viewportWidth, double preferredTop = 0)
        => PinEntry(document, tile, availableColumns, cell, viewportWidth, preferredTop, true);

    /// <summary>Adds a tile in unused board space, using an unnamed group by default or independent placement when explicitly enabled.</summary>
    /// <param name="document">Destination layout; existing groups and tile coordinates are retained.</param>
    /// <param name="tile">New tile whose ID must not already be pinned.</param>
    /// <param name="availableColumns">Number of viewport group lanes, from 1 through 64.</param>
    /// <param name="cell">Small-cell size in DIPs.</param>
    /// <param name="viewportWidth">Usable board width in DIPs.</param>
    /// <param name="preferredTop">Preferred vertical board offset in DIPs, normally the current scroll position.</param>
    /// <returns>The new group or independent entry, according to the saved placement preference.</returns>
    /// <exception cref="ArgumentException">The tile ID already exists.</exception>
    /// <exception cref="InvalidOperationException">No unused position can be found within layout bounds.</exception>
    public static TileGroup PinOnBoard(LayoutDocument document, Tile tile, int availableColumns, double cell, double viewportWidth, double preferredTop = 0)
        => PinEntry(document, tile, availableColumns, cell, viewportWidth, preferredTop, document.Preferences.FreeTilePlacement);

    private static TileGroup PinEntry(LayoutDocument document, Tile tile, int availableColumns, double cell, double viewportWidth, double preferredTop, bool independent)
    {
        if (Find(document, tile.Id) != null) throw new ArgumentException("The tile is already pinned.", nameof(tile));
        var placed = GroupLayout.Pack(document.Groups, availableColumns, document.Preferences.Columns, cell, preserveColumns: true);
        var (w, h) = TileLayout.Dimensions(tile.Size); var width = w * cell; var height = h * cell + (independent ? 0 : GroupLayout.HeaderHeight);
        var origin = independent ? GroupLayout.HeaderHeight : 0;
        var half = cell / 2; var gap = GroupLayout.Gap;
        var preferred = Math.Max(0, Math.Ceiling((preferredTop - origin) / half));
        var rows = placed.Select(p => Math.Max(preferred, Math.Ceiling((p.Top + p.Height + gap - origin) / half))).Append(preferred).Distinct().Order();
        var xs = placed.Select(p => Math.Ceiling((p.Left + p.Width + gap) / half) * half).Append(0).Distinct().Order()
            .Where(x => x + width <= Math.Max(width, viewportWidth)).ToArray();
        foreach (var row in rows)
            foreach (var left in xs)
            {
                var top = origin + row * half;
                if (placed.Any(p => left < p.Left + p.Width + gap && left + width + gap > p.Left && top < p.Top + p.Height + gap && top + height + gap > p.Top)) continue;
                foreach (var p in placed) { p.Group.Column = p.Column; p.Group.Row = p.Row; }
                tile.Column = 0; tile.Row = 0;
                var column = Math.Clamp((int)(left / (document.Preferences.Columns * cell + gap)), 0, 63);
                var entry = new TileGroup { IsStandalone = independent, Name = "", Column = column, Row = (int)row, X = left / cell, Tiles = [tile] };
                document.Groups.Add(entry); return entry;
            }
        throw new InvalidOperationException("No free board position was found.");
    }

    /// <summary>Detaches a root or folder child into a new board entry, preserving its identity and freezing existing visible anchors.</summary>
    /// <param name="document">Layout to mutate in memory; the caller is responsible for saving it.</param>
    /// <param name="tile">Existing root tile or one-level folder child, identified by its stable ID.</param>
    /// <param name="column">Requested zero-based board lane.</param>
    /// <param name="row">Requested board row in half-small-cell units.</param>
    /// <param name="tileColumn">Horizontal tile offset within the independent entry, in small cells.</param>
    /// <param name="availableColumns">Number of available board lanes, from 1 through 64.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <param name="x">Optional precise group origin in small-cell units; null uses the lane.</param>
    /// <param name="independent">True creates an independent entry; false creates an unnamed group with its ordinary header. Existing entries are not converted.</param>
    /// <returns>The new board entry containing the original tile identity.</returns>
    public static TileGroup Detach(LayoutDocument document, Tile tile, int column, int row, int tileColumn, int availableColumns, double cell, double? x = null, bool independent = true)
    {
        var source = document.Groups.FirstOrDefault(g => g.Tiles.Any(t => t == tile || t.Children.Contains(tile)));
        foreach (var p in GroupLayout.Pack(document.Groups, availableColumns, document.Preferences.Columns, cell, preserveColumns: true))
        { p.Group.Column = p.Column; p.Group.Row = p.Row; }
        Remove(document, tile.Id);
        if (source?.Tiles.Count == 0) document.Groups.Remove(source);
        tile.Column = Math.Clamp(tileColumn, 0, document.Preferences.Columns - TileLayout.Dimensions(tile.Size).Width);
        tile.Row = 0;
        var entry = new TileGroup { IsStandalone = independent, Name = "", Column = column, Row = row, X = x, Tiles = [tile] };
        document.Groups.Add(entry);
        GroupLayout.Move(document, entry, column, row, availableColumns, cell, x);
        return entry;
    }

    /// <summary>Unpacks a root folder; independent children keep independent board entries rather than acquiring a group header.</summary>
    /// <param name="document">Layout to mutate in memory; the caller is responsible for saving it.</param>
    /// <param name="folder">Existing root folder to replace with its original child tiles; the folder container is removed.</param>
    public static void UnpackFolder(LayoutDocument document, Tile folder)
    {
        var owner = document.Groups.First(g => g.Tiles.Contains(folder));
        if (!owner.IsStandalone)
        {
            var index = owner.Tiles.IndexOf(folder);
            owner.Tiles.Remove(folder);
            owner.Tiles.InsertRange(index, folder.Children);
            return;
        }
        var children = TileLayout.Pack(folder.Children, document.Preferences.Columns);
        document.Groups.Remove(owner);
        foreach (var p in children)
        {
            p.Tile.Column = p.Column; p.Tile.Row = 0;
            document.Groups.Add(new() { IsStandalone = true, Name = "", X = owner.X, Column = owner.Column, Row = owner.Row.HasValue ? Math.Min(20000, owner.Row.Value + p.Row * 2) : null, Tiles = [p.Tile] });
        }
    }

    /// <summary>Finds a root tile or one-level folder child by its stable layout identifier.</summary>
    /// <param name="document">Original layout to inspect without modifying its groups or preferences.</param>
    /// <param name="id">Stable layout tile ID, not an application ID.</param>
    /// <returns>The root or folder-child tile with that ID, or null when absent.</returns>
    public static Tile? Find(LayoutDocument document, string id) => document.Groups.SelectMany(g => g.Tiles)
        .SelectMany(t => new[] { t }.Concat(t.Children)).FirstOrDefault(t => t.Id == id);

    /// <summary>Removes a tile and removes its parent folder if that folder becomes empty.</summary>
    /// <param name="document">Layout to mutate in memory; the caller is responsible for saving it.</param>
    /// <param name="id">Stable root or folder-child tile ID; an absent ID has no effect.</param>
    public static void Remove(LayoutDocument document, string id)
    {
        foreach (var group in document.Groups.ToArray())
        {
            if (group.Tiles.RemoveAll(t => t.Id == id) > 0)
            {
                if (group.IsStandalone) document.Groups.Remove(group);
                return;
            }
            foreach (var folder in group.Tiles.ToList())
            {
                if (folder.Children.RemoveAll(t => t.Id == id) == 0) continue;
                if (folder.Children.Count == 0) group.Tiles.Remove(folder);
                if (group.IsStandalone && group.Tiles.Count == 0) document.Groups.Remove(group);
                return;
            }
        }
    }

    /// <summary>Moves a tile into a group in list order and clears its previous explicit coordinates.</summary>
    /// <param name="document">Layout to mutate in memory; the caller is responsible for saving it.</param>
    /// <param name="tile">Existing root tile or one-level folder child, identified by its stable ID.</param>
    /// <param name="group">Existing non-independent destination group.</param>
    /// <param name="before">Existing destination root to insert before; null or an absent root appends the tile.</param>
    /// <exception cref="ArgumentException">The destination is absent from the document or is an independent entry.</exception>
    public static void Move(LayoutDocument document, Tile tile, TileGroup group, Tile? before = null)
    {
        if (!document.Groups.Contains(group)) throw new ArgumentException("Group does not belong to the document.", nameof(group));
        if (group.IsStandalone) throw new ArgumentException("An independent tile is not a destination group.", nameof(group));
        if (tile == before) return;
        var source = Owner(document, tile);
        Remove(document, tile.Id);
        tile.Column = null; tile.Row = null;
        var index = before == null ? group.Tiles.Count : group.Tiles.IndexOf(before);
        group.Tiles.Insert(index < 0 ? group.Tiles.Count : index, tile);
        PruneSource(document, source, group);
    }

    /// <summary>Moves a non-folder source into a root target, creating a one-level folder when necessary.</summary>
    /// <param name="document">Layout to mutate in memory; the caller is responsible for saving it.</param>
    /// <param name="source">Non-folder tile to move into the target.</param>
    /// <param name="target">Root tile to turn into a folder, or an existing root folder to receive the source.</param>
    public static void MergeFolder(LayoutDocument document, Tile source, Tile target)
    {
        if (source == target || source.IsFolder || target.Children.Contains(source)) return;
        var group = document.Groups.FirstOrDefault(g => g.Tiles.Contains(target));
        if (group == null) return;
        var column = target.Column;
        var row = target.Row;
        var sourceGroup = Owner(document, source);
        Remove(document, source.Id);
        source.Column = null; source.Row = null;
        if (target.IsFolder) target.Children.Add(source);
        else
        {
            var index = group.Tiles.IndexOf(target);
            target.Column = null; target.Row = null;
            group.Tiles[index] = new() { Label = "", Size = TileSize.Medium, Column = column, Row = row, Children = [target, source] };
        }
        PruneSource(document, sourceGroup, group);
    }

    /// <summary>Moves a non-folder tile into an open root folder, preserving identity and resolving child-cell collisions.</summary>
    /// <param name="document">Layout to mutate in memory; the caller is responsible for saving it.</param>
    /// <param name="tile">Existing non-folder tile to move, including a child already in this folder.</param>
    /// <param name="folder">Existing root folder receiving the child.</param>
    /// <param name="column">Requested folder-local small-cell column, clamped to fit the tile width.</param>
    /// <param name="row">Requested folder-local small-cell row, clamped to 0 through 200.</param>
    /// <exception cref="ArgumentException">The source is a folder or the destination is not an existing root folder.</exception>
    public static void MoveIntoFolder(LayoutDocument document, Tile tile, Tile folder, int column, int row)
    {
        if (tile.IsFolder || tile == folder || !document.Groups.Any(g => g.Tiles.Contains(folder)) || !folder.IsFolder)
            throw new ArgumentException("The destination must be a root folder and the source must not be a folder.", nameof(folder));
        var columns = document.Preferences.Columns;
        var source = Owner(document, tile);
        var (width, height) = TileLayout.Dimensions(tile.Size);
        column = Math.Clamp(column, 0, columns - width); row = Math.Clamp(row, 0, 200);
        var previous = TileLayout.Pack(folder.Children, columns);
        foreach (var p in previous) { p.Tile.Column = p.Column; p.Tile.Row = p.Row; }
        if (folder.Children.Contains(tile)) folder.Children.Remove(tile);
        else Remove(document, tile.Id);
        tile.Column = column; tile.Row = row;
        folder.Children.Insert(0, tile);
        TileReflow.Apply(folder.Children, previous, [new(tile, column, row, width, height)], columns);
        PruneSource(document, source, Owner(document, folder));
    }

    /// <summary>Moves a tile to an explicit cell, preserving unaffected positions and moving conflicts into vacated or nearby space.</summary>
    /// <param name="document">Layout to mutate in memory; the caller is responsible for saving it.</param>
    /// <param name="tile">Existing root tile or one-level folder child, identified by its stable ID.</param>
    /// <param name="destination">Existing non-independent group receiving the root tile.</param>
    /// <param name="column">Requested group-local small-cell column, clamped to fit the tile width.</param>
    /// <param name="row">Requested group-local small-cell row, clamped to 0 through 200.</param>
    /// <exception cref="ArgumentException">The destination is absent from the document or is an independent entry.</exception>
    public static void MoveToCell(LayoutDocument document, Tile tile, TileGroup destination, int column, int row)
    {
        if (!document.Groups.Contains(destination)) throw new ArgumentException("Unknown group.", nameof(destination));
        if (destination.IsStandalone) throw new ArgumentException("An independent tile is not a destination group.", nameof(destination));
        var source = Owner(document, tile);
        var columns = document.Preferences.Columns;
        var (width, height) = TileLayout.Dimensions(tile.Size);
        column = Math.Clamp(column, 0, columns - width); row = Math.Clamp(row, 0, 200);
        var previous = TileLayout.Pack(destination.Tiles, columns);
        foreach (var p in previous) { p.Tile.Column = p.Column; p.Tile.Row = p.Row; }
        Remove(document, tile.Id);
        tile.Column = column; tile.Row = row;
        destination.Tiles.Insert(0, tile);
        TileReflow.Apply(destination.Tiles, previous, [new(tile, column, row, width, height)], columns);
        PruneSource(document, source, destination);
    }

    private static TileGroup? Owner(LayoutDocument document, Tile tile) => document.Groups.FirstOrDefault(g => g.Tiles.Any(t => t == tile || t.Children.Contains(tile)));
    private static void PruneSource(LayoutDocument document, TileGroup? source, TileGroup? destination)
    {
        if (source != destination && source?.Tiles.Count == 0) document.Groups.Remove(source);
    }
}
