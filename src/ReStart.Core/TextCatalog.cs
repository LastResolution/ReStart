using System.Globalization;
using System.Text.Json;

namespace ReStart.Core;

public static class TextCatalog
{
    private static readonly IReadOnlyDictionary<string, string> English = Load("en");
    private static readonly IReadOnlyDictionary<string, string> Japanese = Load("ja");
    private static readonly string SystemLanguage = CultureInfo.CurrentUICulture.Name;
    public static string Language { get; private set; } = Resolve("system");
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Language == "ja" ? "ja-JP" : "en-US");
    public static IReadOnlyCollection<string> Keys => English.Keys.ToArray();
    public static string Resolve(string? language, string? systemLanguage = null) => language is "ja" or "en" ? language :
        (systemLanguage ?? SystemLanguage).StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? "ja" : "en";
    public static void SetLanguage(string? language) => Language = Resolve(language);
    public static string T(string source) => Translate(source, Language);
    public static string F(string source, params object?[] values) => string.Format(Culture, T(source), values);
    public static string Translate(string source, string language) => (language == "ja" ? Japanese : English).TryGetValue(source, out var value) ? value : source;
    private static IReadOnlyDictionary<string, string> Load(string language)
    {
        using var stream = typeof(TextCatalog).Assembly.GetManifestResourceStream($"ReStart.Core.Locales.{language}.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}
