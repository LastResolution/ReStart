using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private readonly List<(Tile Tile, StackPanel Body, Button Button)> _scriptViews = [];
    private readonly Dictionary<string, ScriptTileSession> _scriptSessions = [];
    private readonly Dictionary<string, ScriptTileFrame> _scriptFrames = [];
    private ScriptTilePackages _scriptPackages = null!;
    private bool _scriptRendering;
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

    /// <summary>Copies a selected manifest and tile.js into an installed package and pins the new tile.</summary>
    public void ImportScriptTile()
    {
        WithDialog(() =>
        {
            var dialog = new OpenFileDialog { Title = L.T(K.PromptScriptManifest), Filter = "manifest.json|manifest.json" };
            if (dialog.ShowDialog(this) != true) return;
            try { PinCustom(_scriptPackages.Import(dialog.FileName)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or System.Text.Json.JsonException) { SetStatus(ex.Message); }
        });
    }

    private void AddScriptMenu(ContextMenu menu, Tile tile)
    {
        Add(menu, L.T(K.EditScript), () => EditScript(tile));
        Add(menu, L.T(K.EditScriptSettings), () => EditScript(tile, "manifest.json"));
        Add(menu, L.T(K.OpenScriptFolder), () =>
        {
            try { ShellLauncher.Launch(_scriptPackages.Folder(tile.ScriptId)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception) { SetStatus(ex.Message); }
        });
        Add(menu, L.T(K.ReloadScript), () =>
        {
            if (_scriptSessions.Remove(tile.Id, out var session)) session.Dispose();
            CommitLayout();
        });
    }
    private void EditScript(Tile tile, string file = "tile.js")
    {
        try { _scriptPackages.Edit(tile.ScriptId, file); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception) { SetStatus(ex.Message); }
    }

    private async Task UpdateScriptTilesAsync()
    {
        if (_scriptRendering || !IsVisible) return;
        _scriptRendering = true;
        try
        {
            var current = _scriptViews.Where(v => v.Body.IsVisible).ToArray();
            await Task.WhenAll(current.Take(8).Select(async view =>
            {
                if (!_scriptSessions.TryGetValue(view.Tile.Id, out var session)) _scriptSessions[view.Tile.Id] = session = new();
                if (session.NextUpdate > DateTimeOffset.Now) return;
                session.NextUpdate = DateTimeOffset.Now.AddSeconds(5);
                try
                {
                    var (manifest, source) = _scriptPackages.Read(view.Tile.ScriptId);
                    session.NextUpdate = DateTimeOffset.Now.AddSeconds(manifest.RefreshSeconds);
                    var frame = await session.RenderAsync(source, new { now = DateTimeOffset.Now.ToString("O"), locale = L.Culture.Name, size = view.Tile.Size.ToString().ToLowerInvariant(), settings = manifest.Settings, state = session.State });
                    if (frame != null) { _scriptFrames[view.Tile.Id] = frame; ApplyScriptFrame(view.Body, view.Button, view.Tile, frame); }
                    view.Button.ToolTip = session.Error == null ? view.Tile.Label : L.T(K.ErrorScriptPrefix) + session.Error;
                    if (frame == null && view.Body.Children.Count == 0) view.Body.Children.Add(new TextBlock { Text = L.T(K.ErrorScript), TextWrapping = TextWrapping.Wrap });
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or System.Text.Json.JsonException)
                { view.Button.ToolTip = L.T(K.ErrorScriptPrefix) + ex.Message; if (view.Body.Children.Count == 0) view.Body.Children.Add(new TextBlock { Text = L.T(K.ErrorScript) }); }
            }));
            foreach (var view in current.Skip(8)) view.Button.ToolTip = L.T(K.MaxScriptTiles);
        }
        finally { _scriptRendering = false; }
    }
    private void ApplyScriptFrame(StackPanel body, Button button, Tile tile, ScriptTileFrame frame)
    {
        body.Children.Clear();
        if (!SystemParameters.HighContrast && !tile.UseCustomColor)
        {
            button.Background = frame.Background.Length == 0 ? (System.Windows.Media.Brush)FindResource("AccentBrush") : Brush(frame.Background, Document.Preferences.Accent);
            button.Foreground = frame.Foreground.Length == 0 ? Brushes.White : Brush(frame.Foreground, "#FFFFFF");
        }
        body.Children.Add(new TextBlock { Text = frame.Text, FontSize = tile.Size == TileSize.Small ? 14 : 23, FontWeight = FontWeights.Light, TextWrapping = TextWrapping.Wrap, MaxHeight = tile.Size == TileSize.Large ? 120 : tile.Size == TileSize.Small ? 30 : 32, TextTrimming = TextTrimming.CharacterEllipsis });
        if (tile.Size != TileSize.Small)
        {
            body.Children.Add(new TextBlock { Text = frame.Detail, FontSize = 12, Opacity = 0.85, TextWrapping = TextWrapping.Wrap, MaxHeight = tile.Size == TileSize.Large ? 60 : 18, TextTrimming = TextTrimming.CharacterEllipsis });
            if (frame.Badge.Length > 0) body.Children.Add(new TextBlock { Text = frame.Badge, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right });
            if (frame.Progress is double progress)
            {
                var track = new Grid { Height = 3, Margin = new(0, 4, 0, 0), Background = new SolidColorBrush(Color.FromArgb(50, 128, 128, 128)) };
                track.ColumnDefinitions.Add(new() { Width = new GridLength(progress, GridUnitType.Star) });
                track.ColumnDefinitions.Add(new() { Width = new GridLength(1 - progress, GridUnitType.Star) });
                var fill = new Border { Background = button.Foreground }; track.Children.Add(fill); body.Children.Add(track);
            }
        }
    }
    private void PauseScripts() { foreach (var session in _scriptSessions.Values) session.Dispose(); }
}
