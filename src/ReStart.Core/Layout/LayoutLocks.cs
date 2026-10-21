using System.Text.Json;

namespace ReStart.Core;

/// <summary>Checks geometry and ownership changes against global and per-group locks without changing either layout.</summary>
public static class LayoutLocks
{
    /// <summary>Determines whether proposed groups preserve every locked position, size, tile identity and folder membership.</summary>
    /// <param name="original">Last committed group tree, including one-level folder children; it is not modified.</param>
    /// <param name="proposed">Candidate group tree; names, colors, live state and launch options may change while locked.</param>
    /// <param name="preferences">Current lock choices and tile sizing; the same sizing is used for both trees.</param>
    /// <param name="availableColumns">Current board lane count, from 1 through 64, used to resolve automatic anchors.</param>
    /// <returns>True when all locked geometry and membership survives, including neighbors affected by collision resolution.</returns>
    public static bool AllowsChange(IReadOnlyList<TileGroup> original, IReadOnlyList<TileGroup> proposed, Preferences preferences, int availableColumns)
    {
        if (!preferences.LayoutLocked && preferences.LockedGroupIds.Count == 0) return true;
        var before = Shapes(original, preferences, availableColumns);
        var after = Shapes(proposed, preferences, availableColumns);
        if (preferences.LayoutLocked && (before.Count != after.Count || before.Any(p => !after.TryGetValue(p.Key, out var value) || value != p.Value))) return false;
        return preferences.LockedGroupIds.All(id => !before.TryGetValue(id, out var shape) || after.TryGetValue(id, out var value) && shape == value);
    }

    private static Dictionary<string, string> Shapes(IReadOnlyList<TileGroup> groups, Preferences preferences, int lanes)
        => GroupLayout.Pack(groups, lanes, preferences.Columns, 52 * preferences.TileScale, preserveColumns: true)
            .ToDictionary(p => p.Group.Id, p => JsonSerializer.Serialize(new
            {
                p.Group.IsStandalone,
                p.Left,
                p.Top,
                p.Width,
                p.Height,
                Tiles = TileLayout.Pack(p.Group.Tiles, preferences.Columns).OrderBy(t => t.Tile.Id, StringComparer.Ordinal).Select(t => new
                {
                    t.Tile.Id,
                    t.Column,
                    t.Row,
                    t.Width,
                    t.Height,
                    Children = TileLayout.Pack(t.Tile.Children, preferences.Columns).OrderBy(c => c.Tile.Id, StringComparer.Ordinal)
                        .Select(c => new { c.Tile.Id, c.Column, c.Row, c.Width, c.Height })
                })
            }), StringComparer.Ordinal);
}
