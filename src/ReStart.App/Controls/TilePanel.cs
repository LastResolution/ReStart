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

    /// <summary>Measures tile footprints and retains two spare drop rows beneath the last occupied row.</summary>
    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        var rows = 0;
        for (var i = 0; i < InternalChildren.Count && i < Placements.Count; i++)
        {
            var p = Placements[i];
            InternalChildren[i].Measure(new(p.Width * Cell - 4, p.Height * Cell - 4));
            rows = Math.Max(rows, p.Row + p.Height);
        }
        return new(Columns * Cell, Math.Max(4, rows + 2) * Cell);
    }

    /// <summary>Maps logical cell rectangles to device-independent pixels with a four-pixel tile gap.</summary>
    protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
    {
        for (var i = 0; i < InternalChildren.Count && i < Placements.Count; i++)
        {
            var p = Placements[i];
            InternalChildren[i].Arrange(new Rect(p.Column * Cell, p.Row * Cell, p.Width * Cell - 4, p.Height * Cell - 4));
        }
        return finalSize;
    }
}
