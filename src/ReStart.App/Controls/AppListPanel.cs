using System.Windows;
using System.Windows.Controls;

namespace ReStart.App.Controls;

/// <summary>Balances the flattened app list into columns while keeping section headers with their next row.</summary>
public sealed class AppListPanel : Panel
{
    /// <summary>Gets or sets the number of app-list columns; the main window constrains this to one through four.</summary>
    public int Columns { get; set; } = 3;
    private const double RowHeight = 44;
    private int Rows => Math.Max(1, (int)Math.Ceiling(InternalChildren.Count / (double)Columns) + 1);
    /// <summary>Measures balanced rows at the fixed app-list row height.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : Columns * 304;
        foreach (UIElement child in InternalChildren) child.Measure(new(Math.Max(80, width / Columns - 24), RowHeight));
        return new(width, Rows * RowHeight);
    }
    /// <summary>Places entries column-first and avoids an orphaned section header at a column boundary.</summary>
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
