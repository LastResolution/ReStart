using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ReStart.App.Tablet;
using ReStart.App.Tablet.Home;
using ReStart.App.Tablet.Media;
using ReStart.Core;

namespace ReStart.App;

// Tablet edition: what the Home screen shows. The tablet layout, the dashboard tiles' host
// bridges (ctx.tablet + ReStart.action) and the touch chrome of the board. The window
// behavior of Home (desktop replacement, dock, gestures) belongs to other features.
public partial class MainWindow
{
    private HomeAssistantClient? _homeAssistant;
    private SystemStatus? _tabletStatus;
    /// <summary>Tablet capabilities and allowed Home Assistant entities of each rendered dashboard tile.</summary>
    private readonly Dictionary<string, (string[] Capabilities, HashSet<string> Entities)> _tabletTiles = [];
    private (byte[]? Source, BitmapSource? Image, string Key) _mediaArt = (null, null, "");
    private Thickness? _laptopContentMargin, _laptopRailMargin;
    private long _touchPressAt, _stylusDownAt;
    private Point _touchPressPoint, _stylusDownPoint;
    private bool _touchDragLogged;

    /// <summary>Registers the Home commands and bridges. Called once by App startup (not in preview runs).</summary>
    internal void InitializeTabletHome()
    {
        var data = Path.GetDirectoryName(_store.FilePath)!;
        _homeAssistant = new(data);
        _homeAssistant.Updated += () => Dispatcher.BeginInvoke(() => WakeTabletTiles("tablet.home"));
        _tabletStatus = new();
        _tabletStatus.Updated += () => Dispatcher.BeginInvoke(() => WakeTabletTiles("tablet.system"));
        MediaSessions.Shared.Changed += _ => Dispatcher.BeginInvoke(() => WakeTabletTiles("tablet.media"));
        _ = MediaSessions.Shared.StartAsync();
        TabletCommands.Register("apply-layout", argument => { _ = ApplyTabletLayoutAsync(); });
        TabletCommands.Register("home-assistant", _ => ConnectHomeAssistant());
        TabletShell.TabletChanged += ApplyTabletChrome;
        // Tunneling window handlers run before each tile's own press handler.
        PreviewStylusDown += (_, e) => { _stylusDownAt = Environment.TickCount64; _stylusDownPoint = e.GetPosition(this); };
        PreviewMouseDown += (_, e) =>
        {
            _touchPressAt = FromTouch(e) ? Environment.TickCount64 : 0; _touchDragLogged = false;
            // The promoted press can carry the previous pointer position; the stylus point is exact.
            _touchPressPoint = Environment.TickCount64 - _stylusDownAt < 500 ? _stylusDownPoint : e.GetPosition(this);
        };
        PreviewMouseUp += (_, _) => _touchPressAt = 0;
        InstallTabletTaps();
        InstallTabletEditing();
        TabletHomeLayout.RefreshInstalledPackages(_scriptPackages);
        ApplyTabletChrome(TabletShell.IsTablet);
        Closed += (_, _) => { _homeAssistant.Dispose(); _tabletStatus.Dispose(); };
    }

    /// <summary>
    /// Replaces the board with the tablet Home layout. The previous layout.json is copied beside
    /// it first, and the change is one undoable edit (Ctrl+Z / Undo restores the groups).
    /// </summary>
    internal async Task ApplyTabletLayoutAsync()
    {
        await InitializeAsync();
        if (!CanEditLayout) { SetStatus(L.T(K.LayoutLockedStatus)); return; }
        try
        {
            if (File.Exists(_store.FilePath))
            {
                var backup = Path.Combine(Path.GetDirectoryName(_store.FilePath)!, $"layout.before-tablet-home-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.Copy(_store.FilePath, backup, overwrite: false);
                TabletLog.Write("home", "layout backed up to " + backup);
            }
            var groups = TabletHomeLayout.CreateGroups(_apps, _scriptPackages);
            TabletHomeLayout.Apply(Document.Preferences);
            Document.Groups = groups;
            ApplyAppearance();
            CommitLayout();
            var files = groups.SelectMany(g => g.Tiles).Where(t => t.Widget.Length == 0 && !t.IsShellApp).Select(t => new AppEntry(t.AppId, t.Label, t.Target, false)).ToArray();
            await _catalog.LoadIconsAsync(files);
            RenderGroups();
            TabletLog.Write("home", $"tablet layout applied: {string.Join(", ", groups.Select(g => g.Name + " (" + g.Tiles.Count + ")"))}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or JsonException)
        { TabletLog.Write("home", "apply-layout failed: " + ex.Message); SetStatus(ex.Message); }
    }

    private const int TouchHoldMs = 450;

    /// <summary>
    /// In tablet mode a touch (or pen) tap launches and a swipe scrolls. On the board, touch never starts
    /// OLE drag and drop: a long press enters the edit mode and the pointer stream moves the tile
    /// (MainWindow.TabletEditing.cs). Touch-promoted mouse events can also report a stale press point,
    /// which otherwise turns plain taps into drags. Elsewhere (All apps, search) a press and hold still drags.
    /// </summary>
    private bool TabletAllowsDrag(MouseEventArgs e)
    {
        if (!TabletShell.IsTablet || _touchPressAt == 0 && !FromTouch(e) && _touch == null) return true;
        var point = e.GetPosition(this);
        var moved = Math.Abs(point.X - _touchPressPoint.X) >= 10 || Math.Abs(point.Y - _touchPressPoint.Y) >= 10;
        var board = _touch != null || GroupsPanel.IsVisible;
        var allowed = !board && moved && _touchPressAt != 0 && Environment.TickCount64 - _touchPressAt >= TouchHoldMs;
        if (!_touchDragLogged && e.LeftButton == MouseButtonState.Pressed && moved)
        {
            _touchDragLogged = true;
            TabletLog.Write("home", $"promoted touch drag {(allowed ? "started" : board ? "left to the pointer gesture" : "suppressed (no hold)")} stylus={e.StylusDevice != null} editing={_layoutEditing}");
        }
        return allowed;
    }

    /// <summary>Where a tap landed: touch releases report the finger, while the promoted press may not.</summary>
    private static Point? TabletClickPoint(MouseButtonEventArgs e, IInputElement button, Point? pressed) => FromTouch(e) ? e.GetPosition(button) : pressed;

    /// <summary>Mouse input synthesized from touch or pen: WPF's stylus promotion, or Windows' own (MI_WP_SIGNATURE in the message extra info).</summary>
    private static bool FromTouch(MouseEventArgs e) => e.StylusDevice != null || (GetMessageExtraInfo() & 0xFFFFFF00) == 0xFF515700;

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint GetMessageExtraInfo();

    /// <summary>Touch chrome: tighter side margins, room for the dock and home bar, no scroll bars.</summary>
    private void ApplyTabletChrome(bool tablet)
    {
        _laptopContentMargin ??= MainContent.Margin;
        _laptopRailMargin ??= NavigationRail.Margin;
        // The shelf and the home bar are Home's bottom edge in tablet mode: the board ends one tile
        // gap above them and the rail's bottom buttons sit on them, so nothing slides under the shelf.
        var shelf = Tablet.Design.TabletStyle.ShelfHeight + Tablet.Gestures.HomeBar.BarHeight;
        MainContent.Margin = tablet ? new Thickness(16, 16, 16, shelf + Tablet.Design.TabletStyle.Gap) : _laptopContentMargin.Value;
        NavigationRail.Margin = tablet ? new Thickness(0, 0, 0, shelf) : _laptopRailMargin.Value;
        MainContent.RowDefinitions[3].Height = new GridLength(tablet ? 0 : 28);
        ContentScroll.VerticalScrollBarVisibility = tablet ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;
        ContentScroll.HorizontalScrollBarVisibility = tablet ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;
        ContentScroll.PanningMode = tablet ? PanningMode.VerticalFirst : PanningMode.Both;
        FitPanels();
        foreach (var id in _tabletTiles.Keys) if (_scriptSessions.TryGetValue(id, out var session)) session.NextUpdate = DateTimeOffset.MinValue;
    }

    private void WakeTabletTiles(string capability)
    {
        if (_exit || !IsVisible) return;
        var due = false;
        foreach (var (id, tile) in _tabletTiles)
            if (tile.Capabilities.Contains(capability) && _scriptSessions.TryGetValue(id, out var session)) { session.NextUpdate = DateTimeOffset.MinValue; due = true; }
        if (due && !_startupPreparing) _ = UpdateScriptTilesAsync();
    }

    /// <summary>ctx.tablet for packages declaring tablet.* capabilities; null for every other tile.</summary>
    private object? TabletTileContext(Tile tile, ScriptTileManifest manifest)
    {
        var capabilities = manifest.Capabilities.Where(c => c.StartsWith("tablet.", StringComparison.Ordinal)).ToArray();
        if (capabilities.Length == 0 || _homeAssistant == null || _tabletStatus == null) { _tabletTiles.Remove(tile.Id); return null; }
        var entities = new HashSet<string>(StringComparer.Ordinal);
        object? media = null, home = null, system = null;
        if (capabilities.Contains("tablet.media")) media = MediaContext(tile);
        if (capabilities.Contains("tablet.home"))
        {
            foreach (var list in new[] { "lights", "scenes" })
                if (manifest.Settings.TryGetProperty(list, out var items) && items.ValueKind == JsonValueKind.Array)
                    foreach (var item in items.EnumerateArray().Take(16))
                        if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("entity", out var entity) && HomeAssistantClient.ValidEntity(entity.GetString()))
                            entities.Add(entity.GetString()!);
            var (status, states, updatedAt) = _homeAssistant.Snapshot(entities);
            home = new
            {
                status,
                updatedAt = updatedAt?.ToString("O"),
                states = states.Where(p => entities.Contains(p.Key)).ToDictionary(p => p.Key, p => new { on = p.Value.On, available = p.Value.Available, brightness = p.Value.Brightness, color = p.Value.Color })
            };
        }
        if (capabilities.Contains("tablet.system")) system = _tabletStatus.Snapshot();
        _tabletTiles[tile.Id] = (capabilities, entities);
        return new
        {
            posture = TabletShell.IsTablet ? "tablet" : "laptop",
            orientation = ActualWidth >= ActualHeight ? "landscape" : "portrait",
            media, home, system
        };
    }

    private object MediaContext(Tile tile)
    {
        var current = MediaSessions.Shared.Current;
        if (current?.Thumbnail != _mediaArt.Source)
        {
            BitmapSource? image = null;
            if (current?.Thumbnail is { Length: > 0 } bytes)
                try
                {
                    var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 480; bitmap.StreamSource = new MemoryStream(bytes); bitmap.EndInit(); bitmap.Freeze(); image = bitmap;
                }
                catch (Exception ex) when (ex is NotSupportedException or IOException or ArgumentException or InvalidOperationException) { }
            _mediaArt = (current?.Thumbnail, image, image == null ? "" : Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(current!.Thumbnail!))[..12]);
        }
        // The browser host serves this tile's single image at /slideshow.png (same origin, no path exposed).
        if (_mediaArt.Image != null && current != null) _slideshowImages[tile.Id] = _mediaArt.Image; else _slideshowImages.Remove(tile.Id);
        if (current == null) return new { status = "idle" };
        return new
        {
            status = current.IsPlaying ? "playing" : "paused",
            title = current.Title, artist = current.Artist, album = current.Album,
            app = MediaAppName(current.SourceApp),
            position = Math.Max(0, current.Position.TotalSeconds),
            duration = Math.Max(0, current.Duration.TotalSeconds),
            sampledAt = current.Updated.ToUnixTimeMilliseconds(),
            art = _mediaArt.Image == null ? null : "/slideshow.png?art=" + _mediaArt.Key
        };
    }

    private string MediaAppName(string aumid)
    {
        if (_apps.FirstOrDefault(a => a.Id.Equals(aumid, StringComparison.OrdinalIgnoreCase)) is { } app) return app.Name;
        var name = Path.GetFileNameWithoutExtension(aumid.Split('!')[0]);
        name = name.Split('_')[0];
        if (name.Contains('.')) name = name[(name.LastIndexOf('.') + 1)..];
        return name.Length == 0 ? "" : char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>Runs a validated request from a dashboard tile (ReStart.action); everything else is ignored.</summary>
    private void OnTabletTileAction(Tile tile, string name, JsonElement value)
    {
        if (_exit || _layoutEditing || !_tabletTiles.TryGetValue(tile.Id, out var granted)) return;
        var argument = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        var area = name.Split('.')[0];
        if (!granted.Capabilities.Contains("tablet." + area)) { TabletLog.Write("home", $"denied {name} from {tile.Label}"); return; }
        TabletLog.Write("home", "action " + name + (argument == null ? "" : " " + argument));
        switch (name)
        {
            case "media.toggle": _ = MediaSessions.Shared.PlayPauseAsync(); break;
            case "media.next": _ = MediaSessions.Shared.NextAsync(); break;
            case "media.previous": _ = MediaSessions.Shared.PreviousAsync(); break;
            case "media.open": OpenMediaApp(); break;
            case "home.run" when argument != null && granted.Entities.Contains(argument): _ = _homeAssistant!.RunAsync(argument); break;
            case "home.connect": ConnectHomeAssistant(); break;
            case "system.battery": LaunchPath("ms-settings:batterysaver"); break;
            case "system.performance" when File.Exists(@"C:\Program Files\GHelper\GHelper.exe"): LaunchPath(@"C:\Program Files\GHelper\GHelper.exe"); break;
            case "system.performance": LaunchPath("ms-settings:powersleep"); break;
            default: TabletLog.Write("home", $"unknown action {name}"); break;
        }
    }

    /// <summary>Brings the playing app forward (full screen in tablet mode).</summary>
    private void OpenMediaApp()
    {
        if (MediaSessions.Shared.Current?.SourceApp is not { Length: > 0 } aumid) return;
        var key = Path.GetFileNameWithoutExtension(aumid.Split('!')[0]).Split('_')[0];
        var window = AppWindows.List().FirstOrDefault(w => w.ExePath is { } exe && Path.GetFileNameWithoutExtension(exe).Equals(key, StringComparison.OrdinalIgnoreCase))
            ?? AppWindows.List().FirstOrDefault(w => w.ExePath is { } exe && aumid.Contains(Path.GetFileNameWithoutExtension(exe), StringComparison.OrdinalIgnoreCase));
        if (window != null) { AppWindows.Activate(window.Handle, TabletShell.IsTablet); return; }
        try { Services.ShellLauncher.Launch(aumid, shellApp: true); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { TabletLog.Write("home", "media app: " + ex.Message); }
    }

    private void ConnectHomeAssistant()
    {
        if (_homeAssistant == null) return;
        if (!IsVisible) Present();
        WithDialog(() => new HomeAssistantWindow(this, _homeAssistant).ShowDialog());
    }

    private void AddTabletTileMenu(ContextMenu menu, Tile tile)
    {
        try
        {
            if (_homeAssistant == null || !_scriptPackages.Read(tile.ScriptId).Manifest.Capabilities.Contains("tablet.home")) return;
            Add(menu, _homeAssistant.IsConfigured ? "Home Assistant connection…" : "Connect Home Assistant…", ConnectHomeAssistant);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException) { }
    }
}
