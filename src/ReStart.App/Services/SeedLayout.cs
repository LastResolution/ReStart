using ReStart.Core;

namespace ReStart.App.Services;

internal static class SeedLayout
{
    public static LayoutDocument Create(IReadOnlyList<AppEntry> apps)
    {
        var document = new LayoutDocument();
        var everyday = new TileGroup { Name = L.T("いつものアプリ") };
        var tools = new TileGroup { Name = L.T("ツール") };
        everyday.Tiles.Add(new() { AppId = "restart:calendar", Label = L.T("カレンダー"), Target = "", IsShellApp = false, Widget = "calendar", Size = TileSize.Wide, Color = "#0078D7" });
        everyday.Tiles.Add(new() { AppId = "restart:clock", Label = L.T("クロック"), Target = "ms-clock:", IsShellApp = false, Widget = "clock", Color = "#325B8C" });
        var chosen = new HashSet<string>();
        foreach (var term in new[] { "Edge", "Chrome", "Firefox", "Outlook", "メール", "フォト", "Photos", "Store", "Spotify", "メモ帳", "Notepad", "電卓", "Calculator", "Terminal", "Visual Studio Code", "ペイント", "Paint" })
        {
            var app = apps.FirstOrDefault(a => a.Name.Contains(term, StringComparison.OrdinalIgnoreCase) && !chosen.Contains(a.Id));
            if (app == null) continue;
            chosen.Add(app.Id);
            var group = everyday.Tiles.Count < 9 ? everyday : tools;
            group.Tiles.Add(Tile.FromApp(app));
        }
        tools.Tiles.Add(new() { AppId = "restart:explorer", Label = L.T("エクスプローラー"), Target = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), IsShellApp = false, Color = "#A56A00" });
        tools.Tiles.Add(new() { AppId = "restart:settings", Label = L.T("設定"), Target = "ms-settings:", IsShellApp = false, Color = "#555555" });
        document.Groups = [everyday, tools];
        return document;
    }
}
