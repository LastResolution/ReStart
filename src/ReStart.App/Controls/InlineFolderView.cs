using System.Windows;
using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App.Controls;

/// <summary>Displays folder contents between tile rows, with a name field and separator lines.</summary>
internal sealed class InlineFolderView : Border
{
    internal const double ContentTopInset = 5;
    internal TilePanel Tiles { get; }
    internal InlineFolderView(GroupHeader header, TilePanel tiles)
    {
        Tiles = tiles;
        BorderThickness = new(0, 0, 0, 1);
        Padding = new(0, ContentTopInset, 0, 6);
        SetResourceReference(BorderBrushProperty, "BorderBrush");
        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(tiles);
        Child = body;
    }
}
