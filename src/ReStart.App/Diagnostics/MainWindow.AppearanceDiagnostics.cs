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
        store.Save(new()
        {
            Preferences = new() { ShowWallpaper = false, Animate = false, Language = "en" },
            Groups = [new() { Name = "Sample apps", Tiles = [new() { AppId = "editor", Label = "Sample editor" }, new() { AppId = "photos", Label = "Sample photos" }] }]
        });
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
                    foreach (var mode in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light, ReStart.Core.ThemeMode.System })
                    {
                        fixture.Document.Preferences.Language = tag; fixture.Document.Preferences.Theme = mode; ThemePresets.Apply(fixture.Document.Preferences, preset.Id);
                        fixture.ApplyAppearance(); fixture.RenderGroups(); fixture.ExpandedRail.Visibility = Visibility.Visible; fixture.UpdateLayout();
                        var background = (Color)ColorConverter.ConvertFromString(preset.Palette(mode, ThemeWindow.SystemIsLight).Background);
                        Check(SystemParameters.HighContrast || ColorOf("PanelBrush") == background && Contrast(ColorOf("TextBrush"), background) >= 4.5
                            && Contrast(ColorOf("TextBrush"), ColorOf("ControlBrush")) >= 4.5, "A preset surface or control lost its readable text palette.");
                        var labels = VisualChildren<RailLabel>(fixture.ExpandedRail).ToArray();
                        Check(labels.Length == 9 && labels.All(label => label.Glyph.Length == 1 && label.Icon.FontFamily.Source == "Segoe MDL2 Assets"
                            && new Typeface(label.Icon.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out var font) && font.CharacterToGlyphMap.ContainsKey(label.Glyph[0])
                            && label.Caption.Text.Length > 0 && Math.Abs(label.Icon.TransformToAncestor(fixture.ExpandedRail).Transform(new Point(label.Icon.ActualWidth / 2, 0)).X - 24) < 1),
                            "Expanded navigation lost its symbol font, labels or aligned icon column.");
                        Check(VisualChildren<Button>(fixture.ExpandedRail).All(b => AutomationProperties.GetName(b).Length > 0), "An expanded navigation action has no accessible name.");
                        Check(fixture.NavigationGlyphsStayAligned(), "Expanding navigation moved or resized a glyph.");
                        if (preset.Id == "midnight" && mode is ReStart.Core.ThemeMode.Dark or ReStart.Core.ThemeMode.Light)
                            foreach (var accept in new[] { false, true })
                            {
                                var question = L.F(K.ConfirmDeleteGroup, "Sample group");
                                var confirmation = new ConfirmationWindow(question) { Owner = fixture };
                                Exception? dialogError = null;
                                confirmation.Loaded += (_, _) => confirmation.Dispatcher.BeginInvoke(() =>
                                {
                                    try
                                    {
                                        confirmation.UpdateLayout();
                                        Check(((SolidColorBrush)confirmation.Background).Color == ColorOf("PanelBrush")
                                            && ((SolidColorBrush)confirmation.Foreground).Color == ColorOf("TextBrush")
                                            && ((SolidColorBrush)confirmation.AcceptButton.Background).Color == ColorOf("AccentBrush"), "Confirmation lost the application palette.");
                                        Check(confirmation.CancelButton.IsDefault && confirmation.CancelButton.IsCancel && !confirmation.AcceptButton.IsDefault
                                            && confirmation.CancelButton.IsKeyboardFocused, "Confirmation does not default safely to No.");
                                        Check(Equals(confirmation.AcceptButton.Content, L.T(K.CommonYes)) && Equals(confirmation.CancelButton.Content, L.T(K.CommonNo))
                                            && VisualChildren<TextBlock>(confirmation).Any(t => t.Text == question), "Confirmation actions or question are not localized.");
                                        if (!accept && tag is "en" or "ja") SaveStartupScreenshot(confirmation, Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"confirmation-{tag}-{mode}.png"));
                                        (accept ? confirmation.AcceptButton : confirmation.CancelButton).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                    }
                                    catch (Exception ex) { dialogError = ex; confirmation.Close(); }
                                });
                                var accepted = confirmation.ShowDialog() == true;
                                if (dialogError != null) throw new InvalidOperationException("Confirmation fixture failed.", dialogError);
                                Check(accepted == accept, "Confirmation returned the wrong result.");
                                fixture.Activate();
                            }
                        var splash = new StartupWindow(() => false) { Owner = fixture, Topmost = false };
                        try
                        {
                            splash.Show(); splash.UpdateProgress(K.StartupTiles, .65); splash.UpdateLayout();
                            Check(((SolidColorBrush)((Border)splash.Content).Background).Color == ColorOf("PanelBrush") && ((SolidColorBrush)splash.Stage.Foreground).Color == ColorOf("TextBrush"),
                                "The splash did not use the selected color theme.");
                            if (tag is "en" or "ja") SaveStartupScreenshot(splash, Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"splash-{tag}-{preset.Id}-{mode}.png"));
                        }
                        finally { splash.Close(); fixture.Activate(); }
                        if (tag is "en" or "ja") fixture.SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"rail-{tag}-{preset.Id}-{mode}.png"));
                        paletteCases++;
                    }
            fixture.Document.Preferences.Language = "en"; fixture.Document.Preferences.ColorThemeId = "";
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
                foreach (var mode in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
                {
                    fixture.Document.Preferences.Theme = mode; fixture.Document.Preferences.ColorThemeId = "";
                    var palette = preset.Palette(mode, false);
                    Edit(settings =>
                    {
                        VisualChildren<Button>(settings).Single(b => AutomationProperties.GetName(b) == L.T(preset.Name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Check(Field(settings, K.SettingsBackgroundColor).Text == palette.Background && Field(settings, K.SettingsAccent).Text == palette.Accent
                            && Mode(settings).SelectedIndex == (int)mode, "Choosing a family changed the base mode or chose the wrong variant.");
                        Save(settings);
                    });
                    fixture.RefreshLayout();
                    Check(store.Load()!.Preferences.ColorThemeId == preset.Id && fixture.Document.Preferences.Theme == mode,
                        "Saving a color family lost its identity or changed the base mode.");
                    Edit(settings =>
                    {
                        var background = Field(settings, K.SettingsBackgroundColor); var accent = Field(settings, K.SettingsAccent);
                        var flipped = mode == ReStart.Core.ThemeMode.Dark ? ReStart.Core.ThemeMode.Light : ReStart.Core.ThemeMode.Dark;
                        Mode(settings).SelectedIndex = (int)flipped;
                        Check(background.Text == preset.Palette(flipped, false).Background && accent.Text == preset.Palette(flipped, false).Accent,
                            "Reopening and switching base mode lost the selected family.");
                        Mode(settings).SelectedIndex = (int)mode;
                        var before = System.Text.Json.JsonSerializer.Serialize(fixture.Document.Preferences);
                        background.Text = "invalid"; Save(settings);
                        Check(settings.IsVisible && before == System.Text.Json.JsonSerializer.Serialize(fixture.Document.Preferences)
                            && VisualChildren<TextBlock>(settings).Any(t => t.Text == L.T(K.ErrorInvalidColor)), "Invalid color input was saved or silently ignored.");
                        background.Text = palette.Background; accent.Text = "#FFFFFF";
                        Check(!VisualChildren<TextBlock>(settings).Any(t => t.Text == L.T(K.ErrorInvalidColor)), "Corrected color input retained an obsolete validation error.");
                        Mode(settings).SelectedIndex = (int)flipped;
                        Check(background.Text == palette.Background && accent.Text == "#FFFFFF", "Changing the base mode overwrote custom RGB choices.");
                        SaveStartupScreenshot(settings, Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"settings-{preset.Id}-{mode}.png"));
                        Save(settings);
                    });
                    fixture.RefreshLayout();
                    Check(fixture.Document.Preferences.ColorThemeId == "" && fixture.Document.Preferences.BackgroundColor == palette.Background
                        && fixture.Document.Preferences.Accent == "#FFFFFF" && store.Load()!.Preferences.BackgroundColor == palette.Background,
                        "Custom colors did not leave the family or survive the native persistence round trip.");
                    if (preset.Id == "ocean" && mode == ReStart.Core.ThemeMode.Dark)
                    {
                        Edit(settings => { Field(settings, K.SettingsBackgroundColor).Text = palette.Background; Field(settings, K.SettingsAccent).Text = palette.Accent; Save(settings); });
                        fixture.RefreshLayout();
                        Check(store.Load()!.Preferences.ColorThemeId == "", "Exact custom RGB values matching a preset were relabelled while saving.");
                        Edit(settings =>
                        {
                            Check(Field(settings, K.SettingsBackgroundColor).Text == palette.Background && Field(settings, K.SettingsAccent).Text == palette.Accent
                                && Mode(settings).SelectedIndex == (int)ReStart.Core.ThemeMode.Light, "Reopening custom RGB matching a dark preset silently selected its light variant.");
                            settings.Close();
                        });
                    }
                    editorCases++;
                }
            var pixels = new byte[640 * 360 * 4];
            for (var y = 0; y < 360; y++)
                for (var x = 0; x < 640; x++)
                {
                    var index = (y * 640 + x) * 4; var square = (x / 32 + y / 32) % 2 == 0;
                    pixels[index] = square ? (byte)190 : (byte)70; pixels[index + 1] = square ? (byte)110 : (byte)180;
                    pixels[index + 2] = square ? (byte)40 : (byte)220; pixels[index + 3] = 255;
                }
            var sampleImage = System.Windows.Media.Imaging.BitmapSource.Create(640, 360, 96, 96, PixelFormats.Bgra32, null, pixels, 640 * 4); sampleImage.Freeze();
            fixture.Document.Preferences.ColorThemeId = ""; fixture.Document.Preferences.BackgroundColor = ""; fixture.Document.Preferences.Accent = "#0078D7";
            foreach (var tag in L.SupportedLanguages)
                foreach (var mode in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
                {
                    fixture.Document.Preferences.Language = tag; fixture.Document.Preferences.Theme = mode; fixture.ApplyAppearance();
                    Edit(settings =>
                    {
                        var opacity = VisualChildren<Slider>(settings).Single(s => AutomationProperties.GetName(s) == L.T(K.SettingsOpacity));
                        var blur = VisualChildren<Slider>(settings).Single(s => AutomationProperties.GetName(s) == L.T(K.SettingsBlur));
                        Check(opacity.Minimum == .1 && opacity.Maximum == 1 && blur.Minimum == 0 && blur.Maximum == 50 && blur.IsSnapToTickEnabled,
                            "Wallpaper controls lost their opacity or blur ranges.");
                        opacity.Value = .1; blur.Value = 0; Save(settings);
                    });
                    var saved = store.Load()!.Preferences;
                    Check(saved.BackgroundOpacity == .1 && saved.BackgroundBlur == 0, "Native save/reload lost low opacity or disabled blur.");
                    foreach (var opacity in new[] { .1, .3, .86, 1d })
                        foreach (var radius in new[] { 0d, 12d, 35d, 50d })
                        {
                            fixture.Document.Preferences.BackgroundOpacity = opacity; fixture.Document.Preferences.BackgroundBlur = radius;
                            fixture.Document.Preferences.ShowWallpaper = true; fixture.Document.Preferences.UseCustomBackground = false;
                            fixture.ApplyAppearance(); if (!SystemParameters.HighContrast) fixture.Wallpaper.Source = sampleImage; fixture.UpdateLayout();
                            Check(((SolidColorBrush)fixture.Tint.Background).Color.A == (SystemParameters.HighContrast ? 255 : (byte)(255 * opacity)),
                                "Rendered tint did not follow the selected alpha or high-contrast override.");
                            Check(SystemParameters.HighContrast || radius == 0 ? fixture.Wallpaper.Effect == null
                                : fixture.Wallpaper.Effect is System.Windows.Media.Effects.BlurEffect effect && effect.Radius == radius,
                                "Wallpaper blur did not follow the configured radius.");
                            Check(fixture.Root.Effect == null && fixture.Tint.Effect == null, "Wallpaper blur was applied to foreground controls.");
                            if (tag == "en" && mode == ReStart.Core.ThemeMode.Dark && opacity == .1 && radius is 0 or 12 or 50)
                                fixture.SaveScreenshot(Path.Combine(directory, $"wallpaper-10-blur-{radius:0}.png"));
                        }
                }
            checks += fixture.VerifyEmptyPinnedHome(directory);
            return new(checks, paletteCases, editorCases);
        }
        finally { fixture.Exit(); L.SetLanguage(language); ApplyAppearance(); Activate(); }

        TextBox Field(SettingsWindow settings, ReStart.Core.TextKey key) => VisualChildren<TextBox>(settings).Single(t => AutomationProperties.GetName(t) == L.T(key));
        ComboBox Mode(SettingsWindow settings) => VisualChildren<ComboBox>(settings).Single(c => AutomationProperties.GetName(c) == L.T(K.SettingsBaseTheme));
        void Save(SettingsWindow settings) => VisualChildren<Button>(settings).Single(b => b.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        void Edit(Action<SettingsWindow> edit)
        {
            Exception? error = null;
            var settings = new SettingsWindow(fixture, store) { Owner = fixture };
            settings.Loaded += (_, _) => settings.Dispatcher.BeginInvoke(() =>
            {
                try { settings.SelectSection(L.T(K.SettingsAppearance)); settings.UpdateLayout(); edit(settings); }
                catch (Exception ex) { error = ex; settings.Close(); }
            });
            settings.ShowDialog();
            if (error != null) throw new InvalidOperationException("Appearance editor fixture failed: " + error.Message, error);
        }

    }
}

internal sealed record AppearanceSmokeResult(int Checks, int LanguagePresetCases, int EditorCases);
