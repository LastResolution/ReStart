using System.Windows;
using System.Windows.Controls;

namespace ReStart.App.Controls;

public sealed class AppListPanel : Panel
{
    public int Columns { get; set; } = 3;
    private const double RowHeight = 44;
    private int Rows => Math.Max(1, (int)Math.Ceiling(InternalChildren.Count / (double)Columns) + 1);
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : Columns * 304;
        foreach (UIElement child in InternalChildren) child.Measure(new(Math.Max(80, width / Columns - 24), RowHeight));
        return new(width, Rows * RowHeight);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var row = 0; var col = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (row >= Rows || row == Rows - 1 && child is FrameworkElement { Tag: "section" }) { col++; row = 0; }
            child.Arrange(new(col * finalSize.Width / Columns, row * RowHeight, Math.Max(80, finalSize.Width / Columns - 24), RowHeight));
            row++;
        }
        return finalSize;
    }
}
