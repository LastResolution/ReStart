namespace ReStart.Core;

/// <summary>Builds bounded, local-only Windows Search queries from literal filename text.</summary>
public static class IndexedFileQuery
{
    /// <summary>Creates a read-only SystemIndex query without interpreting input as SQL or wildcard syntax.</summary>
    /// <param name="query">Literal filename fragment; whitespace-only input or more than 128 characters returns null.</param>
    /// <returns>A query limited to twenty indexed filesystem entries, or null for unsupported input. No disk scan is performed.</returns>
    public static string? Build(string query)
    {
        query = query.Trim();
        if (query.Length is < 2 or > 128 || query.Any(char.IsControl)) return null;
        var literal = string.Concat(query.Select(c => c switch { '\'' => "''", '%' => "[%]", '_' => "[_]", '[' => "[[]", ']' => "[]]", _ => c.ToString() }));
        return "SELECT TOP 20 System.ItemUrl, System.ItemNameDisplay FROM SystemIndex WHERE System.ItemUrl LIKE 'file:%' "
            + $"AND System.FileName LIKE '%{literal}%' ORDER BY System.DateModified DESC";
    }
}
