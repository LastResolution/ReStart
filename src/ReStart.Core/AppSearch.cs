using System.Globalization;
using System.Text;

namespace ReStart.Core;

public static class AppSearch
{
    public static IReadOnlyList<AppEntry> Find(IEnumerable<AppEntry> apps, string query, int limit = 80)
    {
        var words = Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return apps.Select(app => (App: app, Name: Fold(app.Name)))
            .Where(x => words.All(word => x.Name.Contains(word, StringComparison.Ordinal)))
            .OrderBy(x => words.Length == 0 ? 0 : x.Name == string.Join(' ', words) ? 0 : x.Name.StartsWith(words[0], StringComparison.Ordinal) ? 1 : 2)
            .ThenBy(x => x.App.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit).Select(x => x.App).ToList();
    }

    private static string Fold(string text) => string.Concat((text ?? "").Normalize(NormalizationForm.FormKC)
        .ToLower(CultureInfo.InvariantCulture).Select(c => c is >= '\u30A1' and <= '\u30F6' ? (char)(c - 0x60) : c));
}
