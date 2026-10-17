using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ReStart.App.Controls;

internal sealed class DropAdorner(UIElement target) : Adorner(target)
{
    public Rect CellBounds { get; set; }
    public bool FolderTarget { get; set; }
    public Rect? GroupBounds { get; set; }
    public string Caption { get; set; } = "";
    protected override void OnRender(DrawingContext dc)
    {
        var accent = (Brush)Application.Current.FindResource("AccentBrush");
        var fill = accent.Clone(); fill.Opacity = 0.2;
        var pen = new Pen(accent, 3) { DashStyle = FolderTarget ? DashStyles.Solid : DashStyles.Dash };
        dc.DrawRectangle(fill, pen, CellBounds);
        if (GroupBounds is Rect group)
            dc.DrawRectangle(null, new Pen(accent, 2), group);
        if (FolderTarget) dc.DrawRoundedRectangle(null, pen, new Rect(CellBounds.X + 8, CellBounds.Y + 8, Math.Max(1, CellBounds.Width - 16), Math.Max(1, CellBounds.Height - 16)), 4, 4);
        if (Caption.Length > 0)
        {
            var text = new FormattedText(Caption, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12,
                (Brush)Application.Current.FindResource("TextBrush"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            var origin = new Point(CellBounds.Left + 6, Math.Max(0, CellBounds.Top - text.Height - 10));
            dc.DrawRectangle((Brush)Application.Current.FindResource("ControlBrush"), new Pen(accent, 1),
                new Rect(origin.X - 5, origin.Y - 3, text.Width + 10, text.Height + 6));
            dc.DrawText(text, origin);
        }
    }
}
