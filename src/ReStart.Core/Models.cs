namespace ReStart.Core;

/// <summary>Logical tile footprints measured in grid cells.</summary>
public enum TileSize
{
    /// <summary>One by one cell.</summary>
    Small,
    /// <summary>Two by two cells.</summary>
    Medium,
    /// <summary>Four by two cells.</summary>
    Wide,
    /// <summary>Four by four cells.</summary>
    Large
}
/// <summary>Requested application palette; high contrast always takes precedence.</summary>
public enum ThemeMode
{
    /// <summary>Follow the Windows application theme.</summary>
    System,
    /// <summary>Use the dark palette.</summary>
    Dark,
    /// <summary>Use the light palette.</summary>
    Light
}

/// <summary>Describes an installed application or shortcut without modifying its OS-provided name.</summary>
/// <param name="Id">Stable AppsFolder or shortcut identity used by pinned tiles and icons.</param>
/// <param name="Name">OS-provided application display name.</param>
/// <param name="LaunchTarget">Shell identity, protocol or shortcut path used for launching.</param>
/// <param name="IsShellApp">Whether the target is resolved through the AppsFolder namespace.</param>
/// <param name="FolderPath">OS-provided relative Start-menu display folders, separated by slashes; empty for root apps.</param>
public sealed record AppEntry(string Id, string Name, string LaunchTarget, bool IsShellApp = true, string FolderPath = "")
{
    /// <summary>Gets the display bucket used for alphabet navigation.</summary>
    public string Section => SectionFor(Name);
    /// <summary>Returns the shared alphabet bucket for an app or folder display name.</summary>
    public static string SectionFor(string name) => string.IsNullOrWhiteSpace(name) ? "#" :
        char.IsAsciiLetter(name[0]) ? char.ToUpperInvariant(name[0]).ToString() :
        char.IsDigit(name[0]) ? "#" : L.T(K.AppListOther);
}

/// <summary>Stores a persistent root tile or one-level folder child.</summary>
public sealed class Tile
{
    /// <summary>Gets or sets a stable identifier unique within the layout.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>Gets or sets the source application identity or built-in tile identity.</summary>
    public string AppId { get; set; } = "";
    /// <summary>Gets or sets the user-owned display label; language changes never rewrite saved labels.</summary>
    public string Label { get; set; } = "";
    /// <summary>Gets or sets the launch target. A script tile does not execute this target.</summary>
    public string Target { get; set; } = "";
    /// <summary>Gets or sets whether the launch target is an AppsFolder shell identity.</summary>
    public bool IsShellApp { get; set; } = true;
    /// <summary>Gets or sets the logical tile footprint.</summary>
    public TileSize Size { get; set; } = TileSize.Medium;
    /// <summary>Gets or sets an explicit logical column, or null for automatic placement.</summary>
    public int? Column { get; set; }
    /// <summary>Gets or sets an explicit logical row, or null for automatic placement.</summary>
    public int? Row { get; set; }
    /// <summary>Gets or sets the optional saved tile color.</summary>
    public string Color { get; set; } = "";
    /// <summary>Gets or sets whether an explicitly selected tile color overrides the shared theme and script background.</summary>
    public bool UseCustomColor { get; set; }
    /// <summary>Gets or sets a built-in widget kind; script identifies a local JavaScript package.</summary>
    public string Widget { get; set; } = "";
    /// <summary>Gets or sets an installed package identifier, never an arbitrary source-file path.</summary>
    public string ScriptId { get; set; } = "";
    /// <summary>Gets or sets whether live updates are enabled for this tile.</summary>
    public bool Live { get; set; } = true;
    /// <summary>Gets or sets one-level folder contents; nested folders are rejected.</summary>
    public List<Tile> Children { get; set; } = [];
    /// <summary>Gets whether this tile currently owns folder children.</summary>
    public bool IsFolder => Children.Count > 0;

    /// <summary>Creates a new tile preserving the source application name and launch identity.</summary>
    public static Tile FromApp(AppEntry app) => new()
    {
        AppId = app.Id,
        Label = app.Name,
        Target = app.LaunchTarget,
        IsShellApp = app.IsShellApp
    };
}

/// <summary>Stores root tiles and an optional position on the full-screen group grid.</summary>
public sealed class TileGroup
{
    /// <summary>Gets or sets an absolute horizontal board anchor in logical cells; null uses the legacy group-column anchor.</summary>
    public double? X { get; set; }
    /// <summary>Gets or sets whether this entry is one independent tile with no group header or reserved group width.</summary>
    public bool IsStandalone { get; set; }
    /// <summary>Gets or sets a stable identifier unique within the layout.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>Gets or sets a user-owned group label.</summary>
    public string Name { get; set; } = L.T(K.NewGroup);
    /// <summary>Gets or sets a horizontal group-column anchor; null uses automatic placement for legacy layouts.</summary>
    public int? Column { get; set; }
    /// <summary>Gets or sets the vertical anchor in half-cell steps; null uses automatic placement.</summary>
    public int? Row { get; set; }
    /// <summary>Gets or sets the root tiles in this group.</summary>
    public List<Tile> Tiles { get; set; } = [];
}

/// <summary>Stores local UI and integration choices; imported layouts do not replace these settings.</summary>
public sealed class Preferences
{
    /// <summary>Gets or sets whether one read-only update check runs at normal startup.</summary>
    public bool CheckUpdatesOnStartup { get; set; } = true;
    /// <summary>Gets or sets whether published prereleases are eligible for update notifications.</summary>
    public bool IncludePrereleaseUpdates { get; set; } = true;
    /// <summary>Gets or sets ja, en, or system for UI resource selection.</summary>
    public string Language { get; set; } = "system";
    /// <summary>Gets or sets whether a bare Windows-key press toggles ReStart.</summary>
    public bool CaptureWindowsKey { get; set; } = true;
    /// <summary>Gets or sets experimental native Start-button interception.</summary>
    public bool CaptureStartButton { get; set; }
    /// <summary>Gets or sets the saved startup preference; settings reconcile it with the real Run registration.</summary>
    public bool StartWithWindows { get; set; }
    /// <summary>Gets or sets whether switching apps or clicking the taskbar closes the menu.</summary>
    public bool CloseOnDeactivate { get; set; } = true;
    /// <summary>Gets or sets whether the desktop wallpaper is used as the background.</summary>
    public bool ShowWallpaper { get; set; } = true;
    /// <summary>Gets or sets whether app tiles use classic accent-colored backgrounds.</summary>
    public bool ColoredTiles { get; set; }
    /// <summary>Gets or sets whether the dark Start surface uses the selected accent color, as in late Windows 10.</summary>
    public bool ShowAccentOnStart { get; set; }
    /// <summary>Gets or sets whether showing the menu animates when system accessibility settings permit.</summary>
    public bool Animate { get; set; } = true;
    /// <summary>Gets or sets the requested application theme.</summary>
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    /// <summary>Gets or sets the requested accent color.</summary>
    public string Accent { get; set; } = "#0078D7";
    /// <summary>Gets or sets background tint opacity, normalized to 0.3 through 1.</summary>
    public double BackgroundOpacity { get; set; } = 0.86;
    /// <summary>Gets or sets the cell scale, normalized to 0.7 through 1.5.</summary>
    public double TileScale { get; set; } = 1;
    /// <summary>Gets or sets six or eight logical cells per tile group.</summary>
    public int Columns { get; set; } = 6;
}

/// <summary>Defines the versioned, locally persisted layout and its normalization rules.</summary>
public sealed class LayoutDocument
{
    /// <summary>Current supported layout schema version.</summary>
    public const int CurrentVersion = 3;
    /// <summary>Current supported layout schema version.</summary>
    public int Version { get; set; } = CurrentVersion;
    /// <summary>Gets the legacy version migrated by normalization; omitted from saved layouts.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int? MigratedFromVersion { get; private set; }
    /// <summary>Gets or sets the application version that last saved this document; empty denotes a legacy producer.</summary>
    public string SavedWithAppVersion { get; set; } = "";
    /// <summary>Gets or sets the last successful application/schema upgrade and its durable backup filename.</summary>
    public LayoutUpgradeInfo? LastUpgrade { get; set; }
    /// <summary>Gets or sets local UI and shell integration preferences.</summary>
    public Preferences Preferences { get; set; } = new();
    /// <summary>Gets or sets groups in display order.</summary>
    public List<TileGroup> Groups { get; set; } = [];

    /// <summary>Repairs bounded presentation values and duplicate identities; rejects unsupported schemas and nested folders.</summary>
    public void Normalize()
    {
        var migrateStandalone = Version is >= 1 and < 3;
        if (Version is >= 1 and < CurrentVersion) { MigratedFromVersion = Version; Version = CurrentVersion; }
        else if (Version != CurrentVersion)
            throw new UnsupportedLayoutVersionException(Version);
        Preferences ??= new();
        if (ReleaseVersion.Parse(SavedWithAppVersion) == null) SavedWithAppVersion = "";
        if (LastUpgrade != null && (LastUpgrade.BackupFile == null || LastUpgrade.FromAppVersion == null || LastUpgrade.ToAppVersion == null
            || LastUpgrade.BackupFile.Length > 100 || LastUpgrade.BackupFile != Path.GetFileName(LastUpgrade.BackupFile)
            || LastUpgrade.FromAppVersion.Length > 128 || LastUpgrade.ToAppVersion.Length > 128)) LastUpgrade = null;
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
            group.Name = (group.Name ?? "").Trim();
            if (group.X.HasValue && (!double.IsFinite(group.X.Value) || group.X is < 0 or > 10000)) group.X = null;
            if (group.Column is < 0 or > 63 || group.Row is < 0 or > 20000 || group.Column.HasValue != group.Row.HasValue)
            { group.Column = null; group.Row = null; }
            group.Tiles ??= [];
            NormalizeTiles(group.Tiles, ids, 0);
            if (group.IsStandalone && group.Tiles.Count != 1) group.IsStandalone = false;
            if (migrateStandalone && group.Name.Length == 0 && group.Tiles.Count == 1)
            {
                // Earlier versions represented every detached tile as an unnamed group.
                group.IsStandalone = true;
                if (group.Row.HasValue) group.Row = Math.Min(20000, group.Row.Value + 2 * (group.Tiles[0].Row ?? 0) +
                    (int)Math.Round(GroupLayout.HeaderHeight / (52 * Preferences.TileScale / 2)));
            }
            if (group.IsStandalone) { group.Name = ""; group.Tiles[0].Row = 0; }
        }
    }

    private static void NormalizeTiles(List<Tile> tiles, HashSet<string> ids, int depth)
    {
        if (tiles.Count > 2000) throw new InvalidDataException(L.T(K.ErrorTooManyTiles));
        foreach (var tile in tiles)
        {
            if (string.IsNullOrEmpty(tile.Id) || !ids.Add(tile.Id))
            {
                tile.Id = Guid.NewGuid().ToString("N");
                ids.Add(tile.Id);
            }
            tile.Label ??= L.T(K.AppFallback);
            tile.AppId ??= "";
            tile.Target ??= "";
            tile.Widget ??= "";
            tile.ScriptId ??= "";
            tile.Color ??= "";
            if (tile.UseCustomColor && (tile.Color.Length != 7 || tile.Color[0] != '#' || !tile.Color.Skip(1).All(Uri.IsHexDigit))) tile.UseCustomColor = false;
            tile.Children ??= [];
            if (!Enum.IsDefined(tile.Size)) tile.Size = TileSize.Medium;
            if (tile.Column is < 0 or > 15 || tile.Row is < 0 or > 200 || tile.Column.HasValue != tile.Row.HasValue)
            { tile.Column = null; tile.Row = null; }
            if (depth >= 1 && tile.Children.Count != 0)
                throw new InvalidDataException(L.T(K.ErrorNestedFolder));
            NormalizeTiles(tile.Children, ids, depth + 1);
        }
    }
}

/// <summary>Records a completed local upgrade and the backup needed to restore the prior application or schema.</summary>
public sealed record LayoutUpgradeInfo(string FromAppVersion, string ToAppVersion, int FromSchema, int ToSchema, DateTimeOffset AtUtc, string BackupFile);
