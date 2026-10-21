using ReStart.Core;

namespace ReStart.App.Services;

internal static class SeedLayout
{
    private const int MinimumTilesPerGroup = 6;
    private static readonly string[] OfflineFallbacks = ["year", "focus", "cpu", "memory", "network", "storage", "clock", "battery"];

    /// <summary>Creates a fresh layout from installed app roles, filling sparse groups with distinct offline examples.</summary>
    /// <param name="apps">Current installed app catalog; entries are selected without modifying them or reading their targets.</param>
    /// <param name="packages">Package store that receives owned copies of the required bundled examples.</param>
    /// <returns>A new two-group layout with at least six tiles per group, automatic coordinates and default preferences.</returns>
    /// <remarks>Call only for first-run seeding or an explicit reset. The caller preserves preferences when resetting.</remarks>
    public static LayoutDocument Create(IReadOnlyList<AppEntry> apps, ScriptTilePackages packages)
    {
        var document = new LayoutDocument();
        var everyday = new TileGroup { Name = TextCatalog.T(K.SeedEveryday) };
        var tools = new TileGroup { Name = TextCatalog.T(K.SeedTools) };
        var calendar = packages.Import(ScriptTilePackages.BundledManifestPath("calendar"));
        calendar.Size = TileSize.Large;
        var clock = packages.Import(ScriptTilePackages.BundledManifestPath("orbit"));
        clock.Size = TileSize.Medium;
        everyday.Tiles.Add(calendar);
        everyday.Tiles.Add(clock);
        var selected = SeedAppSelector.Select(apps);
        everyday.Tiles.AddRange(selected.Everyday.Select(Tile.FromApp));
        tools.Tiles.AddRange(selected.Tools.Select(Tile.FromApp));
        tools.Tiles.Add(new() { AppId = "restart:explorer", Label = TextCatalog.T(K.AppExplorer), Target = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), IsShellApp = false, Color = "#A56A00" });
        tools.Tiles.Add(new() { AppId = "restart:settings", Label = TextCatalog.T(K.NavSettings), Target = "ms-settings:", IsShellApp = false, Color = "#555555" });
        var fallback = 0;
        foreach (var group in new[] { everyday, tools })
            while (group.Tiles.Count < MinimumTilesPerGroup)
                group.Tiles.Add(packages.Import(ScriptTilePackages.BundledManifestPath(OfflineFallbacks[fallback++])));
        document.Groups = [everyday, tools];
        return document;
    }
}
