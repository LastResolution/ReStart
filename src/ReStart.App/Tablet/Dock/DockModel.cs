using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using ReStart.App.Services;
using ReStart.App.Tablet.Gestures;
using ReStart.Core;

namespace ReStart.App.Tablet.Dock;

/// <summary>One dock slot: a favorite (pinned) or a recently used running app.</summary>
internal sealed class DockItem
{
    internal required string Name { get; init; }
    internal AppEntry? Entry { get; init; }
    internal string? Process { get; init; }
    internal ImageSource? Icon { get; set; }
    internal bool Favorite { get; init; }
    /// <summary>Most recent window of this app, or 0 when it isn't running.</summary>
    internal nint Window { get; set; }
    internal string Key => Favorite ? "fav:" + Name : "run:" + Process;
}

/// <summary>
/// Dock contents: favorites from %LOCALAPPDATA%\ReStart\tablet-dock.json (written with
/// defaults on first use), resolved against ReStart's app catalog, plus a few recent apps.
/// </summary>
internal sealed class DockModel
{
    internal static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReStart", "tablet-dock.json");

    private sealed class Config
    {
        [JsonPropertyName("favorites")] public List<Favorite> Favorites { get; set; } = [];
        [JsonPropertyName("recents")] public int Recents { get; set; } = 3;
    }
    private sealed class Favorite
    {
        /// <summary>App name as shown in Start (exact, prefix or substring match).</summary>
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        /// <summary>Executable that identifies its running windows.</summary>
        [JsonPropertyName("process")] public string? Process { get; set; }
        /// <summary>Optional launch target when the app isn't in the catalog.</summary>
        [JsonPropertyName("path")] public string? Path { get; set; }
    }

    private static readonly Config Defaults = new()
    {
        Favorites =
        [
            new() { Name = "Brave", Process = "brave.exe" },
            new() { Name = "Spotify", Process = "Spotify.exe" },
            new() { Name = "Discord", Process = "Discord.exe" },
            new() { Name = "Signal", Process = "Signal.exe" },
            new() { Name = "File Explorer", Process = "explorer.exe" },
            new() { Name = "Photoshop 2026", Process = "Photoshop.exe" },
            new() { Name = "Illustrator 2026", Process = "Illustrator.exe" },
            new() { Name = "Blender 5.2", Process = "blender.exe" },
        ],
    };

    private Config _config = Defaults;
    private List<DockItem> _favorites = [];
    private bool _loading;
    private DateTime _configTime;

    /// <summary>Raised on the UI thread when favorites (names, icons) were resolved.</summary>
    internal event Action? Changed;
    internal int RecentCount => Math.Clamp(_config.Recents, 0, 6);

    /// <summary>Loads the config and resolves favorites in the background (once, and again after the file changes).</summary>
    internal void EnsureLoaded()
    {
        var time = File.Exists(ConfigPath) ? File.GetLastWriteTimeUtc(ConfigPath) : default;
        if (_loading || (_resolved || _failures > 3) && time == _configTime) return;
        _loading = true;
        // Start's own catalog and icon workers use the same shell COM objects (free-threaded,
        // so shared RCWs): enumerating at the same time makes one release the other's.
        if (System.Windows.Application.Current?.MainWindow is MainWindow { IconsReady: false } && _waits++ < 60)
        {
            if (_favorites.Count == 0) _favorites = ReadConfig().Favorites.Where(f => f.Name.Length > 0).Select(f => new DockItem { Name = f.Name, Process = f.Process, Favorite = true }).ToList();
            Retry(TimeSpan.FromSeconds(1));
            return;
        }
        _config = ReadConfig();
        _configTime = File.Exists(ConfigPath) ? File.GetLastWriteTimeUtc(ConfigPath) : default;
        _favorites = _config.Favorites.Where(f => f.Name.Length > 0).Select(f => new DockItem { Name = f.Name, Process = f.Process, Favorite = true }).ToList();
        var specs = _config.Favorites.ToList();
        var dispatcher = TabletShell.Dispatcher!;
        var thread = new Thread(() =>
        {
            List<DockItem> resolved;
            try { resolved = Resolve(specs); }
            catch (Exception ex) { TabletLog.Write("dock", "catalog failed: " + ex.Message); resolved = []; }
            dispatcher.BeginInvoke(() =>
            {
                if (resolved.Count == 0 && _failures++ < 3) { Retry(TimeSpan.FromSeconds(4)); return; }
                if (resolved.Count > 0) { _favorites = resolved; _resolved = true; }
                _loading = false;
                TabletLog.Write("dock", "favorites: " + string.Join(", ", _favorites.Select(f => f.Name + (f.Entry == null ? " (not found)" : ""))));
                Changed?.Invoke();
            });
        }) { IsBackground = true, Name = "ReStart tablet dock" };
        thread.SetApartmentState(ApartmentState.STA); // shell catalog and icons are STA COM
        thread.Start();
    }

    private int _waits, _failures;
    private bool _resolved;

    private void Retry(TimeSpan delay)
    {
        TabletNavigation.Later(delay, () => { _loading = false; EnsureLoaded(); });
    }

    private static Config ReadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath), new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) ?? Defaults;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Defaults, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { TabletLog.Write("dock", "config: " + ex.Message); }
        return Defaults;
    }

    // Runs on an STA worker: the same catalog and icon pipeline as Start's tiles.
    private static List<DockItem> Resolve(List<Favorite> specs)
    {
        var catalog = new AppCatalog(Path.Combine(Path.GetDirectoryName(ConfigPath)!, "icon-cache"));
        var apps = catalog.LoadAsync().GetAwaiter().GetResult();
        var items = new List<DockItem>();
        foreach (var spec in specs.Where(s => s.Name.Length > 0))
        {
            var entry = apps.FirstOrDefault(a => a.Name.Equals(spec.Name, StringComparison.OrdinalIgnoreCase))
                ?? apps.FirstOrDefault(a => a.Name.StartsWith(spec.Name, StringComparison.OrdinalIgnoreCase))
                ?? apps.FirstOrDefault(a => a.Name.Contains(spec.Name, StringComparison.OrdinalIgnoreCase));
            if (entry == null && spec.Path is { Length: > 0 } path) entry = new AppEntry(path, spec.Name, path, false);
            var target = entry == null ? null : entry.IsShellApp ? "shell:AppsFolder\\" + entry.LaunchTarget : entry.LaunchTarget;
            var process = spec.Process ?? (entry?.LaunchTarget.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true ? Path.GetFileName(entry.LaunchTarget) : null);
            items.Add(new DockItem { Name = spec.Name, Entry = entry, Process = process, Favorite = true, Icon = target == null ? null : AppCatalog.GetIcon(target, 128) });
        }
        return items;
    }

    /// <summary>Favorites with their running windows, then up to <see cref="RecentCount"/> other running apps (most recent first).</summary>
    internal List<DockItem> Items()
    {
        var recent = TabletNavigation.Recent();
        foreach (var favorite in _favorites)
            favorite.Window = recent.FirstOrDefault(w => Matches(favorite, w))?.Handle ?? 0;
        var items = new List<DockItem>(_favorites);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var window in recent)
        {
            if (items.Count - _favorites.Count >= RecentCount) break;
            var exe = AppInfo.ExeName(window);
            if (exe.Length == 0 || _favorites.Any(f => Matches(f, window)) || !seen.Add(exe)) continue;
            items.Add(new DockItem { Name = AppInfo.Name(window), Process = exe, Window = window.Handle, Icon = AppInfo.Icon(window, 128) });
        }
        return items;
    }

    private static bool Matches(DockItem favorite, AppWindow window)
    {
        if (favorite.Process is { Length: > 0 } process && AppInfo.ExeName(window).Equals(process, StringComparison.OrdinalIgnoreCase)) return true;
        return favorite.Entry is { IsShellApp: true } entry && AppInfo.PackagedAumid(window.Handle) is { } aumid && aumid.Equals(entry.LaunchTarget, StringComparison.OrdinalIgnoreCase);
    }
}
