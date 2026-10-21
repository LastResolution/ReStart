using System.Windows;
using ReStart.App.Controls;

namespace ReStart.App;

public partial class MainWindow
{
    // Compare actual glyph bounds in the common coordinate space, including
    // scrollable shortcuts. A correct symbol font alone does not catch jumps.
    private bool NavigationGlyphsStayAligned()
    {
        var collapsed = VisualChildren<RailLabel>(NavigationRail).ToArray();
        var expanded = VisualChildren<RailLabel>(ExpandedRail).ToArray();
        if (collapsed.Length == 0 || collapsed.Length != expanded.Length) return false;
        return collapsed.Zip(expanded).All(pair =>
        {
            var (first, second) = pair;
            if (first.Glyph != second.Glyph || first.Icon.FontFamily.Source != second.Icon.FontFamily.Source
                || first.Icon.FontSize != second.Icon.FontSize || first.Icon.FontWeight != second.Icon.FontWeight) return false;
            var a = first.Icon.TransformToAncestor(Root).TransformBounds(new Rect(first.Icon.RenderSize));
            var b = second.Icon.TransformToAncestor(Root).TransformBounds(new Rect(second.Icon.RenderSize));
            return Math.Abs(a.X - b.X) < .01 && Math.Abs(a.Y - b.Y) < .01
                && Math.Abs(a.Width - b.Width) < .01 && Math.Abs(a.Height - b.Height) < .01;
        });
    }
}
