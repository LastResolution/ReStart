using System.Globalization;
using System.Text;

namespace ReStart.Core;

/// <summary>Ranks local application names after Unicode-width and kana normalization.</summary>
public static class AppSearch
{
    /// <summary>Returns up to limit matches, prioritizing exact names, name prefixes, word prefixes, then substrings.</summary>
    /// <param name="apps">Flat catalog of installed app identities and display names.</param>
    /// <param name="query">Search text; matching normalizes case, Unicode width, and hiragana/katakana forms.</param>
    /// <param name="limit">Maximum number of ranked apps to return.</param>
    /// <returns>Ranked matching apps, limited to the requested count; a blank query returns the first culture-sorted catalog entries.</returns>
    public static IReadOnlyList<AppEntry> Find(IEnumerable<AppEntry> apps, string query, int limit = 80)
    {
        var words = Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return apps.Select(app => (App: app, Name: Fold(app.Name)))
            .Where(x => words.All(word => x.Name.Contains(word, StringComparison.Ordinal)))
            .OrderBy(x => words.Length == 0 ? 0 : x.Name == string.Join(' ', words) ? 0 : x.Name.StartsWith(words[0], StringComparison.Ordinal) ? 1 :
                words.All(word => x.Name.Split([' ', '-', '.', '_'], StringSplitOptions.RemoveEmptyEntries).Any(part => part.StartsWith(word, StringComparison.Ordinal))) ? 2 : 3)
            .ThenBy(x => x.App.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit).Select(x => x.App).ToList();
    }

    private static string Fold(string text) => string.Concat((text ?? "").Normalize(NormalizationForm.FormKC)
        .ToLower(CultureInfo.InvariantCulture).Select(c => c is >= '\u30A1' and <= '\u30F6' ? (char)(c - 0x60) : c));
}
