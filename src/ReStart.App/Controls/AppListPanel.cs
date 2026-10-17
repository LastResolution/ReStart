using System.Windows;
using System.Windows.Controls;

namespace ReStart.App.Controls;

/// <summary>Balances the flattened app list into columns while keeping section headers with their next row.</summary>
public sealed class AppListPanel : Panel
{
    /// <summary>Gets the column count chosen from the available width, with a minimum app-row width of 280 DIPs.</summary>
    public int Columns { get; private set; } = 1;
    private const double RowHeight = 44;
    private const double MinimumColumnWidth = 280;
    private const double ColumnGap = 24;
    private int Rows => Math.Max(1, (int)Math.Ceiling(InternalChildren.Count / (double)Columns) + 1);
    private double ColumnWidth(double width) => Math.Max(0, (width - (Columns - 1) * ColumnGap) / Columns);
    /// <summary>Uses the full viewport width and measures balanced, fixed-height rows with gaps only between columns.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? Math.Max(0, availableSize.Width) : MinimumColumnWidth;
        Columns = Math.Clamp((int)((width + ColumnGap) / (MinimumColumnWidth + ColumnGap)), 1, Math.Max(1, InternalChildren.Count));
        foreach (UIElement child in InternalChildren) child.Measure(new(ColumnWidth(width), RowHeight));
        return new(width, Rows * RowHeight);
    }
    /// <summary>Places entries column-first and avoids an orphaned section header at a column boundary.</summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var row = 0; var col = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (row >= Rows || row == Rows - 1 && child is FrameworkElement { Tag: "section" }) { col++; row = 0; }
            var width = ColumnWidth(finalSize.Width);
            child.Arrange(new(col * (width + ColumnGap), row * RowHeight, width, RowHeight));
            row++;
        }
        return finalSize;
    }
}
