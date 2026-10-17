namespace ReStart.Core;

public static class LayoutEditor
{
    public static Tile? Find(LayoutDocument document, string id) => document.Groups.SelectMany(g => g.Tiles)
        .SelectMany(t => new[] { t }.Concat(t.Children)).FirstOrDefault(t => t.Id == id);

    public static void Remove(LayoutDocument document, string id)
    {
        foreach (var group in document.Groups)
        {
            if (group.Tiles.RemoveAll(t => t.Id == id) > 0) return;
            foreach (var folder in group.Tiles.ToList())
            {
                if (folder.Children.RemoveAll(t => t.Id == id) == 0) continue;
                if (folder.Children.Count == 0) group.Tiles.Remove(folder);
                return;
            }
        }
    }

    public static void Move(LayoutDocument document, Tile tile, TileGroup group, Tile? before = null)
    {
        if (!document.Groups.Contains(group)) throw new ArgumentException("Group does not belong to the document.", nameof(group));
        if (tile == before) return;
        Remove(document, tile.Id);
        tile.Column = null; tile.Row = null;
        var index = before == null ? group.Tiles.Count : group.Tiles.IndexOf(before);
        group.Tiles.Insert(index < 0 ? group.Tiles.Count : index, tile);
    }

    public static void MergeFolder(LayoutDocument document, Tile source, Tile target)
    {
        if (source == target || source.IsFolder || target.Children.Contains(source)) return;
        var group = document.Groups.FirstOrDefault(g => g.Tiles.Contains(target));
        if (group == null) return;
        Remove(document, source.Id);
        if (target.IsFolder) target.Children.Add(source);
        else
        {
            var index = group.Tiles.IndexOf(target);
            group.Tiles[index] = new() { Label = "フォルダー", Size = TileSize.Medium, Column = target.Column, Row = target.Row, Children = [target, source] };
        }
    }

    public static void MoveToCell(LayoutDocument document, Tile tile, TileGroup destination, int column, int row)
    {
        if (!document.Groups.Contains(destination)) throw new ArgumentException("Unknown group.", nameof(destination));
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
    }
}
