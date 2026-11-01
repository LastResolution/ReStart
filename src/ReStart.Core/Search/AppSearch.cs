using System.Globalization;
using System.Text;

namespace ReStart.Core;

/// <summary>Ranks local application names and OS-supplied executable aliases after Unicode-width and kana normalization.</summary>
public static class AppSearch
{
    /// <summary>Returns up to limit matches, prioritizing exact display names and aliases before prefix, word-prefix and substring matches.</summary>
    /// <param name="apps">Flat catalog of installed app identities, display names and search-only executable aliases; entries and alias lists are not mutated.</param>
    /// <param name="query">Search text; matching normalizes case, Unicode width, and hiragana/katakana forms.</param>
    /// <param name="limit">Maximum number of ranked apps to return.</param>
    /// <param name="usage">Optional opt-in app usage snapshot; only equally strong text matches are ordered by descending usage and last launch. Null preserves name-only tie breaking. Neither observations nor apps are mutated.</param>
    /// <param name="names">Optional name comparer for final ties, such as Windows NLS collation. Null uses the current culture's case-insensitive comparer.</param>
    /// <returns>Ranked matching apps, limited to the requested count; a blank query returns name-sorted entries without usage weighting.</returns>
    public static IReadOnlyList<AppEntry> Find(IEnumerable<AppEntry> apps, string query, int limit = 80,
        IReadOnlyDictionary<string, WindowsAppUsage>? usage = null, IComparer<string>? names = null)
    {
        var words = Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return apps.Select(app => (App: app, Rank: Rank(app, words)))
            .Where(x => x.Rank < int.MaxValue)
            .OrderBy(x => x.Rank)
            .ThenByDescending(x => words.Length == 0 ? 0 : Math.Clamp(usage?.GetValueOrDefault(x.App.Id)?.Launches ?? 0, 0, 1_000_000))
            .ThenByDescending(x => words.Length == 0 ? default : usage?.GetValueOrDefault(x.App.Id)?.LastLaunched ?? default)
            .ThenBy(x => x.App.Name, names ?? StringComparer.CurrentCultureIgnoreCase)
            .Take(limit).Select(x => x.App).ToList();
    }

    private static int Rank(AppEntry app, string[] words)
    {
        if (words.Length == 0) return 0;
        var name = Fold(app.Name);
        var aliases = app.SearchAliases.Select(Fold).Where(a => a.Length > 0).ToArray();
        var secondary = app.SecondarySearchAliases.Select(Fold).Where(a => a.Length > 0).ToArray();
        var query = string.Join(' ', words);
        if (name == query) return 0;
        if (aliases.Contains(query, StringComparer.Ordinal)) return 1;
        bool Contains(string text, string word) => text.Contains(word, StringComparison.Ordinal);
        if (words.All(word => Contains(name, word)))
        {
            if (name.StartsWith(words[0], StringComparison.Ordinal)) return 2;
            if (words.All(word => name.Split([' ', '-', '.', '_'], StringSplitOptions.RemoveEmptyEntries).Any(part => part.StartsWith(word, StringComparison.Ordinal)))) return 3;
            return 5;
        }
        if (words.All(word => Contains(name, word) || aliases.Any(a => Contains(a, word))))
            return aliases.Any(a => a.StartsWith(words[0], StringComparison.Ordinal)) ? 4 : 6;
        if (!words.All(word => Contains(name, word) || aliases.Concat(secondary).Any(a => Contains(a, word)))) return int.MaxValue;
        if (secondary.Contains(query, StringComparer.Ordinal)) return 7;
        return secondary.Any(a => a.StartsWith(words[0], StringComparison.Ordinal)) ? 8 : 9;
    }

    private static string Fold(string text) => string.Concat((text ?? "").Normalize(NormalizationForm.FormKC)
        .ToLower(CultureInfo.InvariantCulture).Select(c => c is >= '\u30A1' and <= '\u30F6' ? (char)(c - 0x60) : c));
}
