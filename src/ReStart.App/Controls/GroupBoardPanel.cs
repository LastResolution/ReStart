using System.Windows;
using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App.Controls;

/// <summary>Displays independently positioned groups and keeps empty board space available as a drag target.</summary>
public sealed class GroupBoardPanel : Panel
{
    /// <summary>Creates a board whose empty regions participate in hit testing.</summary>
    public GroupBoardPanel() => Background = System.Windows.Media.Brushes.Transparent;
    /// <summary>Gets or sets the configured number of tile cells per group.</summary>
    public int TileColumns { get; set; } = 6;
    /// <summary>Gets or sets one tile cell size in DIPs.</summary>
    public double Cell { get; set; } = 52;
    /// <summary>Gets or sets the transient folder expansion used when measuring group collisions.</summary>
    public string? ExpandedFolderId { get; set; }
    /// <summary>Gets or sets a transient minimum height for scrolling a drag into previously empty board space.</summary>
    public double DragMinimumHeight { get; set; }
    /// <summary>Gets the number of group columns available at the last measure pass.</summary>
    public int Columns { get; private set; } = 1;
    /// <summary>Gets group bounds in the same model grid used by drag/drop and persistence.</summary>
    public IReadOnlyList<GroupPlacement> Placements { get; private set; } = [];

    /// <summary>Measures group content and the union of its logical positions, including blank gaps.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : TileColumns * Cell;
        Columns = GroupLayout.ColumnCount(width, TileColumns, Cell);
        foreach (UIElement child in InternalChildren) child.Measure(new(TileColumns * Cell, double.PositiveInfinity));
        Placements = GroupLayout.Pack(InternalChildren.OfType<FrameworkElement>().Select(e => e.Tag).OfType<TileGroup>(), Columns, TileColumns, Cell, expandedFolderId: ExpandedFolderId);
        var height = Placements.Count == 0 ? Cell * 4 : Placements.Max(p => (p.Row + p.Rows) * Cell / 2) + Cell * 2;
        return new(Math.Max(0, width), Math.Max(height, Math.Max(MinHeight, DragMinimumHeight)));
    }

    /// <summary>Places each group at its own column and vertical anchor rather than in shared wrap-panel rows.</summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (FrameworkElement child in InternalChildren)
        {
            if (child.Tag is TileGroup group)
            {
                var p = Placements.First(p => p.Group == group);
                child.Arrange(new(p.Column * (TileColumns * Cell + GroupLayout.Gap), p.Row * Cell / 2, TileColumns * Cell, child.DesiredSize.Height));
            }
            else child.Arrange(new Rect(new Point(), child.DesiredSize));
        }
        return finalSize;
    }
}
