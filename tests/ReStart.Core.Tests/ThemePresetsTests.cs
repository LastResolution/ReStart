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
        {
            var preferences = new Preferences { Language = "fr", CaptureWindowsKey = false, StartWithWindows = true, Columns = 8, TileScale = 1.3, ShowWallpaper = false };
            ThemePresets.Apply(preferences, preset.Id);
            Assert.Equal(preset.Mode, preferences.Theme); Assert.Equal(preset.Background, preferences.BackgroundColor); Assert.Equal(preset.Accent, preferences.Accent);
            Assert.True(ThemePresets.IsColor(preset.Background) && ThemePresets.IsColor(preset.Accent));
            Assert.Equal("fr", preferences.Language); Assert.False(preferences.CaptureWindowsKey); Assert.True(preferences.StartWithWindows);
            Assert.Equal(8, preferences.Columns); Assert.Equal(1.3, preferences.TileScale); Assert.False(preferences.ShowWallpaper);
        }
        Assert.Throws<ArgumentException>(() => ThemePresets.Apply(new(), "missing"));
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
