namespace ReStart.Core;

/// <summary>Describes a transient inline folder expansion; root tile coordinates remain unchanged.</summary>
public sealed record FolderExpansion(Tile Folder, int Row, int ChildRows, IReadOnlyList<TilePlacement> Children)
{
    /// <summary>Returns the added header, child-grid and separator height in DIPs.</summary>
    public double ExtraHeight(double cell) => GroupLayout.HeaderHeight + ChildRows * cell + 12;

    /// <summary>Returns a root tile's displayed Y position while keeping the persisted row unchanged.</summary>
    public double TileY(TilePlacement tile, double cell) => tile.Row * cell + (tile.Row >= Row ? ExtraHeight(cell) : 0);

    /// <summary>Expands below a complete tile row, advancing the cut line so no large tile is split.</summary>
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
