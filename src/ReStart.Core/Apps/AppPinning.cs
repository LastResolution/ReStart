namespace ReStart.Core;

/// <summary>Applies app-level pin and unpin commands across root tiles and tile-folder contents.</summary>
public static class AppPinning
{
    /// <summary>Gets whether an app is already pinned, including inside a tile folder.</summary>
    /// <param name="document">Original layout to inspect without modifying its groups or preferences.</param>
    /// <param name="app">App identity to match by app ID, or filesystem target for a non-Shell app.</param>
    /// <returns>Whether a matching root or folder-child pin exists.</returns>
    public static bool IsPinned(LayoutDocument document, AppEntry app) => Find(document, app) != null;

    /// <summary>Pins an app once in free board space or an explicitly requested group, preserving existing pins and layout.</summary>
    /// <param name="document">Layout to mutate in memory; the caller is responsible for saving it.</param>
    /// <param name="app">Launch identity and display name to pin.</param>
    /// <param name="destination">Explicit named destination group, or null to find free independent board space.</param>
    /// <param name="availableColumns">Number of available board lanes, from 1 through 64.</param>
    /// <param name="cell">Size of one small tile cell in device-independent pixels (DIPs).</param>
    /// <param name="viewportWidth">Usable board width in DIPs; zero uses the configured group width.</param>
    /// <param name="preferredTop">Preferred vertical board offset in DIPs, usually the current scroll offset.</param>
    /// <returns>The existing matching tile, or the newly added pin.</returns>
    /// <exception cref="ArgumentException">The requested destination is absent from the document or is an independent entry.</exception>
    public static Tile Pin(LayoutDocument document, AppEntry app, TileGroup? destination = null, int availableColumns = 1, double cell = 52, double viewportWidth = 0, double preferredTop = 0)
    {
        if (destination != null && !document.Groups.Contains(destination)) throw new ArgumentException("Unknown destination group.", nameof(destination));
        if (Find(document, app) is { } existing) return existing;
        if (destination?.IsStandalone == true) throw new ArgumentException("An independent tile is not a destination group.", nameof(destination));
        var tile = Tile.FromApp(app);
        if (destination == null) LayoutEditor.PinIndependent(document, tile, availableColumns, cell,
            viewportWidth > 0 ? viewportWidth : document.Preferences.Columns * cell, preferredTop);
        else destination.Tiles.Add(tile);
        return tile;
    }

    /// <summary>Removes all pins for an app and prunes only groups or folders emptied by that removal.</summary>
    /// <param name="document">Layout to mutate in memory; the caller is responsible for saving it.</param>
    /// <param name="app">App identity whose matching pins should be removed.</param>
    /// <returns>Number of matching app pins removed, including folder children.</returns>
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
    /// <param name="document">Original layout to inspect without modifying its groups or preferences.</param>
    /// <param name="app">App identity to match by app ID, or filesystem target for a non-Shell app.</param>
    /// <returns>The first matching root or folder-child tile, or null when absent.</returns>
    public static Tile? Find(LayoutDocument document, AppEntry app) => document.Groups.SelectMany(g => g.Tiles)
        .SelectMany(t => new[] { t }.Concat(t.Children)).FirstOrDefault(t => Matches(t, app));
    private static bool Matches(Tile tile, AppEntry app) => !tile.IsFolder && tile.Widget.Length == 0
        && (app.Id.Length > 0 && string.Equals(tile.AppId, app.Id, StringComparison.OrdinalIgnoreCase)
            || !tile.IsShellApp && !app.IsShellApp && app.LaunchTarget.Length > 0 && string.Equals(tile.Target, app.LaunchTarget, StringComparison.OrdinalIgnoreCase));
}
