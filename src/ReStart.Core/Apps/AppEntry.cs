namespace ReStart.Core;

/// <summary>Describes an installed application or shortcut without modifying its OS-provided name.</summary>
/// <param name="Id">Stable AppsFolder or shortcut identity used by pinned tiles and icons.</param>
/// <param name="Name">OS-provided application display name.</param>
/// <param name="LaunchTarget">Shell identity, protocol or shortcut path used for launching.</param>
/// <param name="IsShellApp">Whether the target is resolved through the AppsFolder namespace.</param>
/// <param name="FolderPath">OS-provided relative Start-menu display folders, separated by slashes; empty for root apps.</param>
public sealed record AppEntry(string Id, string Name, string LaunchTarget, bool IsShellApp = true, string FolderPath = "")
{
    /// <summary>Gets whether Windows shortcut metadata excludes this entry from new-install highlighting. False when not supplied.</summary>
    public bool ExcludeFromRecentlyAdded { get; init; }

    /// <summary>Gets whether Windows shortcut metadata opts this entry out of the Start most-used list. Search and explicit launch remain available.</summary>
    public bool ExcludeFromMostUsed { get; init; }

    /// <summary>Gets the matched physical Start-menu shortcut used only for location/properties commands; empty when unavailable. It never replaces the launch identity or becomes a search alias.</summary>
    public string ShortcutPath { get; init; } = "";

    /// <summary>Gets the OS-resolved executable path for an argument-free catalog entry, or empty when unavailable. Used only for exact Windows usage matching, never as a search alias or a replacement launch target.</summary>
    public string ExecutablePath { get; init; } = "";

    /// <summary>Gets additional executable names supplied by Windows catalog metadata, never full paths or command arguments; empty by default. Producers own the list and must not mutate it after publishing the entry.</summary>
    public IReadOnlyList<string> SearchAliases { get; init; } = [];

    /// <summary>Gets lower-priority executable names for shortcuts with launch arguments, such as developer prompts sharing cmd.exe; empty by default. Producers own the list and must not mutate it after publication.</summary>
    public IReadOnlyList<string> SecondarySearchAliases { get; init; } = [];
}
