using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ReStart.App.Controls;

internal sealed class DropAdorner(UIElement target) : Adorner(target)
{
    public Rect CellBounds { get; set; }
    public bool FolderTarget { get; set; }
    protected override void OnRender(DrawingContext dc)
    {
        var accent = (Brush)Application.Current.FindResource("AccentBrush");
        var fill = accent.Clone(); fill.Opacity = 0.2;
        var pen = new Pen(accent, 3) { DashStyle = FolderTarget ? DashStyles.Solid : DashStyles.Dash };
        dc.DrawRectangle(fill, pen, CellBounds);
        if (FolderTarget) dc.DrawRoundedRectangle(null, pen, new Rect(CellBounds.X + 8, CellBounds.Y + 8, Math.Max(1, CellBounds.Width - 16), Math.Max(1, CellBounds.Height - 16)), 4, 4);
    }
}
