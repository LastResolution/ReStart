using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ReStart.App.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private readonly List<(Tile Tile, LegacyTileView View, FrameworkElement Icon, Button Button)> _legacyViews = [];
    private LegacyNotificationStore.Snapshot _legacySnapshot = new(true, new Dictionary<string, LegacyNotificationStore.Frame>());
    private IReadOnlyDictionary<(string App, string Source), BitmapSource> _legacyImages = new Dictionary<(string, string), BitmapSource>();
    private LegacyTileAssets _legacyAssets = new();
    private CancellationTokenSource? _legacyReadCancellation;
    private long _legacyNextPoll;
    private string _legacyRequested = "";
    private Func<IReadOnlyCollection<string>, CancellationToken, Task<LegacyNotificationStore.Snapshot>>? _legacyTileFixture;

    private static bool LegacyEligible(Tile tile) => !tile.IsFolder && tile.Widget.Length == 0 && tile.IsShellApp && LegacyNotificationStore.IsPackageAppId(tile.AppId);

    private void RefreshLegacyTiles()
    {
        if (_exit || !Document.Preferences.LegacyUwpLiveTiles) { StopLegacyTiles(); ApplyLegacyFrames(); return; }
        if (!IsVisible || !GroupsPanel.IsVisible || _dismissing) return;
        ApplyLegacyFrames();
        if (_preview && _legacyTileFixture == null || _legacyReadCancellation != null) return;
        var apps = _legacyViews.Where(v => v.Tile.Live && v.Button.IsVisible && TileInViewport(v.Button)).Select(v => v.Tile.AppId).Distinct(StringComparer.OrdinalIgnoreCase).Take(32).ToArray();
        if (apps.Length == 0) return;
        var requested = string.Join('|', apps);
        if (Environment.TickCount64 < _legacyNextPoll && requested == _legacyRequested) return;
        _legacyRequested = requested;
        _legacyNextPoll = Environment.TickCount64 + 3000;
        var cancellation = new CancellationTokenSource(); _legacyReadCancellation = cancellation;
        _ = ReadLegacyTilesAsync(apps, cancellation);
    }

    private async Task ReadLegacyTilesAsync(string[] apps, CancellationTokenSource cancellation)
    {
        var assets = _legacyAssets;
        try
        {
            var snapshot = await (_legacyTileFixture?.Invoke(apps, cancellation.Token) ?? LegacyNotificationStore.ReadAsync(apps, cancellation.Token));
            cancellation.Token.ThrowIfCancellationRequested();
            var images = _preview ? new Dictionary<(string, string), BitmapSource>()
                : await Task.Run(() => assets.Read(snapshot, cancellation.Token), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (_exit || !Document.Preferences.LegacyUwpLiveTiles) return;
            _legacySnapshot = snapshot; _legacyImages = images; ApplyLegacyFrames();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        { if (!_exit) { _legacySnapshot = new(false, new Dictionary<string, LegacyNotificationStore.Frame>()); ApplyLegacyFrames(); } }
        finally
        {
            if (_legacyReadCancellation == cancellation) _legacyReadCancellation = null;
            cancellation.Dispose();
        }
    }

    private void ApplyLegacyFrames()
    {
        foreach (var (tile, view, icon, button) in _legacyViews)
        {
            if (!TileInViewport(button)) continue;
            var frame = Document.Preferences.LegacyUwpLiveTiles && tile.Live ? _legacySnapshot.Apps.GetValueOrDefault(tile.AppId) : null;
            view.Apply(frame?.Bindings.FirstOrDefault(b => b.Size == tile.Size), frame?.Badge ?? "", tile.Size, DisplayPreferences.TileScale, DisplayPreferences.ShowLiveTileNames,
                button.Foreground, source => _legacyImages.GetValueOrDefault((tile.AppId, source)), _legacyImages);
            icon.Visibility = view.HasTileContent ? Visibility.Collapsed : Visibility.Visible;
            if (button.Content is Grid grid && grid.Children.OfType<TextBlock>().FirstOrDefault(label => ReferenceEquals(label.Tag, tile)) is { } name)
            {
                name.Margin = new(2, 0, frame?.Badge.Length > 0 ? (frame.Badge.Length * 7 + 6) * DisplayPreferences.TileScale : 1, 0);
                name.Foreground = view.ContentForeground ?? button.Foreground;
            }
        }
    }

    private void StopLegacyTiles()
    {
        if (_legacyReadCancellation == null && _legacySnapshot.Apps.Count == 0 && _legacyImages.Count == 0) { _legacyNextPoll = 0; return; }
        _legacyReadCancellation?.Cancel(); _legacyReadCancellation = null; _legacyNextPoll = 0; _legacyRequested = "";
        _legacySnapshot = new(true, new Dictionary<string, LegacyNotificationStore.Frame>());
        _legacyImages = new Dictionary<(string, string), BitmapSource>(); _legacyAssets = new();
    }

    private void PauseLegacyTiles()
    {
        _legacyReadCancellation?.Cancel(); _legacyNextPoll = 0; _legacyRequested = "";
    }
}
