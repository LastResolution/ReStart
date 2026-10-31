namespace ReStart.Core;

/// <summary>Builds an explicit Windows Search activation without transmitting input while typing or interpreting it as a command.</summary>
public static class WindowsSearchHandoff
{
    /// <summary>Creates a Windows Search URI containing one encoded query.</summary>
    /// <param name="query">Search text trimmed to its meaningful edges. Null, empty input, control characters or more than 128 characters are rejected.</param>
    /// <returns>A fixed-scheme URI, or null for unsupported input. Launching it is a separate explicit user action; Windows controls its online sources and privacy settings.</returns>
    public static Uri? ForQuery(string? query)
    {
        query = query?.Trim() ?? "";
        return query.Length is 0 or > 128 || query.Any(char.IsControl) ? null : new("ms-search:search?query=" + Uri.EscapeDataString(query));
    }
}
