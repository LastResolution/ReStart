namespace ReStart.Core;

/// <summary>Normalizes the documented/canonical Shell actions used by Start menus without accepting arbitrary extension commands.</summary>
public static class AppMenuCommands
{
    /// <summary>Maps canonical Shell aliases to a stable menu command.</summary>
    /// <param name="verb">Untranslated command name supplied by IContextMenu; null, unknown and destructive file verbs are rejected.</param>
    /// <returns>A lower-case supported command, or null. Both AppsFolder's OpenFileLocation and shortcut opencontaining map to opencontaining.</returns>
    public static string? Normalize(string? verb) => verb?.ToLowerInvariant() switch
    {
        "open" => "open",
        "runas" => "runas",
        "runasuser" => "runasuser",
        "openfilelocation" or "openfolderlocation" or "opencontaining" => "opencontaining",
        "taskbarpin" => "taskbarpin",
        "taskbarunpin" => "taskbarunpin",
        "appsettings" => "appsettings",
        "windows.modernshare" or "share" => "share",
        "uninstall" => "uninstall",
        "properties" => "properties",
        "copyaspath" => "copyaspath",
        _ => null
    };
}
