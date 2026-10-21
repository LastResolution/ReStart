using ReStart.Core;

namespace ReStart.App.Services;

internal static class SeedLayout
{
    public static LayoutDocument Create(IReadOnlyList<AppEntry> apps, ScriptTilePackages packages)
    {
        var document = new LayoutDocument();
        var everyday = new TileGroup { Name = L.T(K.SeedEveryday) };
        var tools = new TileGroup { Name = L.T(K.SeedTools) };
        var calendar = packages.Import(ScriptTilePackages.BundledManifestPath("calendar"));
        calendar.Size = TileSize.Large;
        var clock = packages.Import(ScriptTilePackages.BundledManifestPath("orbit"));
        clock.Size = TileSize.Medium;
        everyday.Tiles.Add(calendar);
        everyday.Tiles.Add(clock);
        var chosen = new HashSet<string>();
        var terms = new[] { "Edge", "Chrome", "Firefox", "Outlook", "Store", "Spotify", "Terminal", "Visual Studio Code" }
            .Concat(new[] { K.AppMail, K.AppPhotos, K.AppNotepad, K.AppCalculator, K.AppPaint }
                .SelectMany(key => TextCatalog.SupportedLanguages.Select(language => L.Translate(key, language))));
        foreach (var term in terms)
        {
            var app = apps.FirstOrDefault(a => a.Name.Contains(term, StringComparison.OrdinalIgnoreCase) && !chosen.Contains(a.Id));
            if (app == null) continue;
            chosen.Add(app.Id);
            var group = everyday.Tiles.Count < 9 ? everyday : tools;
            group.Tiles.Add(Tile.FromApp(app));
        }
        tools.Tiles.Add(new() { AppId = "restart:explorer", Label = L.T(K.AppExplorer), Target = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), IsShellApp = false, Color = "#A56A00" });
        tools.Tiles.Add(new() { AppId = "restart:settings", Label = L.T(K.NavSettings), Target = "ms-settings:", IsShellApp = false, Color = "#555555" });
        document.Groups = [everyday, tools];
        return document;
    }
}
