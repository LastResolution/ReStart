using System.Globalization;
using System.Text.Json;

namespace ReStart.Core;

/// <summary>Resolves typed UI resource keys against complete embedded language catalogs.</summary>
/// <remarks>UI code passes identifiers only. Persisted app names and custom labels are never translated.</remarks>
public static class TextCatalog
{
    /// <summary>Gets the supported BCP-47 language tags, in display order.</summary>
    public static IReadOnlyList<string> SupportedLanguages { get; } = Array.AsReadOnly(new[] { "en", "ja", "de", "fr", "es", "ko", "zh-Hans", "zh-Hant" });
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Catalogs =
        SupportedLanguages.ToDictionary(tag => tag, Load, StringComparer.OrdinalIgnoreCase);
    private static readonly string SystemLanguage = CultureInfo.CurrentUICulture.Name;
    /// <summary>Gets the selected supported BCP-47 language tag.</summary>
    public static string Language { get; private set; } = Resolve("system");
    /// <summary>Gets the culture used by translated messages and date tiles.</summary>
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Language switch { "en" => "en-US", "ja" => "ja-JP", "zh-Hans" => "zh-CN", "zh-Hant" => "zh-TW", _ => Language });
    /// <summary>Gets all supported, compile-time resource identifiers.</summary>
    public static IReadOnlyCollection<TextKey> Keys => Enum.GetValues<TextKey>();
    /// <summary>Resolves explicit or system language preferences, including regional Chinese variants, with an English fallback.</summary>
    /// <param name="language">Explicit language preference, system, or null.</param>
    /// <param name="systemLanguage">Optional system-language override for deterministic callers; null uses the captured Windows UI culture.</param>
    /// <returns>A supported BCP-47 language tag, with English as the fallback.</returns>
    public static string Resolve(string? language, string? systemLanguage = null)
    {
        var explicitTag = SupportedLanguages.FirstOrDefault(tag => tag.Equals(language, StringComparison.OrdinalIgnoreCase));
        if (explicitTag != null) return explicitTag;
        var requested = language is null or "system" ? systemLanguage ?? SystemLanguage : language;
        if (requested.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return requested.Split('-').Any(part => new[] { "Hant", "TW", "HK", "MO" }.Contains(part, StringComparer.OrdinalIgnoreCase)) ? "zh-Hant" : "zh-Hans";
        return SupportedLanguages.FirstOrDefault(tag => requested.Equals(tag, StringComparison.OrdinalIgnoreCase) || requested.StartsWith(tag + "-", StringComparison.OrdinalIgnoreCase)) ?? "en";
    }
    /// <summary>Updates the selected language; callers also refresh already rendered UI.</summary>
    /// <param name="language">Explicit language preference or system; unsupported values resolve to English.</param>
    public static void SetLanguage(string? language) => Language = Resolve(language);
    /// <summary>Gets the selected language's text for a compile-time resource identifier.</summary>
    /// <param name="key">Compile-time resource identifier to retrieve.</param>
    /// <returns>Translated resource text in the currently selected language.</returns>
    public static string T(TextKey key) => Translate(key, Language);
    /// <summary>Formats a translated message using the selected language's culture.</summary>
    /// <exception cref="FormatException">The supplied arguments do not satisfy the resource's format.</exception>
    /// <param name="key">Resource identifier for a composite format string.</param>
    /// <param name="values">Values for the resource placeholders, in their documented order.</param>
    /// <returns>Translated and culture-formatted text.</returns>
    public static string F(TextKey key, params object?[] values) => string.Format(Culture, T(key), values);
    /// <summary>Looks up a key in an explicit language, independently of mutable UI preferences.</summary>
    /// <param name="key">Compile-time resource identifier to retrieve.</param>
    /// <param name="language">Requested BCP-47 language tag; unsupported values resolve to English.</param>
    /// <returns>Resource text in the resolved explicit language, without changing the selected language.</returns>
    public static string Translate(TextKey key, string language) => Catalogs[Resolve(language)][key.ToString()];
    private static IReadOnlyDictionary<string, string> Load(string language)
    {
        using var stream = typeof(TextCatalog).Assembly.GetManifestResourceStream($"ReStart.Core.Locales.{language}.json")!;
        var catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        if (!catalog.Keys.Order().SequenceEqual(Enum.GetNames<TextKey>().Order()))
            throw new InvalidDataException($"The {language} UI catalog does not match TextKey.");
        return catalog;
    }
}
