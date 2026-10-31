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
        entry.Launches = Math.Clamp(entry.Launches, 0, 999_999) + 1; entry.LastLaunched = now;
    }

    /// <summary>Returns up to three still-installed apps first discovered during the last seven days.</summary>
    /// <param name="apps">Current catalog used to exclude missing apps and resolve current display names.</param>
    /// <param name="now">Wall-clock instant defining the seven-day visibility window; future observations are excluded.</param>
    /// <returns>A new read-only view of matching catalog entries ordered by discovery date.</returns>
    public IReadOnlyList<AppEntry> Recent(IReadOnlyCollection<AppEntry> apps, DateTimeOffset now) => apps
        .Where(a => Entries.TryGetValue(a.Id, out var e) && e.FirstSeen > now.AddDays(-7) && e.FirstSeen <= now)
        .OrderByDescending(a => Entries[a.Id].FirstSeen).ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).Take(3).ToArray();

    /// <summary>Returns up to six still-installed apps with local launches or an explicitly supplied Windows usage snapshot.</summary>
    /// <param name="apps">Current catalog used to exclude missing apps and resolve current display names.</param>
    /// <param name="windows">Optional transient Windows usage keyed by exact catalog identity; null uses local counts only. Observations are never stored or mutated. The larger count is used to avoid double-counting launches recorded by both sources.</param>
    /// <returns>A new list ordered by descending launch count, latest launch and then current display name.</returns>
    public IReadOnlyList<AppEntry> Frequent(IReadOnlyCollection<AppEntry> apps, IReadOnlyDictionary<string, WindowsAppUsage>? windows = null) => apps
        .Select(a =>
        {
            Entries.TryGetValue(a.Id, out var local);
            var native = windows?.GetValueOrDefault(a.Id);
            return (App: a, Hidden: local?.HiddenFromMostUsed == true, Count: Math.Max(local?.Launches ?? 0, native?.Launches ?? 0),
                Last: (local?.LastLaunched ?? default) > (native?.LastLaunched ?? default) ? local!.LastLaunched : native?.LastLaunched ?? default);
        })
        .Where(a => !a.Hidden && a.Count > 0)
        .OrderByDescending(a => a.Count).ThenByDescending(a => a.Last)
        .ThenBy(a => a.App.Name, StringComparer.CurrentCultureIgnoreCase).Take(6).Select(a => a.App).ToArray();

    /// <summary>Removes one observed app from Recently added without changing its launch counts or installed catalog entry.</summary>
    /// <param name="appId">Observed catalog identity, matched without case sensitivity; unknown identities leave history unchanged.</param>
    /// <returns>True when a discovery date was cleared and history should be saved; otherwise false.</returns>
    public bool DismissRecent(string appId)
    {
        if (!Entries.TryGetValue(appId, out var entry) || entry.FirstSeen == DateTimeOffset.MinValue) return false;
        entry.FirstSeen = DateTimeOffset.MinValue; return true;
    }

    /// <summary>Clears all discovery dates while preserving launch counts, exclusions and the observed catalog baseline.</summary>
    /// <returns>True when at least one date was cleared and history should be saved; otherwise false.</returns>
    public bool ClearRecent()
    {
        var changed = false;
        foreach (var entry in Entries.Values)
            if (entry.FirstSeen != DateTimeOffset.MinValue) { entry.FirstSeen = DateTimeOffset.MinValue; changed = true; }
        return changed;
    }

    /// <summary>Excludes one observed app from Most used until it is removed from and rediscovered in the catalog.</summary>
    /// <param name="appId">Observed catalog identity, matched without case sensitivity; unknown identities leave history unchanged.</param>
    /// <returns>True when the exclusion changed and history should be saved; otherwise false.</returns>
    public bool HideFrequent(string appId)
    {
        if (!Entries.TryGetValue(appId, out var entry) || entry.HiddenFromMostUsed) return false;
        entry.HiddenFromMostUsed = true; return true;
    }

    /// <summary>Clears discovery dates and launch counts while retaining exclusions and the baseline so apps are not relabeled as new.</summary>
    public void Clear() { foreach (var entry in Entries.Values) { entry.FirstSeen = DateTimeOffset.MinValue; entry.Launches = 0; entry.LastLaunched = default; } }
}

/// <summary>Stores discovery and opt-in launch metadata for one catalog identity.</summary>
public sealed class AppActivityEntry
{
    /// <summary>Gets or sets whether the user excluded this app from Most used. Launch recording does not reset this choice.</summary>
    public bool HiddenFromMostUsed { get; set; }
    /// <summary>Gets or sets discovery time, or MinValue for an initial-baseline or cleared app.</summary>
    public DateTimeOffset FirstSeen { get; set; }
    /// <summary>Gets or sets successful ReStart launches, capped at one million.</summary>
    public int Launches { get; set; }
    /// <summary>Gets or sets the last recorded launch instant, or the default value if none.</summary>
    public DateTimeOffset LastLaunched { get; set; }
}
