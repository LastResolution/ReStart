namespace ReStart.Core;

/// <summary>Builds explicit browser searches without contacting a service or interpreting the query as a command.</summary>
public static class WebSearchQuery
{
    /// <summary>Builds a Bing HTTPS search URL for an explicit user action.</summary>
    /// <param name="query">Untrusted search text, trimmed at the ends. Null, blank, control characters, malformed Unicode and more than 2048 UTF-16 code units are rejected.</param>
    /// <returns>A new absolute URL containing one escaped query parameter, or null for rejected input. No network request, clipboard operation or history write occurs.</returns>
    public static Uri? Build(string? query)
    {
        var text = query?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 2048 || query!.Any(char.IsControl)) return null;
        for (var i = 0; i < text.Length; i++)
            if (char.IsSurrogate(text[i]) && (!char.IsHighSurrogate(text[i]) || ++i >= text.Length || !char.IsLowSurrogate(text[i]))) return null;
        return new("https://www.bing.com/search?q=" + Uri.EscapeDataString(text));
    }
}
