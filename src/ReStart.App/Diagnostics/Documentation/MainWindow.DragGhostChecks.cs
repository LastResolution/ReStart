using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private int VerifyDragGhost(string directory)
    {
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        var original = Document; Content = null; Root.Width = 1280; Root.Height = 800;
        using var host = HiddenRenderHost(Root, 1280, 800);
        try
        {
            foreach (var format in new[] { "ReStart.Tile", "ReStart.Group", "ReStart.Selection", "ReStart.App" })
                foreach (var size in Enum.GetValues<TileSize>())
                {
                    var first = new Tile { Label = "Moving tile", Size = size, Widget = "clock", Column = 0, Row = 0 };
                    var second = new Tile { Label = "Second tile", Size = TileSize.Medium, Column = 4, Row = 1 };
                    var group = new TileGroup { Name = "Moving group", Tiles = [first, second], Column = 0, Row = 0, X = 0 };
                    Document = new() { Preferences = new() { Columns = 8, Animate = false, ShowWallpaper = false }, Groups = [group] };
                    RenderGroups(); Root.Measure(new(1280, 800)); Root.Arrange(new Rect(0, 0, 1280, 800)); FitPanels(); UpdateLayout();
                    var button = _selectionViews[first.Id].Button;
                    button.Opacity = .9;
                    _selectedTileIds.Clear(); _selectedTileIds.UnionWith([first.Id, second.Id]);
                    var before = JsonSerializer.Serialize(Document.Groups, LayoutStore.JsonOptions);
                    var grab = button.TranslatePoint(new(12, 17), Root);
                    StartDragGhost(button, format, format == "ReStart.Group" ? group.Id : first.Id, grab);
                    Check(_dragGhost is { Source: BitmapSource }, "A drag did not produce an image: " + format);
                    Check(_dragGhost!.Opacity == DragGhostOpacity && _dragGhost.Opacity is > 0 and < 1, "The dragged image was not translucent.");
                    Check(_dragGhostLayer is { IsHitTestVisible: false, Focusable: false }, "A drag image intercepted input.");
                    Check(_dragGhostSources.Count == (format == "ReStart.Selection" ? 2 : 1), "A drag omitted its selected sources.");
                    Check(_dragGhost.Width <= 640 && _dragGhost.Height <= 500, "A drag image exceeded its bounded footprint.");
                    Check(_dragGhostSources.All(source => source.View.Opacity < source.Opacity), "The original drag source was not visually distinguished.");
                    var bitmap = (BitmapSource)_dragGhost.Source;
                    var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
                    Check(Enumerable.Range(0, pixels.Length / 4).Any(index => pixels[index * 4 + 3] > 0), "A drag snapshot was empty.");
                    var oldLeft = ((TranslateTransform)_dragGhost.RenderTransform).X; var oldTop = ((TranslateTransform)_dragGhost.RenderTransform).Y;
                    MoveDragGhost(new(grab.X + 173, grab.Y + 81));
                    Check(Math.Abs(((TranslateTransform)_dragGhost.RenderTransform).X - oldLeft - 173) < .01 && Math.Abs(((TranslateTransform)_dragGhost.RenderTransform).Y - oldTop - 81) < .01, "The drag image changed its grab offset.");
                    MoveDragGhost(new(-20, -20));
                    Check(_dragGhost.Visibility == Visibility.Hidden, "A drag image remained visible outside the launcher.");
                    MoveDragGhost(new(grab.X + 100, grab.Y + 60));
                    Check(_dragGhost.Visibility == Visibility.Visible, "A drag image did not return with the cursor.");
                    Root.UpdateLayout();
                    if (size == TileSize.Medium && format != "ReStart.App") SaveScreenshot(Path.Combine(directory, "drag-" + format[8..].ToLowerInvariant() + ".png"));
                    Check(before == JsonSerializer.Serialize(Document.Groups, LayoutStore.JsonOptions), "Drawing a drag ghost mutated the layout.");
                    StopDragGhost();
                    Check(_dragGhost == null && _dragGhostLayer == null && !_dragFrameSubscribed && _dragGhostSources.Count == 0, "A finished drag leaked its overlay or timer.");
                    Check(button.Opacity == .9, "Ending a drag did not restore the source opacity.");
                    StopDragGhost();
                    Check(!IsVisible, "Drag verification unexpectedly opened a native window.");
                }
        }
        finally { StopDragGhost(); _selectedTileIds.Clear(); host.RootVisual = null; Content = Root; Document = original; RenderGroups(); }
        return checks;
    }
}
