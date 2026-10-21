namespace ReStart.Core;

/// <summary>Plans an ordinary group around an independent tile without mutating the visible layout.</summary>
public static class TileGrouping
{
    /// <summary>Collects sources beside an independent non-folder target, preserving tile identity and earlier neighbors.</summary>
    /// <param name="document">Original layout to inspect without modifying its groups or preferences.</param>
    /// <param name="sources">Distinct dragged tiles, excluding the target and any selected parent/child pair.</param>
    /// <param name="targetId">Stable ID of an independent, non-folder target tile.</param>
    /// <param name="availableColumns">Number of available board lanes, from 1 through 64.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <returns>A detached group tree containing an ordinary group around the target, or null when grouping is invalid.</returns>
    public static List<TileGroup>? Plan(LayoutDocument document, IReadOnlyList<Tile> sources, string targetId, int availableColumns, double cell)
    {
        if (sources.Count == 0 || sources.Any(t => t.Id == targetId) || sources.Select(t => t.Id).Distinct().Count() != sources.Count
            || availableColumns is < 1 or > 64 || !double.IsFinite(cell) || cell <= 0) return null;
        var ids = sources.Select(t => t.Id).ToHashSet();
        if (sources.Any(t => t.Children.Any(c => ids.Contains(c.Id)))) return null;
        var copy = new LayoutDocument { Preferences = document.Preferences, Groups = LayoutHistory.CopyGroups(document.Groups) };
        var occupied = copy.Groups.Where(g => g.Tiles.Count > 0).Select(g => g.Id).ToHashSet();
        var destination = copy.Groups.SingleOrDefault(g => g.IsStandalone && g.Tiles.Count == 1 && g.Tiles[0].Id == targetId);
        if (destination == null || destination.Tiles[0].IsFolder) return null;
        var baseline = GroupLayout.Pack(copy.Groups, availableColumns, copy.Preferences.Columns, cell, preserveColumns: true);
        foreach (var p in baseline)
        { p.Group.Column = p.Column; p.Group.Row = p.Row; p.Group.X ??= (p.Left - (p.Group.IsStandalone ? p.Group.Tiles[0].Column.GetValueOrDefault() * cell : 0)) / cell; }
        var anchor = baseline.Single(p => p.Group == destination);
        var tiles = sources.Select(t => LayoutEditor.Find(copy, t.Id)
            ?? LayoutHistory.CopyGroups([new() { Tiles = [t] }])[0].Tiles[0]).ToArray();
        foreach (var tile in tiles) LayoutEditor.Remove(copy, tile.Id);
        copy.Groups.RemoveAll(g => g != destination && g.Tiles.Count == 0 && occupied.Contains(g.Id));
        destination.IsStandalone = false; destination.Name = ""; destination.X = anchor.Left / cell;
        destination.Tiles.AddRange(tiles);
        foreach (var tile in destination.Tiles) { tile.Column = null; tile.Row = null; }
        foreach (var p in TileLayout.Pack(destination.Tiles, copy.Preferences.Columns)) { p.Tile.Column = p.Column; p.Tile.Row = p.Row; }
        // The new header occupies space above the old tile. Reserve earlier
        // neighbors first so a small header collision cannot move them below
        // the entire new group; let the new group move down only as needed.
        var earlier = baseline.Where(p => p.Group != destination && p.Top <= anchor.Top).Select(p => p.Group.Id).ToHashSet();
        foreach (var p in GroupLayout.Pack(copy.Groups, availableColumns, copy.Preferences.Columns, cell, preserveColumns: true, priorityIds: earlier))
        { p.Group.Column = p.Column; p.Group.Row = p.Row; }
        return copy.Groups;
    }
}
