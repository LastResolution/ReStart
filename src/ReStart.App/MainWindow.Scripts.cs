using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ReStart.App.Services;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private readonly List<(Tile Tile, StackPanel Body, Button Button)> _scriptViews = [];
    private sealed class ScriptRuntime
    {
        internal DateTimeOffset NextUpdate;
        internal System.Text.Json.JsonElement State = System.Text.Json.JsonSerializer.SerializeToElement(new { });
        internal Queue<ScriptInput> Inputs = new();
    }
    private sealed record ScriptInput(string Type, long UnixMs);
    private readonly Dictionary<string, ScriptRuntime> _scriptSessions = [];
    private ScriptTileWorkerPool? _scriptWorkerPool;
    private readonly Dictionary<string, ScriptTileFrame> _scriptFrames = [];
    private ScriptTilePackages _scriptPackages = null!;
    internal ScriptTilePackages ScriptPackages => _scriptPackages;
    private bool _scriptRendering;
    private string? _scriptLanguage;
    private SystemMetricsSampler? _systemMetrics;
    private TileDataTransport? _tileDataTransport;
    private readonly Dictionary<string, (string Package, ScriptDataRuntime Runtime)> _tileData = [];
    private readonly Dictionary<string, (string Package, SlideshowSource Source)> _slideshowSources = [];
    private readonly Dictionary<string, BitmapSource> _slideshowImages = [];
    /// <summary>Gets the number of live root JavaScript tiles in the current document.</summary>
    public int ScriptTileCount => Document.Groups.SelectMany(g => g.Tiles).Count(t => t.Widget == "script" && t.Live);
    /// <summary>Gets the number of cached successful JavaScript frames.</summary>
    public int ScriptFrameCount => _scriptFrames.Count;
    /// <summary>Gets whether all live root JavaScript tiles have produced a successful frame.</summary>
    public bool ScriptsReady => Document.Groups.SelectMany(g => g.Tiles).Where(t => t.Widget == "script" && t.Live).All(t => _scriptFrames.ContainsKey(t.Id));

    /// <summary>Installs the bundled clock sample, pins it, and explicitly opens its source in Notepad.</summary>
    public void CreateScriptTile()
    {
        try
        {
            var tile = _scriptPackages.Import(Path.Combine(AppContext.BaseDirectory, "Examples", "clock", "manifest.json"));
            PinCustom(tile); _scriptPackages.Edit(tile.ScriptId);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception or System.Text.Json.JsonException) { SetStatus(ex.Message); }
    }

    internal Tile? AddBundledScriptTile(string id)
        => AddScriptPackage(ScriptTilePackages.BundledManifestPath(id));

    internal Tile? AddScriptPackage(string manifestPath)
    {
        try { var tile = _scriptPackages.Import(manifestPath); PinCustom(tile); return tile; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or System.Text.Json.JsonException)
        { SetStatus(ex.Message); return null; }
    }

    /// <summary>Copies a selected manifest and tile.js into an installed package and pins the new tile.</summary>
    public void ImportScriptTile()
    {
        WithDialog(() =>
        {
            var dialog = new OpenFileDialog { Title = L.T(K.PromptScriptManifest), Filter = "manifest.json|manifest.json" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                var tile = _scriptPackages.Import(dialog.FileName); PinCustom(tile);
                if (_scriptPackages.Read(tile.ScriptId).Manifest.Capabilities.Contains("data.sources")) ConfigureTileData(tile, this);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or System.Text.Json.JsonException) { SetStatus(ex.Message); }
        });
    }

    private void AddScriptMenu(ContextMenu menu, Tile tile)
    {
        try
        {
            var manifest = _scriptPackages.Read(tile.ScriptId).Manifest;
            if (manifest.Capabilities.Contains("images.slideshow"))
                Add(menu, L.T(K.ChooseImageFolder), () => ChooseSlideshowFolder(tile, this));
            if (manifest.Capabilities.Contains("data.sources"))
            {
                Add(menu, L.T(K.ConfigureTileData), () => ConfigureTileData(tile, this));
                Add(menu, L.T(K.RefreshTileData), () => { if (_tileData.TryGetValue(tile.Id, out var data)) data.Runtime.Snapshot(DateTimeOffset.Now, true); });
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException) { }
        Add(menu, L.T(K.EditScript), () => EditScript(tile));
        Add(menu, L.T(K.EditScriptSettings), () => EditScript(tile, "manifest.json"));
        Add(menu, L.T(K.OpenScriptFolder), () =>
        {
            try { ShellLauncher.Launch(_scriptPackages.Folder(tile.ScriptId)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception) { SetStatus(ex.Message); }
        });
        Add(menu, L.T(K.ReloadScript), () =>
        {
            RetireTileData(tile.Id);
            _scriptSessions.Remove(tile.Id);
            _scriptFrames.Remove(tile.Id);
            _scriptPackages.InvalidateTitle(tile.ScriptId);
            CommitLayout();
        });
    }
    private void EditScript(Tile tile, string file = "tile.js")
    {
        try { _scriptPackages.Edit(tile.ScriptId, file); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception) { SetStatus(ex.Message); }
    }

    private void InteractWithScriptTile(Tile tile)
    {
        if (!tile.Live) return;
        if (!_scriptSessions.TryGetValue(tile.Id, out var runtime)) _scriptSessions[tile.Id] = runtime = new();
        if (runtime.Inputs.Count < 16) runtime.Inputs.Enqueue(new("click", DateTimeOffset.Now.ToUnixTimeMilliseconds()));
        runtime.NextUpdate = DateTimeOffset.MinValue;
        if (_slideshowSources.TryGetValue(tile.Id, out var photos)) photos.Source.Advance(DateTimeOffset.Now);
        _ = UpdateScriptTilesAsync();
    }

    internal void ChooseSlideshowFolder(Tile tile, Window owner)
    {
        WithDialog(() =>
        {
            var dialog = new OpenFolderDialog { Title = L.T(K.ChooseImageFolder) };
            if (dialog.ShowDialog(owner) != true) return;
            try
            {
                SlideshowSource.Bind(_scriptPackages.SlideshowBindingPath(tile.ScriptId), dialog.FolderName);
                if (_scriptSessions.TryGetValue(tile.Id, out var runtime)) runtime.NextUpdate = DateTimeOffset.MinValue;
                SetStatus(L.T(K.SlideshowFolderSaved));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException) { SetStatus(ex.Message); }
        });
    }

    private async Task UpdateScriptTilesAsync()
    {
        if (_scriptRendering || !IsVisible) return;
        _scriptRendering = true;
        try
        {
            var current = _scriptViews.Where(v => v.Body.IsVisible).ToArray();
            var pinnedIds = Document.Groups.SelectMany(g => g.Tiles).SelectMany(t => new[] { t }.Concat(t.Children)).Select(t => t.Id).ToHashSet();
            foreach (var id in _tileData.Keys.Where(id => !pinnedIds.Contains(id)).ToArray()) RetireTileData(id);
            foreach (var data in _tileData.Where(p => !current.Any(v => v.Tile.Id == p.Key))) data.Value.Runtime.Stop();
            if (current.Length == 0) { PauseScripts(); return; }
            var pool = _scriptWorkerPool ??= new();
            await Task.WhenAll(current.Select(async view =>
            {
                if (!_scriptSessions.TryGetValue(view.Tile.Id, out var session)) _scriptSessions[view.Tile.Id] = session = new();
                if (session.NextUpdate > DateTimeOffset.Now) return;
                session.NextUpdate = DateTimeOffset.Now.AddSeconds(5);
                try
                {
                    var (manifest, source) = _scriptPackages.Read(view.Tile.ScriptId);
                    session.NextUpdate = DateTimeOffset.Now.AddSeconds(manifest.RefreshSeconds);
                    var now = DateTimeOffset.Now;
                    var input = session.Inputs.TryPeek(out var pendingInput) ? pendingInput : null;
                    SystemMetricsSnapshot? metrics = null;
                    if (manifest.Capabilities.Contains("system.metrics"))
                    {
                        if (_preview && !_verifyHostMetrics) metrics = new(now, 37.2, 16, 32UL * 1024 * 1024 * 1024, 12UL * 1024 * 1024 * 1024, 62.5, 91800,
                            [new("C:\\", 512L * 1024 * 1024 * 1024, 214L * 1024 * 1024 * 1024, 58.2)], 425984, 65536, 74, false);
                        else { _systemMetrics ??= new(); _systemMetrics.SetEnabled(true); metrics = _systemMetrics.Latest; }
                    }
                    SlideshowSource.Snapshot? slideshow = null;
                    if (manifest.Capabilities.Contains("images.slideshow"))
                    {
                        if (!_slideshowSources.TryGetValue(view.Tile.Id, out var binding) || binding.Package != view.Tile.ScriptId)
                        {
                            var sourceProvider = new SlideshowSource(_scriptPackages.SlideshowBindingPath(view.Tile.ScriptId));
                            sourceProvider.Updated += () => Dispatcher.BeginInvoke(() =>
                            {
                                if (!_exit && _slideshowSources.TryGetValue(view.Tile.Id, out var currentSource) && currentSource.Source == sourceProvider
                                    && _scriptSessions.TryGetValue(view.Tile.Id, out var runtime)) runtime.NextUpdate = DateTimeOffset.MinValue;
                            });
                            _slideshowSources[view.Tile.Id] = binding = (view.Tile.ScriptId, sourceProvider);
                        }
                        var interval = manifest.Settings.TryGetProperty("intervalSeconds", out var intervalValue) && intervalValue.ValueKind == System.Text.Json.JsonValueKind.Number && intervalValue.TryGetInt32(out var seconds) ? seconds : 15;
                        slideshow = binding.Source.Refresh(now, interval);
                        if (slideshow.Image != null) _slideshowImages[view.Tile.Id] = slideshow.Image; else _slideshowImages.Remove(view.Tile.Id);
                    }
                    else _slideshowImages.Remove(view.Tile.Id);
                    Dictionary<string, TileDataSnapshot>? data = null;
                    if (manifest.Capabilities.Contains("data.sources"))
                    {
                        try { data = ReadTileData(view.Tile, manifest, now); }
                        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Security.Cryptography.CryptographicException or System.Text.Json.JsonException)
                        { data = manifest.DataSources.ToDictionary(p => p.Key, _ => new TileDataSnapshot("error", System.Text.Json.JsonSerializer.SerializeToElement<object?>(null), null, null, false, new("configuration"))); }
                    }
                    else RetireTileData(view.Tile.Id);
                    var (frame, error) = await pool.RenderAsync(source, new
                    {
                        now = now.ToString("O"),
                        locale = L.Culture.Name,
                        language = L.Language,
                        time = new { unixMs = now.ToUnixTimeMilliseconds(), monotonicMs = Environment.TickCount64, utc = now.UtcDateTime.ToString("O"), local = now.ToString("O"), offsetMinutes = now.Offset.TotalMinutes, timeZoneId = TimeZoneInfo.Local.Id },
                        calendar = new { shortDate = now.ToString("d", L.Culture), weekday = now.ToString("dddd", L.Culture), month = now.ToString("MMMM", L.Culture), day = now.Day },
                        @event = input,
                        system = metrics,
                        data,
                        slideshow = slideshow == null ? null : new { available = slideshow.Image != null, count = slideshow.Count, index = slideshow.Index },
                        size = view.Tile.Size.ToString().ToLowerInvariant(),
                        bounds = new { width = view.Button.ActualWidth, height = view.Button.ActualHeight, scale = Document.Preferences.TileScale },
                        settings = manifest.Settings,
                        state = session.State
                    });
                    if (!_scriptSessions.TryGetValue(view.Tile.Id, out var latest) || latest != session || !IsVisible || _scriptWorkerPool != pool) return;
                    if (input != null && session.Inputs.TryPeek(out var queued) && queued == input) session.Inputs.Dequeue();
                    if (session.Inputs.Count > 0) session.NextUpdate = DateTimeOffset.MinValue;
                    if (frame != null) { _scriptFrames[view.Tile.Id] = frame; session.State = frame.State; }
                    foreach (var target in _scriptViews.Where(target => target.Tile.Id == view.Tile.Id))
                    {
                        if (frame != null) ApplyScriptFrame(target.Body, target.Button, target.Tile, frame);
                        target.Button.ToolTip = error == null ? TileDisplayName(target.Tile) : L.T(K.ErrorScriptPrefix) + error;
                        if (frame == null && target.Body.Children.Count == 0) target.Body.Children.Add(new TextBlock { Text = L.T(K.ErrorScript), TextWrapping = TextWrapping.Wrap });
                    }
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or System.Text.Json.JsonException)
                { view.Button.ToolTip = L.T(K.ErrorScriptPrefix) + ex.Message; if (view.Body.Children.Count == 0) view.Body.Children.Add(new TextBlock { Text = L.T(K.ErrorScript) }); }
            }));
        }
        finally { _scriptRendering = false; }
    }
    private void ApplyScriptFrame(StackPanel body, Button button, Tile tile, ScriptTileFrame frame)
    {
        body.Children.Clear();
        var title = TileDisplayName(tile);
        System.Windows.Automation.AutomationProperties.SetName(button, title);
        if (button.Content is Grid tileGrid)
            foreach (var label in tileGrid.Children.OfType<TextBlock>().Where(text => text.Tag == tile)) label.Text = title;
        if (!SystemParameters.HighContrast && !tile.UseCustomColor)
        {
            button.Background = frame.Background.Length == 0 ? (System.Windows.Media.Brush)FindResource("AccentBrush") : Brush(frame.Background, Document.Preferences.Accent);
            if (frame.BackgroundEnd.Length > 0 && button.Background is SolidColorBrush start)
                button.Background = new LinearGradientBrush(start.Color, ((SolidColorBrush)Brush(frame.BackgroundEnd, "#101820")).Color, 35);
            button.Foreground = frame.Foreground.Length == 0 ? Brushes.White : Brush(frame.Foreground, "#FFFFFF");
        }
        if (body.Parent is Grid parent && parent.Children.OfType<ScriptTileArt>().FirstOrDefault() is { } art)
        {
            ArrangeScriptFrame(parent, body, art, tile, frame);
            art.Frame = frame; art.Ink = frame.Accent.Length == 0 || tile.UseCustomColor ? button.Foreground : Brush(frame.Accent, "#FFFFFF");
            art.InvalidateVisual();
        }
        if (button.Content is Grid imageParent && imageParent.Children.OfType<SlideshowTileView>().FirstOrDefault() is { } photos)
            photos.SetSource(frame.Image == "current" && !tile.UseCustomColor && !SystemParameters.HighContrast && _slideshowImages.TryGetValue(tile.Id, out var image) ? image : null,
                Document.Preferences.Animate);
        if (tile.Size != TileSize.Small && frame.Kicker.Length > 0)
            body.Children.Add(new TextBlock { Text = frame.Kicker, FontSize = 10, Opacity = 0.8, MaxHeight = 12, TextTrimming = TextTrimming.CharacterEllipsis });
        body.Children.Add(new TextBlock
        {
            Text = frame.Text,
            Tag = "ScriptValue",
            FontSize = tile.Size == TileSize.Small ? 13 * Document.Preferences.TileScale : frame.Layout == "caption" ? 23
            : tile.Size == TileSize.Medium && frame.Text.Contains('\n') ? 18 : tile.Size == TileSize.Large ? 34 : tile.Size == TileSize.Wide ? 23 : 25,
            LineHeight = tile.Size == TileSize.Small ? 15 * Document.Preferences.TileScale : double.NaN,
            LineStackingStrategy = tile.Size == TileSize.Small ? LineStackingStrategy.BlockLineHeight : LineStackingStrategy.MaxHeight,
            FontWeight = FontWeights.Light,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = tile.Size == TileSize.Large ? 80 : tile.Size == TileSize.Small ? 32 * Document.Preferences.TileScale : frame.Text.Contains('\n') ? 48 : 36,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (tile.Size != TileSize.Small)
        {
            if (frame.Detail.Length > 0) body.Children.Add(new TextBlock { Text = frame.Detail, FontSize = 12, Opacity = 0.85, TextWrapping = TextWrapping.Wrap, MaxHeight = tile.Size == TileSize.Large ? 60 : 18, TextTrimming = TextTrimming.CharacterEllipsis });
            if (frame.Badge.Length > 0) body.Children.Add(new TextBlock { Text = frame.Badge, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right });
            if (frame.Visual.Length == 0 && frame.Progress is double progress)
            {
                var track = new Grid { Height = 3, Margin = new(0, 4, 0, 0), Background = new SolidColorBrush(Color.FromArgb(50, 128, 128, 128)) };
                track.ColumnDefinitions.Add(new() { Width = new GridLength(progress, GridUnitType.Star) });
                track.ColumnDefinitions.Add(new() { Width = new GridLength(1 - progress, GridUnitType.Star) });
                var fill = new Border { Background = button.Foreground }; track.Children.Add(fill); body.Children.Add(track);
            }
        }
        if (frame.Layout == "centered" || tile.Size == TileSize.Small)
            foreach (var text in body.Children.OfType<TextBlock>()) text.TextAlignment = TextAlignment.Center;
    }

    private static void ArrangeScriptFrame(Grid plot, StackPanel body, ScriptTileArt art, Tile tile, ScriptTileFrame frame)
    {
        plot.ColumnDefinitions.Clear(); plot.RowDefinitions.Clear();
        Grid.SetColumn(body, 0); Grid.SetRow(body, 0); Grid.SetColumn(art, 0); Grid.SetRow(art, 0);
        body.VerticalAlignment = frame.Layout == "caption" ? VerticalAlignment.Bottom : VerticalAlignment.Center;
        body.Margin = new(0); art.Margin = new(0);
        art.FillRegion = frame.Layout is "split" or "stacked";
        art.Visibility = tile.Size == TileSize.Small || frame.Layout is "centered" or "caption" ? Visibility.Collapsed : Visibility.Visible;
        if (art.Visibility == Visibility.Collapsed || frame.Visual.Length == 0) return;
        if (frame.Layout == "split")
        {
            plot.ColumnDefinitions.Add(new() { Width = new(3, GridUnitType.Star) });
            plot.ColumnDefinitions.Add(new() { Width = new(2, GridUnitType.Star) });
            Grid.SetColumn(art, 1); body.Margin = new(0, 0, 4, 0);
        }
        else if (frame.Layout == "stacked")
        {
            plot.RowDefinitions.Add(new() { Height = new(45, GridUnitType.Star) });
            plot.RowDefinitions.Add(new() { Height = new(55, GridUnitType.Star) });
            Grid.SetRow(art, 1); art.Margin = new(0, 6, 0, 0);
        }
    }
    private void PauseScripts()
    {
        _scriptWorkerPool?.Dispose(); _scriptWorkerPool = null; _systemMetrics?.SetEnabled(false);
        foreach (var data in _tileData.Values) data.Runtime.Stop();
    }

    private Dictionary<string, TileDataSnapshot> ReadTileData(Tile tile, ScriptTileManifest manifest, DateTimeOffset now)
    {
        var signature = System.Text.Json.JsonSerializer.Serialize(manifest.DataSources, ScriptTileProtocol.Json);
        if (!_tileData.TryGetValue(tile.Id, out var current) || current.Package != tile.ScriptId || current.Runtime.Signature != signature)
        {
            RetireTileData(tile.Id);
            var runtime = new ScriptDataRuntime(_scriptPackages.DataBindingPath(tile.ScriptId), manifest, _tileDataTransport ??= new());
            runtime.Updated += () => Dispatcher.BeginInvoke(() =>
            {
                if (!_exit && IsVisible && _tileData.TryGetValue(tile.Id, out var latest) && latest.Runtime == runtime
                    && _scriptSessions.TryGetValue(tile.Id, out var session)) session.NextUpdate = DateTimeOffset.MinValue;
            });
            _tileData[tile.Id] = current = (tile.ScriptId, runtime);
        }
        return current.Runtime.Snapshot(now);
    }

    private void RetireTileData(string tileId)
    {
        if (_tileData.Remove(tileId, out var current)) current.Runtime.Dispose();
    }
}
