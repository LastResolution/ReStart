using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private const double DragGhostOpacity = .68;
    private Canvas? _dragGhostLayer;
    private Image? _dragGhost;
    private Point _dragGhostAnchor;
    private bool _dragFrameSubscribed;
    private TimeSpan _dragFrameTime;
    private long _dragFrameTicks;
    private readonly List<(FrameworkElement View, double Opacity)> _dragGhostSources = [];

    private void StartDragGhost(Button button, string format, string id, Point grab)
    {
        StopDragGhost();
        FrameworkElement[] sources = format == "ReStart.Group"
            ? GroupsPanel.Children.OfType<StackPanel>().Where(view => view.Tag is TileGroup group && group.Id == id).ToArray()
            : format == "ReStart.Selection"
                ? _selectionViews.Where(pair => _selectedTileIds.Contains(pair.Key)).Select(pair => (FrameworkElement)pair.Value.Button).ToArray()
                : [button];
        sources = sources.Where(view => view.ActualWidth > 0 && view.ActualHeight > 0).ToArray();
        if (sources.Length == 0) return;
        var bounds = sources.Select(view => new Rect(view.TranslatePoint(new(), Root), view.RenderSize)).Aggregate(Rect.Union);
        // Large selections retain their relative positions, but their raster and
        // on-screen footprint stay bounded even on a very large scrolling board.
        var scale = Math.Min(1, Math.Min(Math.Min(640, Root.ActualWidth * .8) / bounds.Width, Math.Min(500, Root.ActualHeight * .8) / bounds.Height));
        var width = Math.Max(1, bounds.Width * scale); var height = Math.Max(1, bounds.Height * scale);
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scale, scale));
            foreach (var source in sources)
            {
                var point = source.TranslatePoint(new(), Root);
                dc.DrawRectangle(new VisualBrush(source) { Stretch = Stretch.Fill }, null,
                    new Rect(point.X - bounds.X, point.Y - bounds.Y, source.ActualWidth, source.ActualHeight));
            }
        }
        var dpi = VisualTreeHelper.GetDpi(Root).PixelsPerDip;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * dpi), (int)Math.Ceiling(height * dpi), 96 * dpi, 96 * dpi, PixelFormats.Pbgra32);
        bitmap.Render(drawing); bitmap.Freeze();
        _dragGhostAnchor = new((grab.X - bounds.X) * scale, (grab.Y - bounds.Y) * scale);
        _dragGhost = new() { Source = bitmap, Width = width, Height = height, Opacity = DragGhostOpacity, IsHitTestVisible = false, RenderTransform = new TranslateTransform() };
        RenderOptions.SetBitmapScalingMode(_dragGhost, BitmapScalingMode.HighQuality);
        _dragGhostLayer = new() { IsHitTestVisible = false, Focusable = false, ClipToBounds = true };
        Panel.SetZIndex(_dragGhostLayer, int.MaxValue);
        _dragGhostLayer.Children.Add(_dragGhost); Root.Children.Add(_dragGhostLayer);
        foreach (var source in sources) { _dragGhostSources.Add((source, source.Opacity)); source.Opacity *= .32; }
        MoveDragGhost(grab);
        // OLE can leave WPF's Mouse.GetPosition at the initial grab point. Read
        // the real cursor once per display frame without invalidating layout.
        _dragFrameTime = TimeSpan.MinValue; _dragFrameTicks = Stopwatch.GetTimestamp();
        _dragFrameSubscribed = true;
        CompositionTarget.Rendering += OnDragFrame;
    }

    private void OnDragFrame(object? sender, EventArgs e)
    {
        if (e is RenderingEventArgs frame)
        {
            if (frame.RenderingTime == _dragFrameTime) return;
            _dragFrameTime = frame.RenderingTime;
        }
        var now = Stopwatch.GetTimestamp();
        var seconds = Math.Clamp(Stopwatch.GetElapsedTime(_dragFrameTicks, now).TotalSeconds, 0, .05);
        _dragFrameTicks = now;
        TrackDragGhostCursor();
        var changed = false;
        if (_dragging && _activeDragFormat != "ReStart.Group")
        {
            var alt = Native.GetAsyncKeyState(0x12) < 0;
            var shift = Native.GetAsyncKeyState(0x10) < 0;
            changed = alt != _freePlacementGesture || shift != _reorderGesture;
            SetFreePlacementGesture(alt); SetReorderGesture(shift);
        }
        var scrolled = _dragScrolling && _dragging && IsVisible && ScrollDragAt(_dragViewportPoint, seconds);
        if (changed && !scrolled) RefreshDragProposal();
    }

    private void TrackDragGhostCursor()
    {
        if (_dragGhost == null || PresentationSource.FromVisual(Root) == null || !Native.GetCursorPos(out var cursor)) return;
        var point = Root.PointFromScreen(new(cursor.X, cursor.Y));
        MoveDragGhost(point);
        _dragViewportPoint = Root.TranslatePoint(point, ContentScroll);
    }

    private void MoveDragGhost(Point point)
    {
        if (_dragGhost == null) return;
        _dragGhost.Visibility = new Rect(Root.RenderSize).Contains(point) ? Visibility.Visible : Visibility.Hidden;
        var transform = (TranslateTransform)_dragGhost.RenderTransform;
        transform.X = point.X - _dragGhostAnchor.X;
        transform.Y = point.Y - _dragGhostAnchor.Y;
    }

    private void StopDragGhost()
    {
        StopDragFrames();
        if (_dragGhostLayer != null) Root.Children.Remove(_dragGhostLayer);
        foreach (var (view, opacity) in _dragGhostSources) view.Opacity = opacity;
        _dragGhostSources.Clear(); _dragGhost = null; _dragGhostLayer = null;
    }

    private void StopDragFrames()
    {
        if (_dragFrameSubscribed) CompositionTarget.Rendering -= OnDragFrame;
        _dragFrameSubscribed = false; _dragScrolling = false;
    }

    private void RefreshDragProposal()
    {
        if (_activeDragId == null) return;
        var board = ContentScroll.TranslatePoint(_dragViewportPoint, GroupsPanel);
        if (_activeDragFormat == "ReStart.Selection" && PlanSelectionDrop(_activeDragId, board) is { } selection)
        { ShowSelectionPreview(selection, board, _activeDragId); ShowPendingFolderPreview(); }
        else if ((_activeDragFormat == "ReStart.App" ? TileForAppDrag(_activeDragId) : FindTile(_activeDragId)) is { } tile)
            PreviewSingleTileDrop(tile, board, _activeDragId);
    }
}
