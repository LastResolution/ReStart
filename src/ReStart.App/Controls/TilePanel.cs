using System.Windows;
using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App.Controls;

/// <summary>Arranges tile buttons at logical cells and preserves hit-testable gaps for drag targets.</summary>
public sealed class TilePanel : Panel
{
    /// <summary>Creates a panel whose empty cells remain valid drag targets.</summary>
    public TilePanel() => Background = System.Windows.Media.Brushes.Transparent;
    /// <summary>Gets or sets the logical column count of the group.</summary>
    public int Columns { get; set; } = 6;
    /// <summary>Gets or sets one logical cell size in device-independent pixels.</summary>
    public double Cell { get; set; } = 52;
    /// <summary>Gets or sets placements in the same order as the panel children.</summary>
    public IReadOnlyList<TilePlacement> Placements { get; set; } = [];
    /// <summary>Gets or sets a transient inline folder expansion following the root tile buttons.</summary>
    public FolderExpansion? Expansion { get; set; }
    /// <summary>Gets or sets the owning folder when this panel displays folder children.</summary>
    public Tile? FolderOwner { get; set; }

    /// <summary>Removes transient folder-expansion height from a pointer Y coordinate below the expanded area.</summary>
    public Point ToModelPoint(Point point)
    {
        if (Expansion != null && point.Y >= Expansion.Row * Cell + Expansion.ExtraHeight(Cell)) point.Y -= Expansion.ExtraHeight(Cell);
        return point;
    }

    /// <summary>Measures occupied tile rows; the surrounding board handles drops into empty space.</summary>
    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        var rows = 0;
        for (var i = 0; i < InternalChildren.Count && i < Placements.Count; i++)
        {
            var p = Placements[i];
            InternalChildren[i].Measure(new(p.Width * Cell - 4, p.Height * Cell - 4));
            rows = Math.Max(rows, p.Row + p.Height);
        }
        if (Expansion != null && InternalChildren.Count > Placements.Count)
            InternalChildren[Placements.Count].Measure(new(Columns * Cell, Expansion.ExtraHeight(Cell)));
        return new(Columns * Cell, Math.Max(1, rows) * Cell + (Expansion?.ExtraHeight(Cell) ?? 0));
    }

    /// <summary>Maps logical cell rectangles to device-independent pixels with a four-pixel tile gap.</summary>
    protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
    {
        for (var i = 0; i < InternalChildren.Count && i < Placements.Count; i++)
        {
            var p = Placements[i];
            InternalChildren[i].Arrange(new Rect(p.Column * Cell, Expansion?.TileY(p, Cell) ?? p.Row * Cell, p.Width * Cell - 4, p.Height * Cell - 4));
        }
        if (Expansion != null && InternalChildren.Count > Placements.Count)
            InternalChildren[Placements.Count].Arrange(new Rect(0, Expansion.Row * Cell, Columns * Cell, Expansion.ExtraHeight(Cell)));
        return finalSize;
    }
}
