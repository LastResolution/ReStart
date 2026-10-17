using System.Globalization;
using System.Text.Json;

namespace ReStart.Core;

/// <summary>Resolves typed UI resource keys against embedded Japanese and English catalogs.</summary>
/// <remarks>UI code passes identifiers only. Persisted app names and custom labels are never translated.</remarks>
public static class TextCatalog
{
    private static readonly IReadOnlyDictionary<string, string> English = Load("en");
    private static readonly IReadOnlyDictionary<string, string> Japanese = Load("ja");
    private static readonly string SystemLanguage = CultureInfo.CurrentUICulture.Name;
    /// <summary>Gets the resolved language code: ja or en.</summary>
    public static string Language { get; private set; } = Resolve("system");
    /// <summary>Gets the culture used by translated messages and date tiles.</summary>
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Language == "ja" ? "ja-JP" : "en-US");
    /// <summary>Gets all supported, compile-time resource identifiers.</summary>
    public static IReadOnlyCollection<TextKey> Keys => Enum.GetValues<TextKey>();
    /// <summary>Resolves a preference and system language to ja or en; unsupported languages fall back to English.</summary>
    public static string Resolve(string? language, string? systemLanguage = null) => language is "ja" or "en" ? language :
        (systemLanguage ?? SystemLanguage).StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? "ja" : "en";
    /// <summary>Updates the selected language; callers also refresh already rendered UI.</summary>
    public static void SetLanguage(string? language) => Language = Resolve(language);
    /// <summary>Gets the selected language's text for a compile-time resource identifier.</summary>
    public static string T(TextKey key) => Translate(key, Language);
    /// <summary>Formats a translated message using the selected language's culture.</summary>
    /// <exception cref="FormatException">The supplied arguments do not satisfy the resource's format.</exception>
    public static string F(TextKey key, params object?[] values) => string.Format(Culture, T(key), values);
    /// <summary>Looks up a key in an explicit language, independently of mutable UI preferences.</summary>
    public static string Translate(TextKey key, string language) => (language == "ja" ? Japanese : English)[key.ToString()];
    private static IReadOnlyDictionary<string, string> Load(string language)
    {
        using var stream = typeof(TextCatalog).Assembly.GetManifestResourceStream($"ReStart.Core.Locales.{language}.json")!;
        var catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        if (!catalog.Keys.Order().SequenceEqual(Enum.GetNames<TextKey>().Order()))
            throw new InvalidDataException($"The {language} UI catalog does not match TextKey.");
        return catalog;
    }
}
