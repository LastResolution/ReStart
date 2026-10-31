namespace ReStart.Core;

/// <summary>Resolves legacy image references lexically inside the owning app's approved local roots.</summary>
public static class LegacyTileAssetPath
{
    /// <summary>Maps an inert image source to a package asset or app-local file without performing I/O.</summary>
    /// <param name="source">Relative package asset, hostless ms-appx URI or hostless ms-appdata local URI. Null, network URLs, absolute paths and other schemes are rejected.</param>
    /// <param name="packageRoot">Absolute owning package directory; a null, empty or relative root is rejected.</param>
    /// <param name="localRoot">Absolute owning package LocalState directory; needed only for ms-appdata local references. Null rejects those references.</param>
    /// <returns>An absolute candidate path contained in its selected root, or null for unsupported/traversing input. Consumers must still reject reparse points and bound file size and image decoding.</returns>
    public static string? Resolve(string? source, string? packageRoot, string? localRoot)
    {
        if (source == null || source.Length is 0 or > 2048 || source.Any(char.IsControl)) return null;
        var root = packageRoot; var relative = source;
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            if (uri.Host.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0) return null;
            if (uri.Scheme == "ms-appx") relative = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
            else if (uri.Scheme == "ms-appdata" && uri.AbsolutePath.StartsWith("/local/", StringComparison.OrdinalIgnoreCase))
            { root = localRoot; relative = Uri.UnescapeDataString(uri.AbsolutePath[7..]); }
            else return null;
        }
        try
        {
            relative = relative.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (root == null || !Path.IsPathFullyQualified(root) || Path.IsPathRooted(relative) || relative.Contains(':') || relative.IndexOfAny(['*', '?', '"', '<', '>', '|']) >= 0) return null;
            var extension = Path.GetExtension(relative).ToLowerInvariant();
            if (extension is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif")) return null;
            var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(prefix, relative));
            return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? candidate : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }
}
