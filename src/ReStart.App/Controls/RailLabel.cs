using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReStart.App.Controls;

/// <summary>Keeps navigation glyphs in their symbol font, separate from localized labels and account names.</summary>
internal sealed class RailLabel : Grid
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(RailLabel), new PropertyMetadata("", Changed));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(RailLabel), new PropertyMetadata("", Changed));
    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    internal TextBlock Icon { get; } = new() { FontFamily = new("Segoe MDL2 Assets"), FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    internal TextBlock Caption { get; } = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    public RailLabel()
    {
        ColumnDefinitions.Add(new() { Width = new(44) }); ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        Children.Add(Icon); SetColumn(Caption, 1); Children.Add(Caption);
    }
    private static void Changed(DependencyObject source, DependencyPropertyChangedEventArgs args)
    { var label = (RailLabel)source; label.Icon.Text = label.Glyph; label.Caption.Text = label.Label; }
}
