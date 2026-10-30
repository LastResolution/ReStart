using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Globalization.Collation;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Uses Windows language collation for All apps headings, ordering and alphabet navigation.</summary>
internal sealed class WindowsAppIndex
{
    private static readonly Dictionary<string, WindowsAppIndex> Cache = new(StringComparer.Ordinal);
    private readonly CharacterGroupings _groups;
    private readonly (string Label, string First)[] _alphabet;
    private readonly Dictionary<string, string> _labels = new(StringComparer.Ordinal);
    internal IComparer<string> Names { get; }

    private WindowsAppIndex(string language)
    {
        _groups = new(language);
        _alphabet = _groups.Where(g => g.Label.Length != 0).Select(g => (g.Label, g.First)).ToArray();
        var locale = CultureInfo.GetCultureInfo(language switch
        { "en" => "en-US", "ja" => "ja-JP", "de" => "de-DE", "fr" => "fr-FR", "es" => "es-ES", "ko" => "ko-KR", "zh-Hans" => "zh-CN", "zh-Hant" => "zh-TW", _ => language }).Name;
        Names = Comparer<string>.Create((left, right) =>
        {
            if (left == right) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            // Match the Windows NLS collation behind CharacterGroupings instead
            // of mixing its labels with .NET's potentially ICU-based order.
            var result = CompareStringEx(locale, 1, left, left.Length, right, right.Length, 0, 0, 0);
            return result == 0 ? StringComparer.OrdinalIgnoreCase.Compare(left, right) : result - 2;
        });
    }

    // The cache and WinRT instances are used only on the WPF dispatcher.
    internal static WindowsAppIndex ForLanguage(string language)
    {
        var resolved = L.Resolve(language);
        if (!Cache.TryGetValue(resolved, out var index)) Cache[resolved] = index = new(resolved);
        return index;
    }

    internal string Section(string name)
    {
        if (_labels.TryGetValue(name, out var label)) return label;
        var text = name.TrimStart();
        // Lookup can return an unindexed symbol (including an emoji) unchanged.
        // Keep those with Windows' symbol group instead of creating emoji keys.
        label = text.Length == 0 || !Rune.TryGetRuneAt(text, 0, out _) ? "&" : _groups.Lookup(text);
        // Judge the returned label, not the input's Unicode category. Windows
        // indexes circled/superscript/Roman numerals under # and enclosed Latin
        // letters under A-Z even though those inputs are symbols or numbers.
        if (label.Length == 0 || label is not ("&" or "#") && (!Rune.TryGetRuneAt(label, 0, out var initial) || !Rune.IsLetterOrDigit(initial))) label = "&";
        _labels[name] = label;
        return label;
    }

    internal string[] Alphabet(IEnumerable<string> names)
    {
        var sections = _alphabet.ToDictionary(g => g.Label, g => g.First, StringComparer.Ordinal);
        foreach (var name in names)
        {
            var label = Section(name);
            // Windows can return labels for scripts outside the selected
            // language's built-in alphabet. Include populated foreign groups.
            sections.TryAdd(label, name.TrimStart());
        }
        return sections.OrderBy(s => s.Key == "&" ? 0 : s.Key == "#" ? 1 : 2)
            .ThenBy(s => s.Value, Names).ThenBy(s => s.Key, StringComparer.Ordinal).Select(s => s.Key).ToArray();
    }

    internal bool IsInitial(string text) => text.Length != 0 && StringInfo.ParseCombiningCharacters(text).Length == 1
        && Rune.TryGetRuneAt(text, 0, out _) && Section(text) != "&";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int CompareStringEx(string locale, uint flags, string left, int leftLength, string right, int rightLength,
        nint version, nint reserved, nint parameter);
}
