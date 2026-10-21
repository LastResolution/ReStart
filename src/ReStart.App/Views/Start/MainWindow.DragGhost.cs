using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private const double DragGhostOpacity = .68;
    private Canvas? _dragGhostLayer;
    private Image? _dragGhost;
    private Point _dragGhostAnchor;
    private DispatcherTimer? _dragGhostTimer;
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
        _dragGhost = new() { Source = bitmap, Width = width, Height = height, Opacity = DragGhostOpacity, IsHitTestVisible = false };
        RenderOptions.SetBitmapScalingMode(_dragGhost, BitmapScalingMode.HighQuality);
        _dragGhostLayer = new() { IsHitTestVisible = false, Focusable = false, ClipToBounds = true };
        Panel.SetZIndex(_dragGhostLayer, int.MaxValue);
        _dragGhostLayer.Children.Add(_dragGhost); Root.Children.Add(_dragGhostLayer);
        foreach (var source in sources) { _dragGhostSources.Add((source, source.Opacity)); source.Opacity *= .32; }
        MoveDragGhost(grab);
        GiveFeedback += OnDragGhostFeedback;
        // OLE can leave WPF's Mouse.GetPosition at the initial grab point. Read
        // the real screen cursor and convert physical pixels through WPF's DPI.
        _dragGhostTimer = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        _dragGhostTimer.Tick += (_, _) => TrackDragGhostCursor();
        _dragGhostTimer.Start();
    }

    private void OnDragGhostFeedback(object sender, GiveFeedbackEventArgs e) => TrackDragGhostCursor();

    private void TrackDragGhostCursor()
    {
        if (_dragGhost == null || PresentationSource.FromVisual(Root) == null || !Native.GetCursorPos(out var cursor)) return;
        MoveDragGhost(Root.PointFromScreen(new(cursor.X, cursor.Y)));
    }

    private void MoveDragGhost(Point point)
    {
        if (_dragGhost == null) return;
        _dragGhost.Visibility = new Rect(Root.RenderSize).Contains(point) ? Visibility.Visible : Visibility.Hidden;
        Canvas.SetLeft(_dragGhost, point.X - _dragGhostAnchor.X);
        Canvas.SetTop(_dragGhost, point.Y - _dragGhostAnchor.Y);
    }

    private void StopDragGhost()
    {
        _dragGhostTimer?.Stop(); _dragGhostTimer = null;
        GiveFeedback -= OnDragGhostFeedback;
        if (_dragGhostLayer != null) Root.Children.Remove(_dragGhostLayer);
        foreach (var (view, opacity) in _dragGhostSources) view.Opacity = opacity;
        _dragGhostSources.Clear(); _dragGhost = null; _dragGhostLayer = null;
    }
}
