namespace ReStart.Core;

/// <summary>Identifies an explicitly addressed file for sharing, without substituting an application's shortcut or executable.</summary>
public static class AppShareTarget
{
    /// <summary>Finds the existing file represented by a file pin or file search result.</summary>
    /// <param name="app">Item to share; null, Shell application identities, directories and shortcut files are unsupported.</param>
    /// <returns>The existing absolute file path, or null. A catalog's source shortcut is never followed or shared on behalf of its application. No extension allowlist is applied to ordinary files.</returns>
    public static string? Find(AppEntry? app)
    {
        if (app == null || app.IsShellApp || app.ShortcutPath.Length != 0
            || AppFileLocation.Find(app) is not { IsDirectory: false } location) return null;
        var extension = Path.GetExtension(location.Path);
        return extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || extension.Equals(".url", StringComparison.OrdinalIgnoreCase)
            ? null : location.Path;
    }
}
