using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using ReStart.Core;

namespace ReStart.App;

internal sealed partial class SettingsWindow
{
    private TextBox _backgroundColor = null!, _accentColor = null!;
    private ComboBox _baseTheme = null!;
    private CheckBox _startAccent = null!;
    private readonly Dictionary<string, Button> _presetButtons = [];

    private void BuildAppearance(Panel content)
    {
        _baseTheme = Select(content, L.T(K.SettingsBaseTheme), new[] { L.T(K.ThemeSystem), L.T(K.ThemeDark), L.T(K.ThemeLight) }, (int)_draft.Theme, i =>
        {
            _draft.Theme = (ReStart.Core.ThemeMode)i;
            if (_backgroundColor != null) _backgroundColor.Text = "";
        });
        content.Children.Add(new TextBlock { Text = L.T(K.SettingsColorThemes), Margin = new(0, 12, 0, 8) });
        var presets = new WrapPanel();
        foreach (var preset in ThemePresets.All)
        {
            var label = new StackPanel();
            label.Children.Add(new Border { Height = 4, Width = 28, Background = ColorBrush(preset.Accent), HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 0, 0, 8) });
            label.Children.Add(new TextBlock { Text = L.T(preset.Name), TextWrapping = TextWrapping.Wrap });
            var button = new Button
            {
                Content = label,
                Width = 100,
                Height = 72,
                Margin = new(0, 0, 6, 6),
                Background = ColorBrush(preset.Background),
                Foreground = MainWindow.ReadableInk(ColorBrush(preset.Background).Color),
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            AutomationProperties.SetName(button, L.T(preset.Name));
            if (SystemParameters.HighContrast) { button.Background = SystemColors.WindowBrush; button.Foreground = SystemColors.WindowTextBrush; }
            button.Click += (_, _) => ApplyColorPreset(preset.Id);
            _presetButtons[preset.Id] = button; presets.Children.Add(button);
        }
        content.Children.Add(presets);
        _backgroundColor = ColorField(content, L.T(K.SettingsBackgroundColor), _draft.BackgroundColor, value => { _draft.BackgroundColor = value; if (value.Length > 0 && _startAccent != null) _startAccent.IsChecked = false; });
        _accentColor = ColorField(content, L.T(K.SettingsAccent), _draft.Accent, value => _draft.Accent = value);
        Note(content, L.T(K.SettingsCustomColorsHelp));
        DataButton(content, L.T(K.SettingsResetColors), () => { _backgroundColor.Text = ""; _accentColor.Text = "#0078D7"; _startAccent.IsChecked = false; });
        _startAccent = Check(content, L.T(K.SettingsStartAccent), _draft.ShowAccentOnStart, value =>
        { _draft.ShowAccentOnStart = value; if (value) _backgroundColor.Text = ""; });
        Note(content, L.T(K.SettingsStartAccentHelp));
        Check(content, L.T(K.SettingsWallpaper), _draft.ShowWallpaper, v => _draft.ShowWallpaper = v);
        Check(content, L.T(K.SettingsColoredTiles), _draft.ColoredTiles, v => _draft.ColoredTiles = v);
        Check(content, L.T(K.SettingsAnimation), _draft.Animate, v => _draft.Animate = v);
        Select(content, L.T(K.SettingsGroupWidth), new[] { L.T(K.ColumnsThree), L.T(K.ColumnsFour) }, _draft.Columns == 8 ? 1 : 0, i => _draft.Columns = i == 1 ? 8 : 6);
        Slide(content, L.T(K.SettingsTileSize), 0.7, 1.5, _draft.TileScale, v => _draft.TileScale = v, true);
        Slide(content, L.T(K.SettingsOpacity), 0.3, 1, _draft.BackgroundOpacity, v => _draft.BackgroundOpacity = v, false);
    }

    private void ApplyColorPreset(string id)
    {
        var preset = ThemePresets.All.Single(p => p.Id == id);
        _baseTheme.SelectedIndex = (int)preset.Mode;
        ThemePresets.Apply(_draft, id);
        _backgroundColor.Text = preset.Background; _accentColor.Text = preset.Accent; _startAccent.IsChecked = false;
    }

    private TextBox ColorField(Panel panel, string label, string initial, Action<string> changed)
    {
        var row = new DockPanel { Margin = new(0, 10, 0, 7) };
        row.Children.Add(new TextBlock { Text = label, Width = 170, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        var preview = new Border { Width = 32, Height = 32, Margin = new(8, 0, 0, 0) };
        DockPanel.SetDock(preview, Dock.Right); row.Children.Add(preview);
        var text = new TextBox { Text = initial, MaxLength = 7, MinWidth = 130, HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(text, label);
        void Refresh()
        {
            var color = text.Text.Trim();
            preview.Background = ThemePresets.IsColor(color) ? ColorBrush(color) : Brushes.Transparent;
            if (color.Length == 0 || ThemePresets.IsColor(color)) changed(color.ToUpperInvariant());
            if (_backgroundColor != null && _accentColor != null && (_backgroundColor.Text.Trim().Length == 0 || ThemePresets.IsColor(_backgroundColor.Text.Trim()))
                && ThemePresets.IsColor(_accentColor.Text.Trim()) && _feedback.Text == L.T(K.ErrorInvalidColor)) _feedback.Text = "";
        }
        text.TextChanged += (_, _) => Refresh(); Refresh();
        row.Children.Add(text); panel.Children.Add(row); return text;
    }
    private bool ValidateColors()
    {
        var invalid = _backgroundColor.Text.Trim().Length > 0 && !ThemePresets.IsColor(_backgroundColor.Text.Trim()) ? _backgroundColor
            : !ThemePresets.IsColor(_accentColor.Text.Trim()) ? _accentColor : null;
        if (invalid == null) return true;
        SelectSection(L.T(K.SettingsAppearance)); _feedback.Text = L.T(K.ErrorInvalidColor); invalid.Focus(); return false;
    }
    private static SolidColorBrush ColorBrush(string color) => new((Color)ColorConverter.ConvertFromString(color));
}
