using ReStart.Core;

namespace ReStart.App.Services;

internal static class SeedLayout
{
    public static LayoutDocument Create(IReadOnlyList<AppEntry> apps)
    {
        var document = new LayoutDocument();
        var everyday = new TileGroup { Name = L.T(K.SeedEveryday) };
        var tools = new TileGroup { Name = L.T(K.SeedTools) };
        everyday.Tiles.Add(new() { AppId = "restart:calendar", Label = L.T(K.AppCalendar), Target = "", IsShellApp = false, Widget = "calendar", Size = TileSize.Wide, Color = "#0078D7" });
        everyday.Tiles.Add(new() { AppId = "restart:clock", Label = L.T(K.AppClock), Target = "ms-clock:", IsShellApp = false, Widget = "clock", Color = "#325B8C" });
        var chosen = new HashSet<string>();
        var terms = new[] { "Edge", "Chrome", "Firefox", "Outlook", "Store", "Spotify", "Terminal", "Visual Studio Code" }
            .Concat(new[] { K.AppMail, K.AppPhotos, K.AppNotepad, K.AppCalculator, K.AppPaint }
                .SelectMany(key => new[] { L.Translate(key, "ja"), L.Translate(key, "en") }));
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
