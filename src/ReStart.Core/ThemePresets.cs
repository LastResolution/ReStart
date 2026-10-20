namespace ReStart.Core;

/// <summary>Defines one optional surface/accent pairing without replacing layout or integration preferences.</summary>
/// <param name="Id">Stable preset identity.</param><param name="Name">Localized display-name key.</param>
/// <param name="Mode">Fallback control mode.</param><param name="Background">Opaque RGB surface color.</param><param name="Accent">Opaque RGB accent color.</param>
public sealed record ThemePreset(string Id, TextKey Name, ThemeMode Mode, string Background, string Accent);

/// <summary>Provides built-in color themes and validates persisted RGB choices independently of WPF.</summary>
public static class ThemePresets
{
    /// <summary>Gets the available dark and light color themes.</summary>
    public static IReadOnlyList<ThemePreset> All { get; } = Array.AsReadOnly(new[]
    {
        new ThemePreset("midnight", TextKey.ThemeMidnight, ThemeMode.Dark, "#101827", "#6CA8FF"),
        new ThemePreset("ocean", TextKey.ThemeOcean, ThemeMode.Dark, "#102D3D", "#35BDE0"),
        new ThemePreset("forest", TextKey.ThemeForest, ThemeMode.Dark, "#18342D", "#5CC99B"),
        new ThemePreset("rose", TextKey.ThemeRose, ThemeMode.Dark, "#382332", "#E887A6"),
        new ThemePreset("sand", TextKey.ThemeSand, ThemeMode.Light, "#F2EADF", "#9A5C1D")
    });
    /// <summary>Gets whether a value is an opaque six-digit RGB color.</summary>
    public static bool IsColor(string? value) => value is { Length: 7 } && value[0] == '#' && value.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;
    /// <summary>Applies only a preset's appearance choices, preserving all other preferences.</summary>
    public static void Apply(Preferences preferences, string id)
    {
        var preset = All.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException("Unknown color theme.", nameof(id));
        preferences.Theme = preset.Mode; preferences.BackgroundColor = preset.Background; preferences.Accent = preset.Accent;
        preferences.ShowAccentOnStart = false;
    }
}
