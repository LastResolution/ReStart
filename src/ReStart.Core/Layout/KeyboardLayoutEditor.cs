namespace ReStart.Core;

/// <summary>Builds detached, lock-aware keyboard edits using the same geometry as pointer placement.</summary>
public static class KeyboardLayoutEditor
{
    /// <summary>Moves a selection by whole small cells within its current group/folder, or across the board for independent tiles.</summary>
    /// <param name="document">Source document; neither groups nor preferences are modified.</param>
    /// <param name="ids">Distinct selected tile IDs sharing one container, or all independent root tiles.</param>
    /// <param name="dx">Horizontal delta in small cells, normally -1, 0 or 1.</param>
    /// <param name="dy">Vertical delta in small cells, normally -1, 0 or 1.</param>
    /// <param name="availableColumns">Current board lane count, from 1 through 64.</param>
    /// <returns>Detached candidate groups, or null for a locked, invalid, mixed-container or out-of-bounds move.</returns>
    public static List<TileGroup>? Nudge(LayoutDocument document, IReadOnlyList<string> ids, int dx, int dy, int availableColumns)
    {
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count || Math.Abs((long)dx) > 100 || Math.Abs((long)dy) > 100) return null;
        var selected = ids.Select(id => LayoutEditor.Find(document, id)).ToArray();
        if (selected.Any(t => t == null)) return null;
        var cell = 52 * document.Preferences.TileScale;
        var owner = document.Groups.FirstOrDefault(g => g.Tiles.Any(t => t.Id == ids[0] || t.Children.Any(c => c.Id == ids[0])));
        if (owner == null) return null;
        var folder = owner.Tiles.FirstOrDefault(t => t.Children.Any(c => c.Id == ids[0]));
        var container = folder?.Children ?? owner.Tiles;
        List<TileGroup>? plan;
        if (folder != null || !owner.IsStandalone)
        {
            if (ids.Any(id => container.All(t => t.Id != id))) return null;
            var positions = TileLayout.Pack(container, document.Preferences.Columns).Where(p => ids.Contains(p.Tile.Id)).ToArray();
            if (positions.Any(p => p.Column + dx < 0 || p.Row + dy < 0 || p.Column + dx + p.Width > document.Preferences.Columns || p.Row + dy + p.Height > 201)) return null;
            var lead = positions[0];
            plan = TileBatchMove.Plan(document, positions.Select(p => new SelectedTilePosition(p.Tile.Id, p.Column, p.Row)).ToArray(), lead.Tile.Id,
                folder == null ? owner.Id : null, folder?.Id, lead.Column + dx, lead.Row + dy, availableColumns, cell);
        }
        else
        {
            var entries = GroupLayout.Pack(document.Groups, availableColumns, document.Preferences.Columns, cell, preserveColumns: true)
                .Where(p => p.Group.IsStandalone && p.Group.Tiles.Count == 1 && ids.Contains(p.Group.Tiles[0].Id)).ToArray();
            if (entries.Length != ids.Count || entries.Any(p => p.Left / cell + dx < 0 || p.Row / 2d + dy < 0)) return null;
            var lead = entries[0];
            plan = TileBatchMove.Plan(document, entries.Select(p => new SelectedTilePosition(p.Group.Tiles[0].Id, p.Left / cell, p.Row / 2d)).ToArray(),
                lead.Group.Tiles[0].Id, null, null, lead.Left / cell + dx, lead.Row / 2d + dy, availableColumns, cell);
        }
        return plan != null && LayoutLocks.AllowsChange(document.Groups, plan, document.Preferences, availableColumns) ? plan : null;
    }

    /// <summary>Changes selected tile sizes as one operation, allowing normal packing to resolve conflicts.</summary>
    /// <param name="document">Source document, which is not modified.</param>
    /// <param name="ids">Distinct selected tile IDs; a folder and its children cannot be selected together.</param>
    /// <param name="size">New small, medium, wide or large size.</param>
    /// <param name="availableColumns">Current board lane count, from 1 through 64.</param>
    /// <returns>Detached candidate groups, or null if IDs, size, selection or placement locks prohibit the change.</returns>
    public static List<TileGroup>? Resize(LayoutDocument document, IReadOnlyList<string> ids, TileSize size, int availableColumns)
    {
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count || !Enum.IsDefined(size)) return null;
        var copy = new LayoutDocument { Preferences = document.Preferences, Groups = LayoutHistory.CopyGroups(document.Groups) };
        foreach (var id in ids)
        {
            var tile = LayoutEditor.Find(copy, id);
            if (tile == null || tile.Children.Any(c => ids.Contains(c.Id))) return null;
            tile.Size = size;
        }
        return LayoutLocks.AllowsChange(document.Groups, copy.Groups, document.Preferences, availableColumns) ? copy.Groups : null;
    }

    /// <summary>Moves the containing group by whole small cells, preserving tile ownership and applying collision rules.</summary>
    /// <param name="document">Source document, which is not modified.</param>
    /// <param name="tileId">Root or folder-child tile identifying the group to move.</param>
    /// <param name="dx">Horizontal delta in small cells, normally -1, 0 or 1.</param>
    /// <param name="dy">Vertical delta in small cells, normally -1, 0 or 1.</param>
    /// <param name="availableColumns">Current board lane count, from 1 through 64.</param>
    /// <returns>Detached candidate groups, or null if the tile is unknown, out of bounds or a lock would be changed.</returns>
    public static List<TileGroup>? MoveGroup(LayoutDocument document, string tileId, int dx, int dy, int availableColumns)
    {
        if (Math.Abs((long)dx) > 100 || Math.Abs((long)dy) > 100) return null;
        var copy = new LayoutDocument { Preferences = document.Preferences, Groups = LayoutHistory.CopyGroups(document.Groups) };
        var group = copy.Groups.FirstOrDefault(g => g.Tiles.Any(t => t.Id == tileId || t.Children.Any(c => c.Id == tileId)));
        if (group == null) return null;
        var cell = 52 * copy.Preferences.TileScale;
        var placement = GroupLayout.Pack(copy.Groups, availableColumns, copy.Preferences.Columns, cell, preserveColumns: true).Single(p => p.Group == group);
        var x = placement.Left / cell + dx; var row = placement.Row + 2 * dy;
        if (x is < 0 or > 10000 || row is < 0 or > 20000) return null;
        GroupLayout.Move(copy, group, placement.Column, row, availableColumns, cell, x);
        return LayoutLocks.AllowsChange(document.Groups, copy.Groups, document.Preferences, availableColumns) ? copy.Groups : null;
    }
}
