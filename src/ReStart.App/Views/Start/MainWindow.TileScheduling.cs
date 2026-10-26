using System.Windows;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private bool _verifyViewportScheduling = false;
    private int _tileResumeGeneration;
    private long _metricsResumeDeadline;

    private void QueueTileResume()
    {
        var generation = ++_tileResumeGeneration;
        _metricsResumeDeadline = Environment.TickCount64 + 1500;
        // Present resets the scroll position and may close an inline folder.
        // Resolve that layout before selecting the browsers to resume, without
        // waiting for the next 250 ms background clock tick.
        Dispatcher.BeginInvoke(async () =>
        {
            if (_exit || !IsVisible || generation != _tileResumeGeneration) return;
            UpdateLiveTiles();
            await UpdateScriptTilesAsync();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private bool TileInViewport(FrameworkElement element)
    {
        if (!Document.Preferences.PauseOffscreenLiveTiles || _preview && !_verifyViewportScheduling) return true;
        if (ContentScroll.ViewportWidth < 1 || ContentScroll.ViewportHeight < 1 || element.ActualWidth < 1) return true;
        try
        {
            var bounds = element.TransformToAncestor(ContentScroll).TransformBounds(new Rect(element.RenderSize));
            var viewport = new Rect(0, 0, ContentScroll.ViewportWidth, ContentScroll.ViewportHeight);
            viewport.Inflate(24, 24); // Warm the next tile before a small scroll exposes it.
            return bounds.IntersectsWith(viewport);
        }
        catch (InvalidOperationException) { return false; }
    }

    private void RecoverHtmlTiles()
    {
        if (_dragging || _editingGroupHeader?.IsEditing == true) return;
        var pending = _htmlTiles.Where(pair => pair.Value.NeedsRecreation && _liveSessions.Budget(pair.Key).CanRun(Environment.TickCount64)).ToArray();
        if (pending.Length == 0) return;
        foreach (var (id, view) in pending)
        {
            _htmlTiles.Remove(id); view.Dispose(); _scriptFrames.Remove(id);
            if (_scriptSessions.TryGetValue(id, out var session))
                _scriptSessions[id] = new LiveTileSessions.Runtime { State = session.State, Inputs = new(session.Inputs), Attempts = session.Attempts };
        }
        RenderGroups();
    }
}
