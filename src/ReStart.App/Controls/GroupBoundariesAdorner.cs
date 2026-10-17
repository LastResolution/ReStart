using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ReStart.Core;

namespace ReStart.App.Controls;

/// <summary>Shows a soft rectangular hover surface and named group boundaries during a drag.</summary>
internal sealed class GroupBoundariesAdorner(GroupBoardPanel board) : Adorner(board)
{
    internal bool ShowAll { get; set; }
    internal TileGroup? HoveredGroup { get; set; }

    protected override void OnRender(DrawingContext drawingContext)
    {
        foreach (var placement in board.Placements.Where(p => !p.Group.IsStandalone && (ShowAll || p.Group == HoveredGroup)))
        {
            var brush = ((Brush)board.FindResource("TextBrush")).CloneCurrentValue();
            var bounds = new Rect(placement.Left, placement.Top, placement.Width, placement.Height);
            if (ShowAll)
            {
                brush.Opacity = .25;
                drawingContext.DrawRectangle(null, new Pen(brush, 1) { DashStyle = DashStyles.Dash }, bounds);
            }
            else
            {
                brush.Opacity = .035;
                drawingContext.DrawRoundedRectangle(brush, null, bounds, 2, 2);
            }
        }
    }
}
