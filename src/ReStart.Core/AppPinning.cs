namespace ReStart.Core;

/// <summary>Applies app-level pin and unpin commands across root tiles and tile-folder contents.</summary>
public static class AppPinning
{
    /// <summary>Gets whether an app is already pinned, including inside a tile folder.</summary>
    public static bool IsPinned(LayoutDocument document, AppEntry app) => Find(document, app) != null;

    /// <summary>Pins an app once, using the requested group or first group, and preserves an existing pin's identity and placement.</summary>
    public static Tile Pin(LayoutDocument document, AppEntry app, TileGroup? destination = null)
    {
        if (destination != null && !document.Groups.Contains(destination)) throw new ArgumentException("Unknown destination group.", nameof(destination));
        if (Find(document, app) is { } existing) return existing;
        if (destination?.IsStandalone == true) throw new ArgumentException("An independent tile is not a destination group.", nameof(destination));
        destination ??= document.Groups.FirstOrDefault(g => !g.IsStandalone);
        if (destination == null) document.Groups.Add(destination = new() { Name = "", IsStandalone = true });
        var tile = Tile.FromApp(app);
        destination.Tiles.Add(tile);
        return tile;
    }

    /// <summary>Removes all pins for an app and prunes only groups or folders emptied by that removal.</summary>
    public static int Unpin(LayoutDocument document, AppEntry app)
    {
        var removed = 0;
        foreach (var group in document.Groups.ToArray())
        {
            var previous = removed;
            foreach (var tile in group.Tiles.ToArray())
            {
                if (Matches(tile, app)) { group.Tiles.Remove(tile); removed++; }
                else if (tile.IsFolder)
                {
                    var childrenRemoved = tile.Children.RemoveAll(child => Matches(child, app));
                    removed += childrenRemoved;
                    if (childrenRemoved > 0 && tile.Children.Count == 0) group.Tiles.Remove(tile);
                }
            }
            if (removed != previous && group.Tiles.Count == 0) document.Groups.Remove(group);
        }
        return removed;
    }

    /// <summary>Finds an existing root or folder-child pin without moving it or changing the document.</summary>
    public static Tile? Find(LayoutDocument document, AppEntry app) => document.Groups.SelectMany(g => g.Tiles)
        .SelectMany(t => new[] { t }.Concat(t.Children)).FirstOrDefault(t => Matches(t, app));
    private static bool Matches(Tile tile, AppEntry app) => !tile.IsFolder && tile.Widget.Length == 0
        && app.Id.Length > 0 && string.Equals(tile.AppId, app.Id, StringComparison.OrdinalIgnoreCase);
}
