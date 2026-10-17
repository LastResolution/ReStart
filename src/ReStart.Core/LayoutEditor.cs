namespace ReStart.Core;

/// <summary>Applies layout mutations while preserving tile identifiers and folder ownership.</summary>
public static class LayoutEditor
{
    /// <summary>Detaches a root or folder child as one independent board tile, preserving its identity and freezing existing visible anchors.</summary>
    public static TileGroup Detach(LayoutDocument document, Tile tile, int column, int row, int tileColumn, int availableColumns, double cell, double? x = null)
    {
        var source = document.Groups.FirstOrDefault(g => g.Tiles.Any(t => t == tile || t.Children.Contains(tile)));
        foreach (var p in GroupLayout.Pack(document.Groups, availableColumns, document.Preferences.Columns, cell, preserveColumns: true))
        { p.Group.Column = p.Column; p.Group.Row = p.Row; }
        Remove(document, tile.Id);
        if (source?.Tiles.Count == 0) document.Groups.Remove(source);
        tile.Column = Math.Clamp(tileColumn, 0, document.Preferences.Columns - TileLayout.Dimensions(tile.Size).Width);
        tile.Row = 0;
        var entry = new TileGroup { IsStandalone = true, Name = "", Column = column, Row = row, X = x, Tiles = [tile] };
        document.Groups.Add(entry);
        GroupLayout.Move(document, entry, column, row, availableColumns, cell, x);
        return entry;
    }

    /// <summary>Unpacks a root folder; independent children keep independent board entries rather than acquiring a group header.</summary>
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
    public static Tile? Find(LayoutDocument document, string id) => document.Groups.SelectMany(g => g.Tiles)
        .SelectMany(t => new[] { t }.Concat(t.Children)).FirstOrDefault(t => t.Id == id);

    /// <summary>Removes a tile and removes its parent folder if that folder becomes empty.</summary>
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
        foreach (var p in previous.Where(p => p.Tile != tile))
            if (p.Column < column + width && p.Column + p.Width > column && p.Row < row + height && p.Row + p.Height > row)
            { p.Tile.Column = null; p.Tile.Row = null; }
        folder.Children.Insert(0, tile);
        foreach (var p in TileLayout.Pack(folder.Children, columns)) { p.Tile.Column = p.Column; p.Tile.Row = p.Row; }
        PruneSource(document, source, Owner(document, folder));
    }

    /// <summary>Moves a tile to an explicit cell, preserving unaffected positions and repacking only conflicting tiles.</summary>
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
        foreach (var p in previous.Where(p => p.Tile != tile))
            if (p.Column < column + width && p.Column + p.Width > column && p.Row < row + height && p.Row + p.Height > row)
            { p.Tile.Column = null; p.Tile.Row = null; }
        destination.Tiles.Insert(0, tile);
        foreach (var p in TileLayout.Pack(destination.Tiles, columns)) { p.Tile.Column = p.Column; p.Tile.Row = p.Row; }
        PruneSource(document, source, destination);
    }

    private static TileGroup? Owner(LayoutDocument document, Tile tile) => document.Groups.FirstOrDefault(g => g.Tiles.Any(t => t == tile || t.Children.Contains(tile)));
    private static void PruneSource(LayoutDocument document, TileGroup? source, TileGroup? destination)
    {
        if (source != destination && source?.Tiles.Count == 0) document.Groups.Remove(source);
    }
}
