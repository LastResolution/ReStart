namespace ReStart.Core;

/// <summary>Describes an installed application or shortcut without modifying its OS-provided name.</summary>
/// <param name="Id">Stable AppsFolder or shortcut identity used by pinned tiles and icons.</param>
/// <param name="Name">OS-provided application display name.</param>
/// <param name="LaunchTarget">Shell identity, protocol or shortcut path used for launching.</param>
/// <param name="IsShellApp">Whether the target is resolved through the AppsFolder namespace.</param>
/// <param name="FolderPath">OS-provided relative Start-menu display folders, separated by slashes; empty for root apps.</param>
public sealed record AppEntry(string Id, string Name, string LaunchTarget, bool IsShellApp = true, string FolderPath = "")
{
    /// <summary>Gets additional executable names supplied by Windows catalog metadata, never full paths or command arguments; empty by default. Producers own the list and must not mutate it after publishing the entry.</summary>
    public IReadOnlyList<string> SearchAliases { get; init; } = [];

    /// <summary>Gets lower-priority executable names for shortcuts with launch arguments, such as developer prompts sharing cmd.exe; empty by default. Producers own the list and must not mutate it after publication.</summary>
    public IReadOnlyList<string> SecondarySearchAliases { get; init; } = [];

    /// <summary>Gets the display bucket used for alphabet navigation.</summary>
    public string Section => SectionFor(Name);
    /// <summary>Returns the shared alphabet bucket for an app or folder display name.</summary>
    /// <param name="name">OS-provided app or directory display name used for grouping.</param>
    /// <returns>Uppercase ASCII letter, # for blank/digit names, or the localized other section.</returns>
    public static string SectionFor(string name) => string.IsNullOrWhiteSpace(name) ? "#" :
        char.IsAsciiLetter(name[0]) ? char.ToUpperInvariant(name[0]).ToString() :
        char.IsDigit(name[0]) ? "#" : L.T(K.AppListOther);
}
