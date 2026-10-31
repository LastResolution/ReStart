namespace ReStart.Core;

/// <summary>Describes a physical Start-menu shortcut, its precedence key and optional shell identity metadata.</summary>
/// <param name="RelativeKey">Path relative to a user or common Start-menu root; user-first duplicates share this key.</param>
/// <param name="App">Shortcut launch entry and its physical folder path.</param>
/// <param name="AppId">Optional Shell app identity used for exact matching.</param>
/// <param name="TargetPath">Optional resolved shortcut target used for unambiguous matching.</param>
/// <param name="Arguments">Shortcut arguments; distinct argument strings remain distinct launch identities.</param>
public sealed record StartMenuShortcut(string RelativeKey, AppEntry App, string AppId = "", string TargetPath = "", string Arguments = "");

/// <summary>Describes the target and arguments exposed by an AppsFolder item.</summary>
/// <param name="AppId">AppsFolder identity owning this metadata.</param>
/// <param name="TargetPath">Resolved filesystem launch target, or empty when unavailable.</param>
/// <param name="Arguments">Launch arguments used to distinguish links with a shared target.</param>
public sealed record ShellAppLink(string AppId, string TargetPath, string Arguments = "");

/// <summary>Attaches Start-menu folders while retaining existing AppsFolder identities and launch targets.</summary>
public static class AppCatalogLayout
{
    /// <summary>Merges user-first shortcuts by app ID, unambiguous link identity, then unambiguous display name.</summary>
    /// <param name="shellApps">AppsFolder entries with unique app IDs.</param>
    /// <param name="shortcuts">Physical shortcuts ordered with per-user entries before common entries.</param>
    /// <param name="shellLinks">Optional target/argument metadata for Shell entries; null supplies no link matching.</param>
    /// <param name="appPaths">Optional OS App Paths registrations; null supplies none. Names are attached only to catalog entries with matching executable targets; registrations never create entries.</param>
    /// <returns>A culture-sorted merged catalog, retaining Shell launch identities when a unique match is found.</returns>
    public static IReadOnlyList<AppEntry> Merge(IEnumerable<AppEntry> shellApps, IEnumerable<StartMenuShortcut> shortcuts,
        IEnumerable<ShellAppLink>? shellLinks = null, IEnumerable<AppPathRegistration>? appPaths = null)
    {
        var entries = shellApps.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
        var relativeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var links = shortcuts.Where(s => relativeKeys.Add(s.RelativeKey.Replace('/', '\\'))).ToArray();
        var metadata = (shellLinks ?? []).Where(s => entries.ContainsKey(s.AppId)).DistinctBy(s => s.AppId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(s => s.AppId, StringComparer.OrdinalIgnoreCase);
        var registered = (appPaths ?? []).GroupBy(p => p.TargetPath.Replace('/', '\\'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(p => p.ExecutableName).ToArray(), StringComparer.OrdinalIgnoreCase);
        AppEntry WithTarget(AppEntry app, string target, string arguments) => AppSearchAliases.AddExecutables(app with
        { ExecutablePath = string.IsNullOrWhiteSpace(arguments) ? target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? target : app.ExecutablePath : "" },
            new[] { target }.Concat(registered.GetValueOrDefault(target.Replace('/', '\\')) ?? []), !string.IsNullOrWhiteSpace(arguments));
        foreach (var target in metadata.Values) entries[target.AppId] = WithTarget(entries[target.AppId], target.TargetPath, target.Arguments);
        var byTarget = metadata.Values.Where(s => !string.IsNullOrWhiteSpace(s.TargetPath))
            .GroupBy(s => new LinkKey(s.TargetPath, s.Arguments), LinkComparer.Instance)
            .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single().AppId, LinkComparer.Instance);
        var byName = entries.Values.GroupBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single(), StringComparer.CurrentCultureIgnoreCase);
        var uniqueNames = links.GroupBy(s => s.App.Name, StringComparer.CurrentCultureIgnoreCase)
            .Where(g => g.Count() == 1).Select(g => g.Key).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assignments = new string?[links.Length];

        // Strong identities take precedence over names, including shortcuts that
        // occur later in the filesystem's enumeration order.
        for (var i = 0; i < links.Length; i++)
            if (entries.ContainsKey(links[i].AppId) && matched.Add(links[i].AppId)) assignments[i] = links[i].AppId;
        for (var i = 0; i < links.Length; i++)
        {
            var link = links[i];
            if (assignments[i] != null || link.AppId.Length != 0 || string.IsNullOrWhiteSpace(link.TargetPath)) continue;
            if (byTarget.TryGetValue(new(link.TargetPath, link.Arguments), out var id) && matched.Add(id)) assignments[i] = id;
        }
        for (var i = 0; i < links.Length; i++)
        {
            var link = links[i];
            if (assignments[i] != null || link.AppId.Length != 0 || !uniqueNames.Contains(link.App.Name)
                || !byName.TryGetValue(link.App.Name, out var app) || matched.Contains(app.Id)) continue;
            if (metadata.TryGetValue(app.Id, out var target) && target.TargetPath.Length != 0 && link.TargetPath.Length != 0
                && !LinkComparer.Instance.Equals(new(target.TargetPath, target.Arguments), new(link.TargetPath, link.Arguments))) continue;
            matched.Add(app.Id);
            assignments[i] = app.Id;
        }
        for (var i = 0; i < links.Length; i++)
        {
            var link = links[i];
            if (assignments[i] is { } id) entries[id] = WithTarget(entries[id] with { FolderPath = link.App.FolderPath, ShortcutPath = link.App.LaunchTarget }, link.TargetPath, link.Arguments);
            else entries.TryAdd(link.App.Id, WithTarget(link.App, link.TargetPath, link.Arguments));
        }
        return entries.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private sealed record LinkKey(string Target, string Arguments);
    private sealed class LinkComparer : IEqualityComparer<LinkKey>
    {
        internal static readonly LinkComparer Instance = new();
        public bool Equals(LinkKey? x, LinkKey? y) => x != null && y != null
            && StringComparer.OrdinalIgnoreCase.Equals(x.Target, y.Target) && StringComparer.Ordinal.Equals(x.Arguments, y.Arguments);
        public int GetHashCode(LinkKey value) => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(value.Target), StringComparer.Ordinal.GetHashCode(value.Arguments));
    }
}
