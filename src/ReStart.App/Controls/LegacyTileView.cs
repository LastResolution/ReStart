using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReStart.Core;

namespace ReStart.App.Controls;

/// <summary>Renders only parsed native text, raster images and column groups. No notification markup is loaded as WPF markup or browser content.</summary>
internal sealed class LegacyTileView : ContentControl
{
    internal LegacyTileView()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
    }
    private string _key = "";
    private LegacyTileBinding? _binding;
    private object? _imagesVersion;
    private Brush? _foreground;
    private string _badge = "";
    private TileSize _size;
    private double _scale;
    private bool _showName;
    internal bool HasTileContent { get; private set; }
    internal Brush? ContentForeground { get; private set; }

    internal void Apply(LegacyTileBinding? binding, string badge, TileSize size, double scale, bool showName, Brush foreground,
        Func<string, ImageSource?> image, object imagesVersion)
    {
        if (ReferenceEquals(binding, _binding) && badge == _badge && size == _size && scale == _scale && showName == _showName
            && ReferenceEquals(foreground, _foreground) && ReferenceEquals(imagesVersion, _imagesVersion)) return;
        _binding = binding; _badge = badge; _size = size; _scale = scale; _showName = showName; _foreground = foreground; _imagesVersion = imagesVersion;
        var imageKeys = binding == null ? "" : string.Join(',', Sources(binding.Elements).Select(s =>
            image(s) is { } bitmap ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(bitmap) : 0));
        var key = System.Text.Json.JsonSerializer.Serialize(binding) + badge + size + scale + showName + foreground + imageKeys;
        if (_key == key) return;
        _key = key; HasTileContent = false;
        var root = new Grid { ClipToBounds = true, IsHitTestVisible = false };
        var body = new StackPanel { Margin = new(3 * scale, 2 * scale, 3 * scale, size == TileSize.Small || !showName ? 0 : 19 * scale), ClipToBounds = true };
        var images = new Grid { ClipToBounds = true }; root.Children.Add(images); root.Children.Add(body);
        var hasBackground = !SystemParameters.HighContrast && binding != null
            && binding.Elements.Any(e => e.Kind == "image" && e.Placement is "background" or "peek" && image(e.Source) != null);
        var ink = hasBackground ? Brushes.White : foreground;
        ContentForeground = ink;
        if (binding != null)
            foreach (var element in binding.Elements)
                if (element.Kind == "image" && element.Placement is "background" or "peek" && image(element.Source) is { } background && !SystemParameters.HighContrast)
                {
                    images.Children.Add(new Image { Source = background, Stretch = Stretch.UniformToFill });
                    images.Children.Add(new Border { Background = Brushes.Black, Opacity = .45 }); HasTileContent = true;
                }
                else if (Build(element, ink, image, size, scale) is { } visual) { body.Children.Add(visual); HasTileContent = true; }
        if (badge.Length > 0)
        {
            var label = new TextBlock { Text = badge, Foreground = ink, FontWeight = FontWeights.SemiBold, FontSize = 11 * scale };
            root.Children.Add(new Border { Child = label, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Padding = new(2), Margin = new(2) });
        }
        Content = root;
    }

    private static FrameworkElement? Build(LegacyTileElement element, Brush ink, Func<string, ImageSource?> image, TileSize size, double scale)
    {
        if (element.Kind == "text")
        {
            if (element.Text.Length == 0) return null;
            var style = element.Style.ToLowerInvariant().Replace("subtle", "");
            var font = style switch { "caption" => 12, "subtitle" => 20, "title" => 24, "subheader" => 30, "header" => 42, _ => 15 };
            return new TextBlock
            {
                Text = element.Text,
                Foreground = ink,
                FontSize = (size == TileSize.Small ? Math.Min(font, 14) : font) * scale,
                Opacity = element.Style.EndsWith("subtle", StringComparison.OrdinalIgnoreCase) ? .8 : 1,
                TextWrapping = element.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = element.Align switch { "center" => TextAlignment.Center, "right" => TextAlignment.Right, _ => TextAlignment.Left },
                Margin = new(0, 0, 0, scale),
                MaxHeight = (size == TileSize.Small ? 30 : size == TileSize.Large ? 120 : 54) * scale
            };
        }
        if (element.Kind == "image")
        {
            if (SystemParameters.HighContrast || image(element.Source) is not { } bitmap) return null;
            return new Image { Source = bitmap, Stretch = Stretch.Uniform, MaxHeight = (size == TileSize.Small ? 26 : size == TileSize.Large ? 120 : 48) * scale };
        }
        if (element.Kind == "group")
        {
            var group = new Grid(); var index = 0;
            foreach (var child in element.Children)
                if (Build(child, ink, image, size, scale) is { } visual)
                {
                    group.ColumnDefinitions.Add(new() { Width = new(child.Weight, GridUnitType.Star) });
                    Grid.SetColumn(visual, index++); group.Children.Add(visual);
                }
            return group.Children.Count > 0 ? group : null;
        }
        var column = new StackPanel();
        foreach (var child in element.Children)
            if (Build(child, ink, image, size, scale) is { } visual) column.Children.Add(visual);
        return column.Children.Count > 0 ? column : null;
    }

    private static IEnumerable<string> Sources(IEnumerable<LegacyTileElement> elements) => elements.SelectMany(e =>
        (e.Kind == "image" ? new[] { e.Source } : []).Concat(Sources(e.Children)));
}
