using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Exercises viewport changes and the actual scroll-bar templates in an isolated WPF fixture.</summary>
    internal async Task<ScrollSmokeResult> VerifyScrollingAsync(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("Scroll verification requires preview mode.");
        var original = Document;
        var originalWidth = Root.Width;
        var originalHeight = Root.Height;
        var checks = new List<ScrollMeasurement>();
        try
        {
            Root.Width = 900; Root.Height = 650;
            Document = new() { Preferences = new() { Animate = false, ShowWallpaper = false, Language = "en" } };
            ResetToPinnedView();
            foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
            {
                Document.Preferences.Theme = theme; ApplyAppearance();
                foreach (var mode in new[] { "fitting", "vertical", "both", "horizontal", "reset", "drag-vertical" })
                {
                    _dragging = false; ClearDropPreview(); GroupsPanel.DragMinimumHeight = 0; GroupsPanel.DragMinimumWidth = 0;
                    var tall = mode is "vertical" or "both" or "drag-vertical";
                    var wide = mode is "horizontal" or "both";
                    Document.Groups = [new() { Name = "Scroll sample", Column = 0, Row = 0,
                        Tiles = [new() { Label = "Sample A", Column = 0, Row = 0 },
                            new() { Label = "Sample B", Column = 0, Row = tall && mode != "drag-vertical" ? 24 : 2 }] }];
                    if (wide) Document.Groups.Add(new() { Name = "Off-screen sample", X = 20, Column = 3, Row = 0, Tiles = [new() { Label = "Sample C" }] });
                    RenderGroups(); UpdateLayout();
                    if (mode == "drag-vertical")
                    {
                        Require(ContentScroll.ScrollableHeight <= 1, "The drag fixture already overflows before moving.");
                        _dragging = true; _dragOffset = new(0, 0); PreserveDragExtent();
                        PreviewGroupOnBoard(Document.Groups[0], new(0, 700)); UpdateLayout();
                    }
                    await Dispatcher.InvokeAsync(() => UpdateLayout(), DispatcherPriority.ApplicationIdle);
                    var bars = VisualChildren<ScrollBar>(ContentScroll).ToArray();
                    var vertical = bars.Single(b => b.Orientation == Orientation.Vertical);
                    var horizontal = bars.Single(b => b.Orientation == Orientation.Horizontal);
                    var styled = bars.All(b => VisualChildren<Border>(b).Any(t => t.Name == "ThumbBody"));
                    checks.Add(new(theme.ToString(), mode, ContentScroll.ViewportWidth, ContentScroll.ExtentWidth,
                        ContentScroll.ScrollableWidth, ContentScroll.ScrollableHeight, vertical.ActualWidth, styled));
                    SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"scroll-{theme}-{mode}.png"));
                    Require((ContentScroll.ScrollableWidth > 1) == wide,
                        $"Unexpected horizontal overflow in {theme}/{mode}: extent={ContentScroll.ExtentWidth}, viewport={ContentScroll.ViewportWidth}, vertical width={vertical.ActualWidth}, themed={styled}.");
                    Require((ContentScroll.ScrollableHeight > 1) == tall, $"Unexpected vertical overflow in {theme}/{mode}.");
                    Require(styled, "A ScrollViewer scrollbar is using a native theme template.");
                    Require(!tall || Math.Abs(vertical.ActualWidth - 12) < 0.1, "The native scrollbar minimum width still overrides the themed bar.");
                    foreach (var bar in bars)
                    {
                        var thumb = VisualChildren<Border>(bar).Single(t => t.Name == "ThumbBody");
                        Require(thumb.Background == FindResource("ScrollThumbBrush"), "A scrollbar thumb does not follow the app theme.");
                        var inset = bar.Orientation == Orientation.Vertical ? thumb.Margin.Left + thumb.Margin.Right : thumb.Margin.Top + thumb.Margin.Bottom;
                        Require(Math.Abs(inset - 6) < .1, "Horizontal and vertical scrollbar thumbs have different visual thickness.");
                    }
                    if (wide) { ContentScroll.ScrollToRightEnd(); UpdateLayout(); Require(ContentScroll.HorizontalOffset > 1, "Horizontal scrolling does not move the board."); }
                    if (tall) { ContentScroll.ScrollToEnd(); UpdateLayout(); Require(ContentScroll.VerticalOffset > 1, "Vertical scrolling does not move the board."); }
                }
            }
            _dragging = false; ClearDropPreview(); GroupsPanel.DragMinimumHeight = 0; GroupsPanel.DragMinimumWidth = 0;
            Document.Groups = [new() { Name = "Top", Column = 0, Row = 0, Tiles = [new()] },
                new() { Name = "Bottom", Column = 0, Row = 80, Tiles = [new()] }];
            RenderGroups(); UpdateLayout(); ContentScroll.ScrollToEnd(); UpdateLayout();
            var bottom = Document.Groups[1]; var offset = ContentScroll.VerticalOffset;
            Require(offset > 1000, "The drag fixture did not start at the bottom of a tall board.");
            _dragging = true; _dragOffset = new(10, 10); PreserveDragExtent();
            double? previousTop = null;
            for (var attempt = 0; attempt < 32; attempt++)
            {
                var pointer = ContentScroll.TranslatePoint(new Point(40, 180), GroupsPanel);
                PreviewGroupOnBoard(bottom, pointer); UpdateLayout();
                Require(Math.Abs(ContentScroll.VerticalOffset - offset) < .1, "Moving the bottom group coerced the scroll offset and fed back into dragging.");
                var top = GroupsPanel.DragPlacements!.Single(p => p.Group == bottom).Top;
                Require(previousTop == null || Math.Abs(top - previousTop.Value) < .1, "A stationary pointer produced alternating group positions.");
                previousTop = top;
                var preview = GroupsPanel.DragPlacements;
                PreviewGroupOnBoard(bottom, ContentScroll.TranslatePoint(new Point(40, 180), GroupsPanel));
                Require(ReferenceEquals(preview, GroupsPanel.DragPlacements) && GroupsPanel.IsMeasureValid,
                    "An unchanged drop anchor forced another board layout.");
            }
            SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "scrolled-group-drag.png"));
            Require(bottom.Row == 80 && bottom.Tiles[0].Row == null, "A drag preview mutated the saved layout.");
            return new(checks.ToArray(), 32);
        }
        finally
        {
            _dragging = false; ClearDropPreview(); GroupsPanel.DragMinimumHeight = 0; GroupsPanel.DragMinimumWidth = 0;
            Document = original; Root.Width = originalWidth; Root.Height = originalHeight;
            ApplyAppearance(); RenderGroups(); ResetToPinnedView(); UpdateLayout();
        }
    }
}

internal sealed record ScrollMeasurement(string Theme, string Mode, double ViewportWidth, double ExtentWidth, double HorizontalOverflow, double VerticalOverflow, double VerticalBarWidth, bool Themed);
internal sealed record ScrollSmokeResult(ScrollMeasurement[] Cases, int DragStabilityChecks);
