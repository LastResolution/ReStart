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
            group.Tiles[index] = new() { Label = "フォルダー", Size = TileSize.Medium, Children = [target, source] };
        }
    }
}
