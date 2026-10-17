using System.Windows;
using System.Windows.Media;
using ReStart.Core;

namespace ReStart.App.Controls;

/// <summary>Draws bounded native chart primitives supplied as JSON data; never interprets markup or script drawing commands.</summary>
internal sealed class ScriptTileArt : FrameworkElement
{
    internal ScriptTileFrame? Frame { get; set; }
    internal Brush Ink { get; set; } = Brushes.White;
    internal bool FillRegion { get; set; }

    protected override void OnRender(DrawingContext dc)
    {
        if (Frame is not { } frame || ActualWidth < 20 || ActualHeight < 20 || SystemParameters.HighContrast) return;
        var ink = Ink.Clone(); ink.Opacity = 0.35;
        var faint = Ink.Clone(); faint.Opacity = 0.12;
        var w = ActualWidth; var h = ActualHeight;
        if (frame.Visual == "ring")
        {
            var radius = FillRegion ? Math.Max(1, Math.Min(w, h) / 2 - 5) : Math.Min(w * 0.23, h * 0.36);
            var center = FillRegion ? new Point(w / 2, h / 2) : new Point(w - radius - 5, radius + 5);
            dc.DrawEllipse(null, new Pen(faint, 5), center, radius, radius);
            var progress = Math.Clamp(frame.Progress ?? 0, 0, 0.999999);
            if (progress <= 0) return;
            var angle = progress * 2 * Math.PI;
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                path.BeginFigure(new(center.X, center.Y - radius), false, false);
                path.ArcTo(new(center.X + Math.Sin(angle) * radius, center.Y - Math.Cos(angle) * radius), new(radius, radius), 0,
                    progress > 0.5, SweepDirection.Clockwise, true, false);
            }
            geometry.Freeze(); dc.DrawGeometry(null, new Pen(ink, 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, geometry);
        }
        else if (frame.Visual == "sparkline" && frame.Series.Length > 1)
        {
            var shape = new StreamGeometry();
            using (var path = shape.Open())
            {
                var range = FillRegion ? .75 : .4;
                path.BeginFigure(new(0, h * (0.88 - frame.Series[0] * range)), false, false);
                for (var i = 1; i < frame.Series.Length; i++) path.LineTo(new(w * i / (frame.Series.Length - 1), h * (0.88 - frame.Series[i] * range)), true, false);
            }
            shape.Freeze(); dc.DrawGeometry(null, new Pen(ink, 2.5) { LineJoin = PenLineJoin.Round }, shape);
        }
        else if (frame.Visual == "bars" && frame.Series.Length > 0)
        {
            var step = w / frame.Series.Length;
            for (var i = 0; i < frame.Series.Length; i++)
            {
                var height = Math.Max(2, h * (FillRegion ? .85 : .45) * frame.Series[i]);
                dc.DrawRoundedRectangle(ink, null, new(i * step + 1, h - height - 4, Math.Max(1, step - 3), height), 1.5, 1.5);
            }
        }
        else if (frame.Visual == "dots")
        {
            var step = Math.Min(w / 8, h / 6);
            var left = FillRegion ? (w - step * 7) / 2 : w - step * 7;
            var top = FillRegion ? (h - step * 5) / 2 : 0;
            for (var i = 0; i < frame.Series.Length; i++)
                dc.DrawEllipse(frame.Series[i] > 0 ? ink : faint, null, new(left + step * (.5 + i % 7), top + step * (.5 + i / 7)), step * 0.22, step * 0.22);
        }
    }
}
