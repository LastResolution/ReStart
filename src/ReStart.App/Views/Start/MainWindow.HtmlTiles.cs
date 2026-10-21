using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private readonly Dictionary<string, HtmlTileView> _htmlTiles = [];
    private readonly HashSet<string> _usedHtmlTiles = [];
    private readonly HashSet<string> _folderThumbnailIds = [];
    private bool _documentationHtmlRendering;

    private async void OnContentMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var horizontal = Keyboard.Modifiers == ModifierKeys.Shift;
        var button = Ancestor<Button>(e.OriginalSource as DependencyObject);
        if (!_layoutEditing && !_dragging && button?.Tag is Tile tile && _htmlTiles.TryGetValue(tile.Id, out var view) && view.BrowserReady)
        {
            // Capture the source and consume the routed event before awaiting:
            // neither a later hover target nor WPF's parent scrolling may take it.
            e.Handled = true;
            await RouteWebTileScrollAsync(view, e.GetPosition(button), e.Delta, horizontal);
        }
        else if (horizontal && ContentScroll.ScrollableWidth > 0)
        { ContentScroll.ScrollToHorizontalOffset(ContentScroll.HorizontalOffset - e.Delta); e.Handled = true; }
    }

    private bool HandleWebTileScrollKey(KeyEventArgs e)
    {
        if (_layoutEditing || Keyboard.Modifiers != ModifierKeys.None || e.Key is not (Key.PageUp or Key.PageDown)) return false;
        var button = Ancestor<Button>(e.OriginalSource as DependencyObject);
        if (button?.Tag is not Tile tile || !_htmlTiles.TryGetValue(tile.Id, out var view) || !view.BrowserReady) return false;
        e.Handled = true;
        _ = RouteWebTileScrollAsync(view, new(button.ActualWidth / 2, button.ActualHeight / 2), e.Key == Key.PageUp ? 120 : -120, page: true);
        return true;
    }

    private async Task RouteWebTileScrollAsync(HtmlTileView view, Point point, int delta, bool horizontal = false, bool page = false)
    {
        if (await view.ScrollAsync(point, delta, horizontal, page)) return;
        if (_dismissing || _exit || _dragging || _layoutEditing || (!_preview && !IsVisible) || !_htmlTiles.Values.Contains(view)) return;
        ScrollBoard(delta, horizontal, page);
    }

    private void ScrollBoard(int delta, bool horizontal, bool page)
    {
        if (horizontal) ContentScroll.ScrollToHorizontalOffset(ContentScroll.HorizontalOffset - delta);
        else
        {
            var distance = page || SystemParameters.WheelScrollLines < 0 ? ContentScroll.ViewportHeight : SystemParameters.WheelScrollLines * 16;
            ContentScroll.ScrollToVerticalOffset(ContentScroll.VerticalOffset - delta / 120d * distance);
        }
    }

    private HtmlTileView HtmlView(Tile tile, bool thumbnail = false)
    {
        _usedHtmlTiles.Add(tile.Id);
        var offscreen = thumbnail || _preview && _documentationHtmlRendering;
        if (_htmlTiles.TryGetValue(tile.Id, out var previous) && previous.OffscreenOnly != offscreen)
        { _htmlTiles.Remove(tile.Id); previous.Dispose(); _scriptSessions.Remove(tile.Id); }
        if (!_htmlTiles.TryGetValue(tile.Id, out var view))
        {
            _htmlTiles[tile.Id] = view = new(_scriptPackages.BrowserDataRoot, tile.Id, offscreen);
            if (_scriptSessions.TryGetValue(tile.Id, out var session)) session.NextUpdate = DateTimeOffset.MinValue;
        }
        if (view.Parent is Panel panel) panel.Children.Remove(view);
        return view;
    }

    private void RetireHtmlTiles(bool all = false)
    {
        foreach (var id in _htmlTiles.Keys.Where(id => all || !_usedHtmlTiles.Contains(id)).ToArray())
        { _htmlTiles.Remove(id, out var view); view!.Dispose(); }
    }

    private async Task<(ScriptTileFrame? Frame, string? Error)> RenderHtmlTileAsync(Tile tile, object context, string source, ScriptTileManifest manifest)
    {
        if (!_htmlTiles.TryGetValue(tile.Id, out var view)) return (null, null);
        var files = _scriptPackages.ReadWeb(tile.ScriptId);
        view.SetImage(_slideshowImages.GetValueOrDefault(tile.Id));
        view.SetAssets(_scriptPackages.WebAssets(tile.ScriptId));
        var result = await view.RenderAsync(files.Html, files.Css, source, tile.AllowWebNetwork && manifest.Capabilities.Contains("web.network"), context);
        if (result.Frame != null && _folderThumbnailIds.Contains(tile.Id)) await view.FreezeForDocumentationAsync();
        return result;
    }

    private void AddWebTileMenu(ContextMenu menu, Tile tile)
    {
        if (!_scriptPackages.IsWeb(tile.ScriptId)) return;
        var network = new MenuItem
        {
            Header = L.T(K.AllowWebTileNetwork),
            IsCheckable = true,
            IsChecked = tile.AllowWebNetwork,
            IsEnabled = _scriptPackages.Read(tile.ScriptId).Manifest.Capabilities.Contains("web.network")
        };
        network.Click += (_, _) =>
        {
            tile.AllowWebNetwork = network.IsChecked;
            if (_htmlTiles.Remove(tile.Id, out var view)) view.Dispose();
            _scriptSessions.Remove(tile.Id); CommitLayout();
        };
        menu.Items.Add(network);
    }
}
