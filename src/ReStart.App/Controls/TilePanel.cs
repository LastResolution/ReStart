using System.Windows;
using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App.Controls;

public sealed class TilePanel : Panel
{
    public TilePanel() => Background = System.Windows.Media.Brushes.Transparent;
    public int Columns { get; set; } = 6;
    public double Cell { get; set; } = 52;
    public IReadOnlyList<TilePlacement> Placements { get; set; } = [];

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
