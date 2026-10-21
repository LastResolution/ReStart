using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;

namespace ReStart.App.Controls;

/// <summary>Edits a bounded local tile document with native selection, undo and theme-aware syntax colors.</summary>
internal sealed class TileCodeEditor : TextEditor
{
    /// <summary>Creates an editor with the bundled syntax definition and a user-input length ceiling.</summary>
    /// <param name="language">AvalonEdit definition name: JavaScript, HTML, CSS or Json.</param>
    /// <param name="maximum">Maximum UTF-16 characters accepted through typing or paste; save additionally enforces UTF-8 package limits.</param>
    internal TileCodeEditor(string language, int maximum)
    {
        FontFamily = new("Cascadia Mono, Consolas"); FontSize = 13; Padding = new(8);
        ShowLineNumbers = true; WordWrap = false; AllowDrop = false;
        HorizontalScrollBarVisibility = VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        SetResourceReference(BackgroundProperty, "TileBrush"); SetResourceReference(ForegroundProperty, "TextBrush");
        SetResourceReference(LineNumbersForegroundProperty, "MutedBrush");
        TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty, "FocusAccentBrush");
        Options.IndentationSize = 4; Options.ConvertTabsToSpaces = true;
        Options.EnableHyperlinks = Options.EnableEmailHyperlinks = false;
        SyntaxHighlighting = HighlightingManager.Instance.GetDefinition(language) ?? throw new ArgumentException("Unknown editor language.", nameof(language));
        TextArea.TextEntering += (_, e) => { if (Text.Length - SelectionLength + e.Text.Length > maximum) e.Handled = true; };
        DataObject.AddPastingHandler(this, (_, e) =>
        {
            var text = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? e.DataObject.GetData(DataFormats.Text) as string;
            if (text != null && Text.Length - SelectionLength + text.Length > maximum) e.CancelCommand();
        });
    }

    protected override IVisualLineTransformer CreateColorizer(IHighlightingDefinition highlightingDefinition) => new PaletteColorizer(highlightingDefinition);

    private sealed class PaletteColorizer(IHighlightingDefinition definition) : HighlightingColorizer(definition)
    {
        protected override void ApplyColorToElement(VisualLineElement element, HighlightingColor color)
        {
            if (color.Foreground == null) { base.ApplyColorToElement(element, color); return; }
            var themed = color.Clone();
            themed.Foreground = new SimpleHighlightingBrush(TokenColor(color.Name ?? "", ThemeWindow.IsLight, SystemParameters.HighContrast));
            themed.Background = null;
            base.ApplyColorToElement(element, themed);
        }
    }

    internal static Color TokenColor(string name, bool light, bool highContrast)
    {
        if (highContrast) return SystemColors.WindowTextColor;
        var token = name.ToLowerInvariant();
        var value = token.Contains("comment") ? light ? "#486B30" : "#8CB573"
            : token.Contains("string") || token.Contains("char") || token.Contains("value") ? light ? "#A31515" : "#E3A68D"
            : token.Contains("number") || token.Contains("digit") ? light ? "#086C51" : "#B5CEA8"
            : token.Contains("attribute") || token.Contains("property") ? light ? "#7D3E13" : "#9CDCFE"
            : token.Contains("tag") || token.Contains("selector") ? light ? "#800000" : "#D7BA7D"
            : token.Contains("keyword") || token.Contains("bool") || token.Contains("null") ? light ? "#7025A8" : "#D4A0D8"
            : light ? "#0756A5" : "#70BBF2";
        return (Color)ColorConverter.ConvertFromString(value);
    }
}
