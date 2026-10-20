using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class ThemePresetsTests
{
    [Theory]
    [InlineData("#0078D7", true)]
    [InlineData("#abcdef", true)]
    [InlineData("#000000", true)]
    [InlineData("#FFFFFF", true)]
    [InlineData("#FFFFFFFF", false)]
    [InlineData("#123", false)]
    [InlineData("#gg0000", false)]
    [InlineData("Red", false)]
    [InlineData(null, false)]
    public void Accepts_only_opaque_rgb_choices(string? value, bool valid) => Assert.Equal(valid, ThemePresets.IsColor(value));

    [Fact]
    public void Presets_preserve_non_color_preferences()
    {
        Assert.Equal(5, ThemePresets.All.Select(p => p.Id).Distinct().Count());
        foreach (var preset in ThemePresets.All)
            foreach (var mode in Enum.GetValues<ThemeMode>())
            {
                var preferences = new Preferences { Theme = mode, Language = "fr", CaptureWindowsKey = false, StartWithWindows = true, Columns = 8, TileScale = 1.3, ShowWallpaper = false };
                ThemePresets.Apply(preferences, preset.Id, true);
                var palette = preset.Palette(mode, true);
                Assert.Equal(mode, preferences.Theme); Assert.Equal(palette.Background, preferences.BackgroundColor); Assert.Equal(palette.Accent, preferences.Accent);
                Assert.Equal(preset.Id, preferences.ColorThemeId);
                Assert.True(ThemePresets.IsColor(palette.Background) && ThemePresets.IsColor(palette.Accent));
                Assert.Equal("fr", preferences.Language); Assert.False(preferences.CaptureWindowsKey); Assert.True(preferences.StartWithWindows);
                Assert.Equal(8, preferences.Columns); Assert.Equal(1.3, preferences.TileScale); Assert.False(preferences.ShowWallpaper);
            }
        Assert.Throws<ArgumentException>(() => ThemePresets.Apply(new(), "missing"));
    }

    [Fact]
    public void System_family_resolves_both_modes_without_overwriting_saved_rgb_or_identity()
    {
        foreach (var preset in ThemePresets.All)
        {
            var preferences = new Preferences { Theme = ThemeMode.System };
            ThemePresets.Apply(preferences, preset.Id);
            var before = JsonSerializer.Serialize(preferences);
            Assert.Equal(preset.Light, ThemePresets.Resolve(preferences, true));
            Assert.Equal(preset.Dark, ThemePresets.Resolve(preferences, false));
            Assert.NotEqual(preset.Light.Background, preset.Dark.Background);
            Assert.Equal(before, JsonSerializer.Serialize(preferences));
            preferences.Theme = ThemeMode.Light;
            Assert.Equal(preset.Light, ThemePresets.Resolve(preferences, false));
            preferences.Theme = ThemeMode.Dark;
            Assert.Equal(preset.Dark, ThemePresets.Resolve(preferences, true));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => ThemePresets.All[0].Palette((ThemeMode)100, false));
    }

    [Fact]
    public void Unlabelled_legacy_palettes_are_recognized_and_custom_rgb_is_never_inferred_at_runtime()
    {
        foreach (var preset in ThemePresets.All)
        {
            var legacy = new Preferences { BackgroundColor = preset.Dark.Background, Accent = preset.Dark.Accent };
            Assert.Equal(preset, ThemePresets.Match(legacy));
            Assert.Equal(new ThemePalette(legacy.BackgroundColor, legacy.Accent), ThemePresets.Resolve(legacy, true));
        }
        var document = new LayoutDocument { Preferences = new() { ColorThemeId = "ocean", Theme = ThemeMode.Light } };
        var copy = JsonSerializer.Deserialize<LayoutDocument>(JsonSerializer.Serialize(document))!; copy.Normalize();
        Assert.Equal("ocean", copy.Preferences.ColorThemeId);
        Assert.Equal(ThemePresets.All.Single(p => p.Id == "ocean").Light, ThemePresets.Resolve(copy.Preferences, false));
        copy.Preferences.ColorThemeId = "unknown"; copy.Preferences.BackgroundColor = "#123456"; copy.Normalize();
        Assert.Equal("", copy.Preferences.ColorThemeId);
        Assert.Equal("#123456", ThemePresets.Resolve(copy.Preferences, true).Background);
    }

    [Fact]
    public void Explicit_custom_colors_matching_a_palette_stay_custom_after_reopening_in_the_opposite_mode()
    {
        var palette = ThemePresets.All.Single(p => p.Id == "ocean").Dark;
        var document = new LayoutDocument { Preferences = new() { ColorThemeId = "", Theme = ThemeMode.Light, BackgroundColor = palette.Background, Accent = palette.Accent } };
        var copy = JsonSerializer.Deserialize<LayoutDocument>(JsonSerializer.Serialize(document))!; copy.Normalize();
        Assert.Null(ThemePresets.Match(copy.Preferences));
        Assert.Equal(palette, ThemePresets.Resolve(copy.Preferences, true));
        var legacy = JsonSerializer.Deserialize<LayoutDocument>("{\"Preferences\":{\"BackgroundColor\":\"#102D3D\",\"Accent\":\"#35BDE0\"}}")!; legacy.Normalize();
        Assert.Null(legacy.Preferences.ColorThemeId);
        Assert.Equal("ocean", ThemePresets.Match(legacy.Preferences)!.Id);
        legacy.Preferences.Theme = ThemeMode.Light;
        Assert.Null(ThemePresets.Match(legacy.Preferences));
        Assert.Equal(palette, ThemePresets.Resolve(legacy.Preferences, true));
        legacy.Preferences.Theme = ThemeMode.System;
        Assert.Null(ThemePresets.Match(legacy.Preferences));
    }

    [Fact]
    public void Custom_colors_round_trip_and_older_preferences_keep_defaults()
    {
        var document = new LayoutDocument { Preferences = new() { Accent = "#aa33ff", BackgroundColor = "#ddeeff" } };
        var copy = JsonSerializer.Deserialize<LayoutDocument>(JsonSerializer.Serialize(document))!; copy.Normalize();
        Assert.Equal("#AA33FF", copy.Preferences.Accent); Assert.Equal("#DDEEFF", copy.Preferences.BackgroundColor);
        var older = JsonSerializer.Deserialize<LayoutDocument>("{\"Preferences\":{\"Theme\":1}}")!; older.Normalize();
        Assert.Equal("", older.Preferences.BackgroundColor); Assert.Equal("#0078D7", older.Preferences.Accent);
        copy.Preferences.BackgroundColor = "invalid"; copy.Preferences.Accent = "#abcd"; copy.Normalize();
        Assert.Equal("", copy.Preferences.BackgroundColor); Assert.Equal("#0078D7", copy.Preferences.Accent);
    }
}
