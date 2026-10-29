namespace ReStart.Core;

/// <summary>Finds the filesystem item to select when Start opens an item's containing folder.</summary>
/// <param name="Path">Existing absolute item path, never a command string. A matched Start-menu shortcut takes precedence over an executable target.</param>
/// <param name="IsDirectory">Whether the selected item itself is a directory, used for the translated folder-location label.</param>
public sealed record AppFileLocation(string Path, bool IsDirectory)
{
    /// <summary>Returns a local location without resolving shortcuts or exposing a packaged app's installation directory.</summary>
    /// <param name="app">Catalog item; null has no location. Shell identities and protocols are not interpreted as filesystem paths.</param>
    /// <returns>A new location for the existing matched shortcut or local target; null for unavailable, relative, quoted or control-containing paths.</returns>
    public static AppFileLocation? Find(AppEntry? app)
    {
        if (app == null) return null;
        var path = app.ShortcutPath.Length > 0 ? app.ShortcutPath : app.IsShellApp ? "" : app.LaunchTarget;
        if (string.IsNullOrWhiteSpace(path) || path.Length > 32767 || path.Any(c => char.IsControl(c) || c == '"')
            || !System.IO.Path.IsPathFullyQualified(path)) return null;
        try
        {
            var full = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));
            if (Directory.Exists(full)) return new(full, true);
            return File.Exists(full) ? new(full, false) : null;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException) { return null; }
    }
}
