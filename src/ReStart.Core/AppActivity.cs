namespace ReStart.Core;

/// <summary>Stores bounded, local app discovery and opt-in launch counts, independently of the tile layout.</summary>
public sealed class AppActivity
{
    /// <summary>Gets or sets whether an initial catalog baseline has been observed.</summary>
    public bool Initialized { get; set; }
    /// <summary>Gets or sets per-app observations, keyed by catalog identity. Entries contain no file contents.</summary>
    public Dictionary<string, AppActivityEntry> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Updates discovery dates and removes uninstalled apps without treating the initial catalog as newly installed.</summary>
    /// <param name="apps">Complete current catalog. An empty catalog is ignored to preserve history during enumeration failure.</param>
    /// <param name="now">Current wall-clock instant used for newly discovered apps after initialization.</param>
    /// <returns>Whether the in-memory history changed and should be saved.</returns>
    public bool Observe(IReadOnlyCollection<AppEntry> apps, DateTimeOffset now)
    {
        if (apps.Count == 0) return false;
        var ids = apps.Select(a => a.Id).Take(4096).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changed = false;
        foreach (var id in Entries.Keys.Where(id => !ids.Contains(id)).ToArray()) { Entries.Remove(id); changed = true; }
        foreach (var id in ids)
            if (!Entries.ContainsKey(id)) { Entries[id] = new() { FirstSeen = Initialized ? now : DateTimeOffset.MinValue }; changed = true; }
        Initialized = true;
        return changed;
    }

    /// <summary>Records a successful app launch when usage tracking is enabled.</summary>
    /// <param name="appId">Identity already present in the observed app catalog; arbitrary paths and settings are ignored.</param>
    /// <param name="enabled">Whether the user opted in to usage recording. False leaves all counts unchanged.</param>
    /// <param name="now">Wall-clock instant used to break ties between equally frequent apps.</param>
    public void RecordLaunch(string appId, bool enabled, DateTimeOffset now)
    {
        if (!enabled || !Entries.TryGetValue(appId, out var entry)) return;
        entry.Launches = Math.Min(1_000_000, entry.Launches + 1); entry.LastLaunched = now;
    }

    /// <summary>Returns up to three still-installed apps first discovered during the last seven days.</summary>
    /// <param name="apps">Current catalog used to exclude missing apps and resolve current display names.</param>
    /// <param name="now">Wall-clock instant defining the seven-day visibility window; future observations are excluded.</param>
    /// <returns>A new read-only view of matching catalog entries ordered by discovery date.</returns>
    public IReadOnlyList<AppEntry> Recent(IReadOnlyCollection<AppEntry> apps, DateTimeOffset now) => apps
        .Where(a => Entries.TryGetValue(a.Id, out var e) && e.FirstSeen > now.AddDays(-7) && e.FirstSeen <= now)
        .OrderByDescending(a => Entries[a.Id].FirstSeen).ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).Take(3).ToArray();

    /// <summary>Returns up to six still-installed apps with recorded successful ReStart launches.</summary>
    /// <param name="apps">Current catalog used to exclude missing apps and resolve current display names.</param>
    /// <returns>A new list ordered by descending launch count, latest launch and then current display name.</returns>
    public IReadOnlyList<AppEntry> Frequent(IReadOnlyCollection<AppEntry> apps) => apps
        .Where(a => Entries.TryGetValue(a.Id, out var e) && e.Launches > 0)
        .OrderByDescending(a => Entries[a.Id].Launches).ThenByDescending(a => Entries[a.Id].LastLaunched)
        .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).Take(6).ToArray();

    /// <summary>Clears discovery dates and launch counts while retaining the current baseline so apps are not relabeled as new.</summary>
    public void Clear() { foreach (var entry in Entries.Values) { entry.FirstSeen = DateTimeOffset.MinValue; entry.Launches = 0; entry.LastLaunched = default; } }
}

/// <summary>Stores discovery and opt-in launch metadata for one catalog identity.</summary>
public sealed class AppActivityEntry
{
    /// <summary>Gets or sets discovery time, or MinValue for an initial-baseline or cleared app.</summary>
    public DateTimeOffset FirstSeen { get; set; }
    /// <summary>Gets or sets successful ReStart launches, capped at one million.</summary>
    public int Launches { get; set; }
    /// <summary>Gets or sets the last recorded launch instant, or the default value if none.</summary>
    public DateTimeOffset LastLaunched { get; set; }
}
