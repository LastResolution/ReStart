namespace ReStart.Core;

/// <summary>One selected tile's visible origin, expressed in logical cells on the board.</summary>
/// <param name="Id">Stable layout tile ID.</param>
/// <param name="X">Visible board X origin in small-cell units.</param>
/// <param name="Y">Visible board Y origin in small-cell units, relative to the tile-board origin.</param>
public readonly record struct SelectedTilePosition(string Id, double X, double Y);

/// <summary>Plans an atomic multi-tile move on copies; a rejected or cancelled plan never changes the source document.</summary>
public static class TileBatchMove
{
    /// <summary>Plans a rigid selection move into a group/folder or onto open board space. Returns null when the selection cannot fit.</summary>
    /// <remarks>Group/folder destinations snap to whole cells. Open-space destinations use half cells and create independent tiles or one unnamed group.</remarks>
    /// <param name="document">Original layout to inspect without modifying its groups or preferences.</param>
    /// <param name="selection">Selected tile IDs and their visible origins in board small-cell units.</param>
    /// <param name="anchorId">Selected tile whose origin follows the destination anchor.</param>
    /// <param name="groupId">Destination named-group ID, or null for a folder/board destination.</param>
    /// <param name="folderId">Destination root-folder ID, or null for a group/board destination.</param>
    /// <param name="x">Destination anchor X in small-cell units, local to a group/folder or absolute on the board.</param>
    /// <param name="y">Destination anchor Y in small-cell units, local to a group/folder or absolute on the board.</param>
    /// <param name="availableColumns">Number of available board lanes, from 1 through 64.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <param name="independent">Whether an empty-space destination creates independent entries; false collects the selection into one new unnamed group.</param>
    /// <returns>A deep-copied group tree for an atomic commit, or null for an invalid, colliding, or unfit selection.</returns>
    public static List<TileGroup>? Plan(LayoutDocument document, IReadOnlyList<SelectedTilePosition> selection,
        string anchorId, string? groupId, string? folderId, double x, double y, int availableColumns, double cell, bool independent = true)
    {
        if (selection.Count == 0 || selection.Select(s => s.Id).Distinct().Count() != selection.Count
            || !double.IsFinite(x) || !double.IsFinite(y) || x is < -10000 or > 10000 || y is < -10000 or > 10000
            || selection.Any(s => !double.IsFinite(s.X) || !double.IsFinite(s.Y) || s.X is < -10000 or > 20000 || s.Y is < -10000 or > 20000)) return null;
        var anchor = selection.FirstOrDefault(s => s.Id == anchorId);
        if (anchor.Id == null) return null;
        var copy = new LayoutDocument { Preferences = document.Preferences, Groups = LayoutHistory.CopyGroups(document.Groups) };
        var tiles = selection.Select(s => LayoutEditor.Find(copy, s.Id)).ToArray();
        if (tiles.Any(t => t == null)) return null;
        var ids = selection.Select(s => s.Id).ToHashSet();
        if (tiles.Any(t => t!.Children.Any(c => ids.Contains(c.Id)))) return null;
        var group = groupId == null ? null : copy.Groups.FirstOrDefault(g => g.Id == groupId && !g.IsStandalone);
        var folder = folderId == null ? null : copy.Groups.SelectMany(g => g.Tiles).FirstOrDefault(t => t.Id == folderId && t.IsFolder);
        if (groupId != null && group == null || folderId != null && (folder == null || ids.Contains(folder.Id) || tiles.Any(t => t!.IsFolder))) return null;

        // Freeze existing visible anchors before removing source tiles so that an
        // emptied group cannot pull unrelated groups into a different column.
        foreach (var p in GroupLayout.Pack(copy.Groups, availableColumns, copy.Preferences.Columns, cell, preserveColumns: true))
        { p.Group.X = p.Left / cell - (p.Group.IsStandalone ? TileLayout.Pack(p.Group.Tiles, copy.Preferences.Columns)[0].Column : 0); p.Group.Column = p.Column; p.Group.Row = p.Row; }
        var offsets = selection.Select(s => (X: s.X - anchor.X, Y: s.Y - anchor.Y)).ToArray();
        if (!independent && group == null && folder == null)
        {
            var minX = offsets.Min(o => o.X); var minY = offsets.Min(o => o.Y);
            var left = Math.Max(0, x + minX); var top = Math.Max(0, y + minY);
            group = new()
            {
                Name = "",
                X = left,
                Column = Math.Clamp((int)(left * cell / (copy.Preferences.Columns * cell + GroupLayout.Gap)), 0, 63),
                Row = Math.Clamp((int)Math.Round(top * 2, MidpointRounding.AwayFromZero), 0, 20000)
            };
            copy.Groups.Add(group); x = -minX; y = -minY;
        }
        var target = folder?.Children ?? group?.Tiles;
        if (target != null)
        {
            var positions = offsets.Select(o => (Column: (int)Math.Round(x + o.X, MidpointRounding.AwayFromZero),
                Row: (int)Math.Round(y + o.Y, MidpointRounding.AwayFromZero))).ToArray();
            var minX = positions.Min(p => p.Column); var minY = positions.Min(p => p.Row);
            var maxX = positions.Select((p, i) => p.Column + TileLayout.Dimensions(tiles[i]!.Size).Width).Max();
            var maxY = positions.Select((p, i) => p.Row + TileLayout.Dimensions(tiles[i]!.Size).Height).Max();
            var columns = copy.Preferences.Columns;
            if (maxX - minX > columns || maxY - minY > 201) return null;
            var dx = Math.Max(0, -minX) - Math.Max(0, maxX + Math.Max(0, -minX) - columns);
            var dy = Math.Max(0, -minY) - Math.Max(0, maxY + Math.Max(0, -minY) - 201);
            var cells = new HashSet<(int, int)>();
            for (var i = 0; i < positions.Length; i++)
            {
                positions[i] = (positions[i].Column + dx, positions[i].Row + dy);
                var (w, h) = TileLayout.Dimensions(tiles[i]!.Size);
                for (var yy = 0; yy < h; yy++)
                    for (var xx = 0; xx < w; xx++)
                        if (!cells.Add((positions[i].Column + xx, positions[i].Row + yy))) return null;
            }
            var previous = TileLayout.Pack(target, columns);
            foreach (var p in previous) { p.Tile.Column = p.Column; p.Tile.Row = p.Row; }
            RemoveSelection(copy, ids, group, folder);
            for (var i = 0; i < tiles.Length; i++)
            { tiles[i]!.Column = positions[i].Column; tiles[i]!.Row = positions[i].Row; }
            target.InsertRange(0, tiles!);
            var moved = tiles.Select((t, i) => new TilePlacement(t!, positions[i].Column, positions[i].Row,
                TileLayout.Dimensions(t!.Size).Width, TileLayout.Dimensions(t.Size).Height)).ToArray();
            TileReflow.Apply(target, previous, moved, columns);
        }
        else
        {
            var left = Math.Max(x, -offsets.Min(o => o.X)); var top = Math.Max(y, -offsets.Min(o => o.Y));
            if (offsets.Any(o => left + o.X > 10000 || top + o.Y > 10000)) return null;
            RemoveSelection(copy, ids, null, null);
            var moving = new HashSet<string>();
            for (var i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i]!; tile.Column = 0; tile.Row = 0;
                var entry = new TileGroup
                {
                    IsStandalone = true,
                    Name = "",
                    Tiles = [tile],
                    X = left + offsets[i].X,
                    Column = Math.Clamp((int)((left + offsets[i].X) * cell / (copy.Preferences.Columns * cell + GroupLayout.Gap)), 0, 63),
                    Row = Math.Max(0, (int)Math.Round((top + offsets[i].Y) * 2, MidpointRounding.AwayFromZero))
                };
                copy.Groups.Add(entry); moving.Add(entry.Id);
            }
            var placements = GroupLayout.Pack(copy.Groups, availableColumns, copy.Preferences.Columns, cell, preserveColumns: true, priorityIds: moving);
            if (placements.Any(p => moving.Contains(p.Group.Id) && p.Row != p.Group.Row)) return null;
            foreach (var p in placements)
            { p.Group.Column = p.Column; p.Group.Row = p.Row; }
        }
        return copy.Groups;
    }

    private static void RemoveSelection(LayoutDocument copy, HashSet<string> ids, TileGroup? destination, Tile? folder)
    {
        foreach (var source in copy.Groups.ToArray())
        {
            var hadTiles = source.Tiles.Count > 0;
            foreach (var root in source.Tiles.ToArray())
            {
                if (ids.Contains(root.Id)) { source.Tiles.Remove(root); continue; }
                var hadChildren = root.Children.Count > 0;
                root.Children.RemoveAll(t => ids.Contains(t.Id));
                if (hadChildren && root.Children.Count == 0 && root != folder) source.Tiles.Remove(root);
            }
            if (hadTiles && source.Tiles.Count == 0 && source != destination) copy.Groups.Remove(source);
        }
    }
}
