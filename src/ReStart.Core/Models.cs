namespace ReStart.Core;

public enum TileSize { Small, Medium, Wide, Large }
public enum ThemeMode { System, Dark, Light }

public sealed record AppEntry(string Id, string Name, string LaunchTarget, bool IsShellApp = true)
{
    public string Section => string.IsNullOrWhiteSpace(Name) ? "#" :
        char.IsAsciiLetter(Name[0]) ? char.ToUpperInvariant(Name[0]).ToString() :
        char.IsDigit(Name[0]) ? "#" : "あ・他";
}

public sealed class Tile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AppId { get; set; } = "";
    public string Label { get; set; } = "";
    public string Target { get; set; } = "";
    public bool IsShellApp { get; set; } = true;
    public TileSize Size { get; set; } = TileSize.Medium;
    public string Color { get; set; } = "";
    public string Widget { get; set; } = "";
    public bool Live { get; set; } = true;
    public List<Tile> Children { get; set; } = [];
    public bool IsFolder => Children.Count > 0;

    public static Tile FromApp(AppEntry app) => new()
    {
        AppId = app.Id,
        Label = app.Name,
        Target = app.LaunchTarget,
        IsShellApp = app.IsShellApp
    };
}

public sealed class TileGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "新しいグループ";
    public List<Tile> Tiles { get; set; } = [];
}

public sealed class Preferences
{
    public bool CaptureWindowsKey { get; set; } = true;
    public bool CaptureStartButton { get; set; }
    public bool StartWithWindows { get; set; }
    public bool CloseOnDeactivate { get; set; } = true;
    public bool ShowWallpaper { get; set; } = true;
    public bool ColoredTiles { get; set; }
    public bool Animate { get; set; } = true;
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public string Accent { get; set; } = "#0078D7";
    public double BackgroundOpacity { get; set; } = 0.86;
    public double TileScale { get; set; } = 1;
    public int Columns { get; set; } = 6;
}

public sealed class LayoutDocument
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public Preferences Preferences { get; set; } = new();
    public List<TileGroup> Groups { get; set; } = [];

    public void Normalize()
    {
        if (Version != CurrentVersion)
            throw new InvalidDataException($"未対応のレイアウト形式です: {Version}");
        Preferences ??= new();
        Groups ??= [];
        Preferences.Columns = Preferences.Columns == 8 ? 8 : 6;
        Preferences.TileScale = double.IsFinite(Preferences.TileScale) ? Math.Clamp(Preferences.TileScale, 0.7, 1.5) : 1;
        Preferences.BackgroundOpacity = double.IsFinite(Preferences.BackgroundOpacity) ? Math.Clamp(Preferences.BackgroundOpacity, 0.3, 1) : 0.86;
        if (!Enum.IsDefined(Preferences.Theme)) Preferences.Theme = ThemeMode.Dark;
        var ids = new HashSet<string>();
        foreach (var group in Groups)
        {
            if (string.IsNullOrEmpty(group.Id) || !ids.Add(group.Id))
            {
                group.Id = Guid.NewGuid().ToString("N");
                ids.Add(group.Id);
            }
            group.Name = string.IsNullOrWhiteSpace(group.Name) ? "新しいグループ" : group.Name.Trim();
            group.Tiles ??= [];
            NormalizeTiles(group.Tiles, ids, 0);
        }
    }

    private static void NormalizeTiles(List<Tile> tiles, HashSet<string> ids, int depth)
    {
        if (tiles.Count > 2000) throw new InvalidDataException("タイル数が多すぎます。");
        foreach (var tile in tiles)
        {
            if (string.IsNullOrEmpty(tile.Id) || !ids.Add(tile.Id))
            {
                tile.Id = Guid.NewGuid().ToString("N");
                ids.Add(tile.Id);
            }
            tile.Label ??= "アプリ";
            tile.AppId ??= "";
            tile.Target ??= "";
            tile.Widget ??= "";
            tile.Color ??= "";
            tile.Children ??= [];
            if (!Enum.IsDefined(tile.Size)) tile.Size = TileSize.Medium;
            if (depth >= 1 && tile.Children.Count != 0)
                throw new InvalidDataException("フォルダーの入れ子には対応していません。");
            NormalizeTiles(tile.Children, ids, depth + 1);
        }
    }
}
