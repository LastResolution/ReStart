using System.Text.Json.Nodes;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App.Tablet.Home;

/// <summary>
/// The tablet edition's Home layout (tuned on a 13.4" 2560x1600 tablet): a "Today" dashboard of live tiles
/// beside touch-sized app groups. Built from the installed catalog, so it is reproducible
/// (<c>--tablet-command apply-layout</c>, first run, Reset layout) instead of hand-edited.
/// </summary>
/// <remarks>
/// Geometry: 6-cell groups at tile scale 1.1 give 57-DIP cells (medium tiles ~26 mm on the
/// Z13). With the tablet side margins that is three group lanes at 1280 DIP landscape and two
/// at 800 DIP portrait, and the 8-row Today group still clears the dock in landscape.
/// Groups keep automatic anchors so they reflow when the screen rotates.
/// </remarks>
internal static class TabletHomeLayout
{
    internal const double TileScale = 1.1;
    internal const int Columns = 6;
    internal const string YouTubeId = "restart:tablet:youtube";

    /// <summary>One app slot: matched by AUMID/path prefix first, then by display-name prefix.</summary>
    private sealed record Slot(string[] Ids, string[] Names, TileSize Size = TileSize.Medium);

    private static readonly (string Name, Slot[] Slots)[] AppGroups =
    [
        ("Browse & watch", [
            new(["Brave"], ["Brave"]),
            new([YouTubeId], []),
            new([], ["Stremio"]),
        ]),
        ("Creative", [
            new([], ["Adobe Photoshop"]),
            new([], ["Adobe Illustrator"]),
            new([], ["Blender"]),
            new(["Microsoft.Paint", "Microsoft.MSPaint"], ["Paint"]),
            new(["Microsoft.ScreenSketch"], ["Snipping Tool"]),
            new(["Microsoft.WindowsCamera"], ["Camera"]),
        ]),
        ("Music & chat", [
            new(["SpotifyAB.SpotifyMusic", "Spotify.exe"], ["Spotify"]),
            new(["com.squirrel.Discord"], ["Discord"]),
            new(["org.whispersystems.signal-desktop"], ["Signal"]),
        ]),
        ("Tools", [
            new(["Microsoft.Windows.Explorer"], ["File Explorer"]),
            new(["windows.immersivecontrolpanel"], ["Settings"]),
            new(["Microsoft.WindowsTerminal"], ["Terminal", "Windows Terminal"]),
            new(["Microsoft.WindowsCalculator"], ["Calculator"]),
            new([GHelperPath], []),
            new([], ["Task Manager"]),
        ]),
    ];

    private const string GHelperPath = @"C:\Program Files\GHelper\GHelper.exe";

    /// <summary>Package folder name, size, explicit cell and settings overrides of each dashboard tile.</summary>
    private static readonly (string Package, TileSize Size, int Column, int Row, string? View)[] Dashboard =
    [
        ("today", TileSize.Wide, 0, 0, null),
        ("weather", TileSize.Medium, 4, 0, null),
        ("now-playing", TileSize.Wide, 0, 2, null),
        ("battery", TileSize.Medium, 4, 2, null),
        ("home", TileSize.Large, 0, 4, null),
        ("system", TileSize.Medium, 4, 4, null),
        ("weather", TileSize.Medium, 4, 6, "forecast"),
    ];

    internal static string PackagePath(string name) => Path.Combine(AppContext.BaseDirectory, "TabletTiles", name, "manifest.json");

    /// <summary>Creates the full Home document with tablet preferences (callers resetting a layout keep their own preferences).</summary>
    /// <param name="apps">Installed catalog; entries are matched, never launched.</param>
    /// <param name="packages">Receives owned copies of the dashboard packages.</param>
    internal static LayoutDocument Create(IReadOnlyList<AppEntry> apps, ScriptTilePackages packages)
    {
        var document = new LayoutDocument();
        Apply(document.Preferences);
        document.Groups = CreateGroups(apps, packages);
        return document;
    }

    /// <summary>Touch-oriented appearance for the Home screen.</summary>
    internal static void Apply(Preferences preferences)
    {
        preferences.TileScale = TileScale;
        preferences.Columns = Columns;
        // Dashboard tiles carry their own headings; names under them read as clutter.
        preferences.ShowLiveTileNames = false;
        preferences.ShowWallpaper = true;
        preferences.BackgroundBlur = 28;
        preferences.BackgroundOpacity = 0.55;
        preferences.FreeTilePlacement = false;
    }

    internal static List<TileGroup> CreateGroups(IReadOnlyList<AppEntry> apps, ScriptTilePackages packages)
    {
        var today = new TileGroup { Name = "Today" };
        // Personal values (location, Home Assistant entities) come from the user's local settings, never from the bundle.
        var personal = TabletSettings.Load();
        foreach (var (package, size, column, row, view) in Dashboard)
        {
            var tile = packages.Import(PackagePath(package));
            tile.Size = size; tile.Column = column; tile.Row = row;
            if (view != null) ConfigureView(packages, tile, view);
            if (package == "weather" && personal.Weather is { } place)
            {
                if (place.Name is { Length: > 0 } name) PatchSettings(packages, tile, settings => settings["location"] = name);
                ConnectWeather(packages, tile, TabletSettings.WeatherUrl(place.Latitude, place.Longitude));
            }
            if (package == "home" && personal.HomeAssistant is { } home)
                PatchSettings(packages, tile, settings =>
                {
                    foreach (var key in new[] { "lights", "scenesTitle", "scenes" })
                        if (home[key] is { } value) settings[key] = value.DeepClone();
                });
            today.Tiles.Add(tile);
        }
        var groups = new List<TileGroup> { today };
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, slots) in AppGroups)
        {
            var group = new TileGroup { Name = name };
            foreach (var slot in slots)
                if (Resolve(slot, apps, used) is { } tile) { tile.Size = slot.Size; group.Tiles.Add(tile); }
            if (group.Tiles.Count > 0) groups.Add(group);
        }
        return groups;
    }

    private static Tile? Resolve(Slot slot, IReadOnlyList<AppEntry> apps, HashSet<string> used)
    {
        if (slot.Ids.Contains(YouTubeId)) return YouTube(apps);
        if (slot.Ids.Contains(GHelperPath))
            return File.Exists(GHelperPath) ? new Tile { AppId = GHelperPath, Label = "G-Helper", Target = GHelperPath, IsShellApp = false } : null;
        var candidates = apps.Where(a => !used.Contains(a.Id) && !a.LaunchTarget.StartsWith("http", StringComparison.OrdinalIgnoreCase)).ToArray();
        // Exact identity, then name prefix; the highest version wins ("Blender 5.2" over "Blender 4.5").
        var app = candidates.FirstOrDefault(a => slot.Ids.Any(id => a.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase)))
            ?? candidates.Where(a => slot.Names.Any(n => a.Name.Equals(n, StringComparison.OrdinalIgnoreCase)
                    || a.Name.StartsWith(n + " ", StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(a => a.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        if (app == null) return null;
        used.Add(app.Id);
        return Tile.FromApp(app);
    }

    /// <summary>YouTube opens in Brave when installed, else in the default browser.</summary>
    private static Tile YouTube(IReadOnlyList<AppEntry> apps)
    {
        const string url = "https://www.youtube.com/";
        var brave = apps.FirstOrDefault(a => a.Id.Equals("Brave", StringComparison.OrdinalIgnoreCase));
        return brave == null
            ? new Tile { AppId = YouTubeId, Label = "YouTube", Target = url, IsShellApp = false, UseCustomColor = true, Color = "#E62117" }
            : new Tile { AppId = YouTubeId, Label = "YouTube", Target = brave.LaunchTarget, IsShellApp = brave.IsShellApp, UseLaunchOptions = true, LaunchArguments = url, UseCustomColor = true, Color = "#E62117" };
    }

    /// <summary>Segoe MDL2 glyph for tablet shortcut tiles that have no catalog icon.</summary>
    internal static string? Glyph(Tile tile) => tile.AppId == YouTubeId ? "\uF5B0" : null; // PlaySolid

    /// <summary>Second instances of a package (the forecast) differ only by <c>settings.view</c> in their own copy.</summary>
    private static void ConfigureView(ScriptTilePackages packages, Tile tile, string view)
    {
        var folder = packages.Folder(tile.ScriptId);
        var path = Path.Combine(folder, "manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        manifest["settings"]!["view"] = view;
        if (view == "forecast") { manifest["title"] = "Forecast"; manifest.Remove("localizedTitles"); tile.Label = "Forecast"; }
        var (source, html, css) = (File.ReadAllText(Path.Combine(folder, "tile.js")), File.ReadAllText(Path.Combine(folder, "index.html")), File.ReadAllText(Path.Combine(folder, "tile.css")));
        packages.SaveInstalled(tile.ScriptId, manifest.ToJsonString(new() { WriteIndented = true }), source, html, css);
    }

    /// <summary>
    /// Brings installed dashboard tiles up to the bundled version (settings.tabletVersion): code and
    /// markup are replaced, while the installed title and settings values (entities, view, location) are kept.
    /// </summary>
    internal static void RefreshInstalledPackages(ScriptTilePackages packages)
    {
        if (!Directory.Exists(packages.Root)) return;
        foreach (var folder in Directory.EnumerateDirectories(packages.Root).Take(512))
        {
            var id = Path.GetFileName(folder);
            try
            {
                if (!Guid.TryParseExact(id, "N", out _) || !File.Exists(Path.Combine(folder, "manifest.json"))) continue;
                var installed = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json")))?.AsObject();
                if (installed?["settings"]?["tabletPackage"]?.GetValue<string>() is not { } name || !File.Exists(PackagePath(name))) continue;
                var bundledFolder = Path.GetDirectoryName(PackagePath(name))!;
                var bundled = JsonNode.Parse(File.ReadAllText(PackagePath(name)))!.AsObject();
                if ((bundled["settings"]?["tabletVersion"]?.GetValue<int>() ?? 0) <= (installed["settings"]?["tabletVersion"]?.GetValue<int>() ?? 0)) continue;
                var settings = bundled["settings"]!.AsObject();
                foreach (var (key, value) in installed["settings"]!.AsObject().ToArray())
                    if (key is not ("tabletPackage" or "tabletVersion")) settings[key] = value?.DeepClone();
                bundled["title"] = installed["title"]?.DeepClone() ?? bundled["title"]?.DeepClone();
                if (installed["localizedTitles"] == null) bundled.Remove("localizedTitles");
                string Read(string file) => File.ReadAllText(Path.Combine(bundledFolder, file));
                packages.SaveInstalled(id, bundled.ToJsonString(new() { WriteIndented = true }), Read("tile.js"), Read("index.html"), Read("tile.css"));
                TabletLog.Write("home", $"updated dashboard tile {name} ({id})");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or System.Text.Json.JsonException or FormatException)
            { TabletLog.Write("home", $"tile {id} not updated: {ex.Message}"); }
        }
    }

    /// <summary>Edits <c>settings</c> in this tile's own package copy.</summary>
    private static void PatchSettings(ScriptTilePackages packages, Tile tile, Action<JsonObject> patch)
    {
        var path = Path.Combine(packages.Folder(tile.ScriptId), "manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        patch(manifest["settings"]!.AsObject());
        File.WriteAllText(path, manifest.ToJsonString(new() { WriteIndented = true }));
    }

    /// <summary>
    /// Enables the weather tile's Open-Meteo connection for the configured location, as the user
    /// would in "Configure data sources…" (public endpoint, no credential).
    /// </summary>
    private static void ConnectWeather(ScriptTilePackages packages, Tile tile, string url)
    {
        try
        {
            ScriptDataBindings.Save(packages.DataBindingPath(tile.ScriptId), new()
            { ["forecast"] = new TileDataBinding { Enabled = true, Access = "http", Location = url, JsonPointer = "" } });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Security.Cryptography.CryptographicException)
        { TabletLog.Write("home", "weather connection not saved: " + ex.Message); }
    }
}
