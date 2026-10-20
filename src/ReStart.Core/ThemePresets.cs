namespace ReStart.Core;

/// <summary>Stores an opaque RGB surface and its contrasting accent.</summary>
/// <param name="Background">Six-digit RGB background, including the leading #.</param>
/// <param name="Accent">Six-digit RGB accent, including the leading #.</param>
public sealed record ThemePalette(string Background, string Accent);

/// <summary>Defines a color family with independent dark and light palettes.</summary>
/// <param name="Id">Stable family identity stored in local preferences.</param>
/// <param name="Name">Localized display-name key shared by both variants.</param>
/// <param name="Dark">Palette used for dark mode.</param>
/// <param name="Light">Palette used for light mode.</param>
public sealed record ThemePreset(string Id, TextKey Name, ThemePalette Dark, ThemePalette Light)
{
    /// <summary>Resolves the requested base mode without changing preferences.</summary>
    /// <param name="mode">Explicit light/dark mode or the system-following mode.</param>
    /// <param name="systemLight">Current Windows shell light-mode preference; used only for System.</param>
    /// <returns>The light or dark palette for this family.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The mode is not defined.</exception>
    public ThemePalette Palette(ThemeMode mode, bool systemLight)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        return mode == ThemeMode.Light || mode == ThemeMode.System && systemLight ? Light : Dark;
    }
}

/// <summary>Provides paired color themes and resolves them independently of WPF.</summary>
public static class ThemePresets
{
    /// <summary>Gets the five color families, each with dark and light variants.</summary>
    public static IReadOnlyList<ThemePreset> All { get; } = Array.AsReadOnly(new[]
    {
        new ThemePreset("midnight", TextKey.ThemeMidnight, new("#101827", "#6CA8FF"), new("#E9EFF8", "#315F9D")),
        new ThemePreset("ocean", TextKey.ThemeOcean, new("#102D3D", "#35BDE0"), new("#E4F3F8", "#007E9E")),
        new ThemePreset("forest", TextKey.ThemeForest, new("#18342D", "#5CC99B"), new("#E8F3EC", "#287654")),
        new ThemePreset("rose", TextKey.ThemeRose, new("#382332", "#E887A6"), new("#F8EBF0", "#A23D66")),
        new ThemePreset("sand", TextKey.ThemeSand, new("#342C23", "#D6B585"), new("#F2EADF", "#9A5C1D"))
    });

    /// <summary>Checks an opaque six-digit RGB color without accepting named colors or alpha.</summary>
    /// <param name="value">Candidate color, or null.</param>
    /// <returns>True only for # followed by six hexadecimal digits.</returns>
    public static bool IsColor(string? value) => value is { Length: 7 } && value[0] == '#' && value.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;

    /// <summary>Recognizes exact palettes only in unlabelled legacy preferences; explicitly saved custom colors remain custom.</summary>
    /// <param name="preferences">Local preferences; a null family ID marks the legacy format, while an empty ID marks intentional custom RGB.</param>
    /// <returns>The matching legacy family, or null for explicit custom/default colors or an already labelled family.</returns>
    public static ThemePreset? Match(Preferences preferences) => preferences.ColorThemeId == null ? All.FirstOrDefault(p => new[] { p.Dark, p.Light }.Any(c =>
        string.Equals(c.Background, preferences.BackgroundColor, StringComparison.OrdinalIgnoreCase) && string.Equals(c.Accent, preferences.Accent, StringComparison.OrdinalIgnoreCase))) : null;

    /// <summary>Resolves an active family for the current base mode, retaining exact RGB choices for custom colors.</summary>
    /// <param name="preferences">Normalized local preferences. An empty family ID selects the saved RGB colors.</param>
    /// <param name="systemLight">Current Windows shell light-mode preference.</param>
    /// <returns>The active family palette, or the saved background/accent. An empty custom background requests the base surface.</returns>
    public static ThemePalette Resolve(Preferences preferences, bool systemLight) => All.FirstOrDefault(p => p.Id == preferences.ColorThemeId)?.Palette(preferences.Theme, systemLight)
        ?? new(preferences.BackgroundColor, preferences.Accent);

    /// <summary>Selects a family without changing the base mode, layout, or integration preferences.</summary>
    /// <param name="preferences">Mutable preferences receiving the family ID, RGB fallback colors, and disabled classic Start accent.</param>
    /// <param name="id">Stable identity from <see cref="All"/>.</param>
    /// <param name="systemLight">Current Windows shell light-mode preference, used when the base mode is System.</param>
    /// <exception cref="ArgumentException">The family identity is unknown.</exception>
    public static void Apply(Preferences preferences, string id, bool systemLight = false)
    {
        var preset = All.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException("Unknown color theme.", nameof(id));
        var palette = preset.Palette(preferences.Theme, systemLight);
        preferences.ColorThemeId = id; preferences.BackgroundColor = palette.Background; preferences.Accent = palette.Accent;
        preferences.ShowAccentOnStart = false;
    }
}
