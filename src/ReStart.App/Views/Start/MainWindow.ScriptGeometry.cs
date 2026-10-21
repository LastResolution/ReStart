using System.Windows;
using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Checks that native tile dimensions and local content bounds depend on size/scale, not board position or ownership.</summary>
    private int VerifyScriptGeometry(Tile tile, string directory)
    {
        var measurements = new List<object>();
        Root.Width = 1920; Root.Height = 1080;
        foreach (var scale in new[] { .7, 1, 1.5 })
            foreach (var size in Enum.GetValues<TileSize>())
            {
                Rect? baseline = null; Size? plotBaseline = null;
                foreach (var (standalone, x, row) in new[] { (true, 0d, 0), (true, 5d, 24), (false, 10d, 0), (false, 15d, 24) })
                {
                    tile.Size = size; tile.Column = 0; tile.Row = 0;
                    var frame = new ScriptTileFrame
                    {
                        Text = "42 KiB/s",
                        Detail = "↑ 44 KiB/s",
                        Kicker = "Network flow",
                        Visual = "sparkline",
                        Series = [.2, .4, .1, .3],
                        Layout = size is TileSize.Small or TileSize.Medium ? "centered" : size == TileSize.Large ? "stacked" : "split",
                        Background = "#10464B",
                        Foreground = "#FFFFFF"
                    };
                    Document = new()
                    {
                        Preferences = new() { TileScale = scale, Language = "en", ShowWallpaper = false, Animate = false },
                        Groups = [new() { Name = "Network geometry", IsStandalone = standalone, Column = 0, X = x, Row = row, Tiles = [tile] }]
                    };
                    _scriptFrames[tile.Id] = frame;
                    ApplyAppearance(); RenderGroups(); UpdateLayout();
                    var view = _scriptViews.Single();
                    var plot = (Grid)view.Body.Parent;
                    var local = ((FrameworkElement)view.Button.Content).TransformToAncestor(view.Button)
                        .TransformBounds(new Rect(((FrameworkElement)view.Button.Content).RenderSize));
                    var (width, height) = TileLayout.Dimensions(size);
                    Require(Math.Abs(view.Button.ActualWidth - (width * Cell - 4)) <= 1 && Math.Abs(view.Button.ActualHeight - (height * Cell - 4)) <= 1,
                        "A script tile exceeds its logical footprint at a different board position.");
                    var global = view.Button.TranslatePoint(new Point(), GroupsPanel);
                    Require(Math.Abs(global.X - x * Cell) <= 1, "A tile's drawn left edge differs from its saved horizontal anchor.");
                    if (baseline is Rect first)
                        Require(Math.Abs(first.X - local.X) <= 1 && Math.Abs(first.Y - local.Y) <= 1 &&
                            Math.Abs(first.Width - local.Width) <= 1 && Math.Abs(first.Height - local.Height) <= 1 &&
                            Math.Abs(plotBaseline!.Value.Width - plot.ActualWidth) <= 1 && Math.Abs(plotBaseline.Value.Height - plot.ActualHeight) <= 1,
                            "Moving a script tile changed its local margins or plot dimensions.");
                    baseline = local; plotBaseline = plot.RenderSize;
                    measurements.Add(new { scale, size = size.ToString(), standalone, x, row, view.Button.ActualWidth, view.Button.ActualHeight, content = local.ToString(), plotWidth = plot.ActualWidth, plotHeight = plot.ActualHeight });
                }
            }
        var previousOffset = _dragOffset;
        try
        {
            _dragOffset = new();
            tile.Size = TileSize.Large;
            var laneLeft = 2 * (6 * 52 + GroupLayout.Gap);
            var above = new TileGroup { Column = 2, Row = 0, X = laneLeft / 52, IsStandalone = true, Tiles = [new() { Size = TileSize.Large }] };
            var neighbor = new TileGroup { Column = 2, Row = 8, X = 17, IsStandalone = true, Tiles = [new() { Size = TileSize.Wide }] };
            var moving = new TileGroup { Column = 2, Row = 8, X = 13, IsStandalone = true, Tiles = [tile] };
            Document = new() { Preferences = new() { TileScale = 1, Columns = 6, ShowWallpaper = false, Animate = false }, Groups = [above, neighbor, moving] };
            RenderGroups(); UpdateLayout();
            var pointer = new Point(676, GroupLayout.HeaderHeight + 8 * 26);
            PreviewStandalone(tile, pointer);
            Require(_dropAdorner?.CellBounds.X == laneLeft, "The independent drop preview retains the four-pixel lane offset.");
            MoveTileToStandalone(tile, pointer); UpdateLayout();
            var actual = _scriptViews.Single().Button.TranslatePoint(new Point(), GroupsPanel).X;
            Require(Math.Abs(actual - laneLeft) <= 1, "The committed tile does not align with the column above it.");
            measurements.Add(new { regression = "lane-origin-preview-and-drop", expected = laneLeft, actual });
        }
        finally { _dragOffset = previousOffset; ClearDropPreview(); }
        File.WriteAllText(Path.Combine(directory, "script-geometry.json"), System.Text.Json.JsonSerializer.Serialize(measurements));
        return measurements.Count;
    }
}
