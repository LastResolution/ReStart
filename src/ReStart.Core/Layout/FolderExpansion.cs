namespace ReStart.Core;

/// <summary>Describes a transient inline folder expansion; root tile coordinates remain unchanged.</summary>
/// <param name="Folder">Root folder being expanded inline.</param>
/// <param name="Row">Insertion row immediately below the owning root tile, in small tile cells.</param>
/// <param name="ChildRows">Number of small-cell rows occupied by packed children.</param>
/// <param name="Children">Packed child placements in folder-local small-cell coordinates.</param>
public sealed record FolderExpansion(Tile Folder, int Row, int ChildRows, IReadOnlyList<TilePlacement> Children)
{
    /// <summary>Measures the columns actually occupied by roots and expanded children without reserving the configured wrap width.</summary>
    /// <param name="roots">Non-null packed root placements of the owning group; coordinates are read without mutation. An empty collection adds no width.</param>
    /// <returns>The rightmost occupied cell edge across roots and children, with a minimum width of one small tile cell.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="roots"/> is null.</exception>
    public int WidthInCells(IReadOnlyList<TilePlacement> roots)
        => Math.Max(1, roots.Concat(Children).Select(p => p.Column + p.Width).DefaultIfEmpty(1).Max());

    /// <summary>Returns the added header, child-grid and separator height in DIPs.</summary>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <returns>Height inserted for the folder header, children and closing separator, in DIPs.</returns>
    public double ExtraHeight(double cell) => GroupLayout.HeaderHeight + ChildRows * cell + 12;

    /// <summary>Returns a root tile's displayed Y position while keeping the persisted row unchanged.</summary>
    /// <param name="tile">Packed root tile whose displayed Y position is needed.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <returns>Root tile top in DIPs, shifted only if it follows the insertion row.</returns>
    public double TileY(TilePlacement tile, double cell) => tile.Row * cell + (tile.Row >= Row ? ExtraHeight(cell) : 0);

    /// <summary>Expands below a complete tile row, advancing the cut line so no large tile is split.</summary>
    /// <param name="roots">Packed root placements for one group.</param>
    /// <param name="columns">Width of a group in small tile cells, normally 6 or 8.</param>
    /// <param name="folderId">Stable root-folder ID to expand, or null when no folder is open.</param>
    /// <returns>The matching folder expansion, or null when the ID is absent, closed, or not a folder.</returns>
    public static FolderExpansion? Create(IReadOnlyList<TilePlacement> roots, int columns, string? folderId)
    {
        var folder = roots.FirstOrDefault(p => p.Tile.Id == folderId && p.Tile.IsFolder);
        if (folder.Tile == null) return null;
        var row = folder.Row + folder.Height;
        while (true)
        {
            var crossing = roots.Where(p => p.Row < row && p.Row + p.Height > row).ToArray();
            if (crossing.Length == 0) break;
            row = crossing.Max(p => p.Row + p.Height);
        }
        var children = TileLayout.Pack(folder.Tile.Children, columns);
        return new(folder.Tile, row, Math.Max(1, children.Max(p => p.Row + p.Height)), children);
    }
}
