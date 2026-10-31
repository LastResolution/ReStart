namespace ReStart.Core;

/// <summary>Builds bounded, local-only Windows Search queries without accepting SQL or full-text query operators.</summary>
public static class IndexedFileQuery
{
    /// <summary>Gets the maximum number of local indexed results returned by one query.</summary>
    public const int MaximumResults = 50;
    /// <summary>Creates a read-only SystemIndex query without interpreting input as SQL or wildcard syntax.</summary>
    /// <param name="query">Literal filename fragment; whitespace-only input or more than 128 characters returns null.</param>
    /// <param name="includeContents">When true, also passes plain search text to Windows' full-text word breaker. Defaults to false; never reads unindexed file contents or enables indexing.</param>
    /// <returns>A query limited to fifty indexed filesystem entries, or null for unsupported input. No disk scan is performed.</returns>
    public static string? Build(string query, bool includeContents = false)
    {
        query = query.Trim();
        if (query.Length is < 2 or > 128 || query.Any(char.IsControl)) return null;
        var literal = string.Concat(query.Select(c => c switch { '\'' => "''", '%' => "[%]", '_' => "[_]", '[' => "[[]", ']' => "[]]", _ => c.ToString() }));
        var filename = $"System.FileName LIKE '%{literal}%'";
        var predicate = includeContents ? $"({filename} OR FREETEXT(System.Search.Contents, '{query.Replace("'", "''")}'))" : filename;
        return $"SELECT TOP {MaximumResults} System.ItemUrl, System.ItemNameDisplay FROM SystemIndex WHERE System.ItemUrl LIKE 'file:%' "
            + $"AND {predicate} ORDER BY System.DateModified DESC";
    }
}
