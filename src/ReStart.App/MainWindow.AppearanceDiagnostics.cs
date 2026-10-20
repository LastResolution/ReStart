using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Exercises preset/custom surfaces, native settings saves and expanded navigation glyphs without changing real settings.</summary>
    internal async Task<AppearanceSmokeResult> VerifyAppearanceAsync(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("Appearance verification requires preview mode.");
        var directory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "fixture-" + Guid.NewGuid().ToString("N"));
        var store = new LayoutStore(Path.Combine(directory, "layout.json"));
        var fixture = new MainWindow(store, true, () => Task.FromResult(new List<AppEntry> { new("editor", "Sample editor", "", false), new("photos", "Sample photos", "", false) }))
        { Owner = this, Width = 1280, Height = 800, Topmost = false };
        var language = L.Language; var checks = 0; var paletteCases = 0; var editorCases = 0;
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        Color ColorOf(string key) => ((SolidColorBrush)fixture.FindResource(key)).Color;
        try
        {
            fixture.Present(); await fixture.InitializeAsync(); fixture._clock.Stop(); fixture.Document.Preferences.Animate = false;
            foreach (var tag in L.SupportedLanguages)
                foreach (var preset in ThemePresets.All)
                {
                    fixture.Document.Preferences.Language = tag; ThemePresets.Apply(fixture.Document.Preferences, preset.Id);
                    fixture.ApplyAppearance(); fixture.RenderGroups(); fixture.ExpandedRail.Visibility = Visibility.Visible; fixture.UpdateLayout();
                    var background = (Color)ColorConverter.ConvertFromString(preset.Background);
                    Check(SystemParameters.HighContrast || ColorOf("PanelBrush") == background && Contrast(ColorOf("TextBrush"), background) >= 4.5
                        && Contrast(ColorOf("TextBrush"), ColorOf("ControlBrush")) >= 4.5, "A preset surface or control lost its readable text palette.");
                    var labels = VisualChildren<RailLabel>(fixture.ExpandedRail).ToArray();
                    Check(labels.Length == 8 && labels.All(label => label.Glyph.Length == 1 && label.Icon.FontFamily.Source == "Segoe MDL2 Assets"
                        && new Typeface(label.Icon.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out var font) && font.CharacterToGlyphMap.ContainsKey(label.Glyph[0])
                        && label.Caption.Text.Length > 0 && Math.Abs(label.Icon.TransformToAncestor(fixture.ExpandedRail).Transform(new Point(label.Icon.ActualWidth / 2, 0)).X - 24) < 1),
                        "Expanded navigation lost its symbol font, labels or aligned icon column.");
                    Check(VisualChildren<Button>(fixture.ExpandedRail).All(b => AutomationProperties.GetName(b).Length > 0), "An expanded navigation action has no accessible name.");
                    var splash = new StartupWindow(() => false) { Owner = fixture, Topmost = false };
                    try
                    {
                        splash.Show(); splash.UpdateProgress(K.StartupTiles, .65); splash.UpdateLayout();
                        Check(((SolidColorBrush)((Border)splash.Content).Background).Color == ColorOf("PanelBrush") && ((SolidColorBrush)splash.Stage.Foreground).Color == ColorOf("TextBrush"),
                            "The splash did not use the selected color theme.");
                        if (tag is "en" or "ja") SaveStartupScreenshot(splash, Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"splash-{tag}-{preset.Id}.png"));
                    }
                    finally { splash.Close(); fixture.Activate(); }
                    if (tag is "en" or "ja") fixture.SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"rail-{tag}-{preset.Id}.png"));
                    paletteCases++;
                }
            fixture.Document.Preferences.Language = "en";
            foreach (var background in new[] { "#000000", "#FFFFFF", "#777777", "#0078D7" })
                foreach (var accent in new[] { "#000000", "#FFFFFF", "#777777" })
                {
                    fixture.Document.Preferences.BackgroundColor = background; fixture.Document.Preferences.Accent = accent; fixture.ApplyAppearance();
                    Check(SystemParameters.HighContrast || Contrast(ColorOf("TextBrush"), ColorOf("PanelBrush")) >= 4.5 && Contrast(ColorOf("MutedBrush"), ColorOf("PanelBrush")) >= 4.5
                        && Contrast(ColorOf("TextBrush"), ColorOf("ControlBrush")) >= 4.5 && Contrast(ColorOf("AccentTextBrush"), ColorOf("AccentBrush")) >= 4.5
                        && Contrast(ColorOf("FocusAccentBrush"), ColorOf("PanelBrush")) >= 3 && Contrast(ColorOf("ScrollThumbBrush"), ColorOf("PanelBrush")) >= 3,
                        "An extreme custom color made text, selected actions or scrollbars unreadable.");
                }
            foreach (var preset in ThemePresets.All)
            {
                Exception? error = null;
                var settings = new SettingsWindow(fixture, store) { Owner = fixture };
                settings.Loaded += (_, _) => settings.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        settings.SelectSection(L.T(K.SettingsAppearance)); settings.UpdateLayout();
                        VisualChildren<Button>(settings).Single(b => AutomationProperties.GetName(b) == L.T(preset.Name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        var background = VisualChildren<TextBox>(settings).Single(t => AutomationProperties.GetName(t) == L.T(K.SettingsBackgroundColor));
                        var accent = VisualChildren<TextBox>(settings).Single(t => AutomationProperties.GetName(t) == L.T(K.SettingsAccent));
                        Check(background.Text == preset.Background && accent.Text == preset.Accent, "Choosing a color theme did not update the editable colors.");
                        var before = System.Text.Json.JsonSerializer.Serialize(fixture.Document.Preferences);
                        var save = VisualChildren<Button>(settings).Single(b => b.IsDefault);
                        background.Text = "invalid"; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Check(settings.IsVisible && before == System.Text.Json.JsonSerializer.Serialize(fixture.Document.Preferences)
                            && VisualChildren<TextBlock>(settings).Any(t => t.Text == L.T(K.ErrorInvalidColor)), "Invalid color input was saved or silently ignored.");
                        background.Text = preset.Background; accent.Text = "#FFFFFF";
                        Check(!VisualChildren<TextBlock>(settings).Any(t => t.Text == L.T(K.ErrorInvalidColor)), "Corrected color input retained an obsolete validation error.");
                        SaveStartupScreenshot(settings, Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"settings-{preset.Id}.png"));
                        save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch (Exception ex) { error = ex; settings.Close(); }
                });
                settings.ShowDialog(); if (error != null) throw new InvalidOperationException("Appearance editor fixture failed: " + error.Message, error);
                fixture.RefreshLayout();
                Check(fixture.Document.Preferences.BackgroundColor == preset.Background && fixture.Document.Preferences.Accent == "#FFFFFF"
                    && store.Load()!.Preferences.BackgroundColor == preset.Background, "Custom colors did not survive the native save and persistence round trip.");
                editorCases++;
            }
            return new(checks, paletteCases, editorCases);
        }
        finally { fixture.Exit(); L.SetLanguage(language); ApplyAppearance(); Activate(); }
    }
}

internal sealed record AppearanceSmokeResult(int Checks, int LanguagePresetCases, int EditorCases);
