using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ReStart.App.Controls;

internal sealed class DropAdorner(UIElement target) : Adorner(target)
{
    public Rect CellBounds { get; set; }
    protected override void OnRender(DrawingContext dc)
    {
        var pen = new Pen((Brush)Application.Current.FindResource("AccentBrush"), 3) { DashStyle = DashStyles.Dash };
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(60, 0, 120, 215)), pen, CellBounds);
    }
}
