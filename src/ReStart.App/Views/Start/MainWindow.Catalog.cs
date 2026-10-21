using System.Windows.Threading;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private long _catalogRefreshedAt;
    private Task? _catalogRefresh;

    private void RequestCatalogRefresh(bool opening = false)
    {
        if (_preview || !_ready || !IconsReady || _exit || _catalogRefresh != null) return;
        if (Environment.TickCount64 - _catalogRefreshedAt < (opening ? 5000 : 30000)) return;
        _ = RefreshCatalogAsync();
    }

    private Task RefreshCatalogAsync() => _catalogRefresh ??= RefreshCatalogCoreAsync();

    private async Task RefreshCatalogCoreAsync()
    {
        await Dispatcher.Yield(DispatcherPriority.Background);
        try
        {
            var apps = await _loadCatalog();
            await WaitForCatalogViewsAsync();
            if (_exit) return;
            _apps = apps;
            RenderApps(); RefreshSearchResults();
            await _catalog.LoadIconsAsync(apps);
            await WaitForCatalogViewsAsync();
            if (_exit) return;
            var retained = apps.Select(a => a.Id).Concat(Document.Groups.SelectMany(g => g.Tiles)
                .SelectMany(t => new[] { t }.Concat(t.Children)).Select(t => t.AppId)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var id in _catalog.Icons.Keys.Where(id => !retained.Contains(id))) _catalog.Icons.TryRemove(id, out _);
            RenderApps(); RefreshSearchResults();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        { if (!_exit && IsVisible) SetStatus(L.T(K.ErrorCatalogLoad)); }
        finally { _catalogRefreshedAt = Environment.TickCount64; _catalogRefresh = null; }
    }

    private async Task WaitForCatalogViewsAsync()
    {
        while (!_exit && (_dragging || _editingGroupHeader?.IsEditing == true || _dialogDepth > 0)) await Task.Delay(100);
    }
}
