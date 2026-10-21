using System.Text;

namespace ReStart.Core;

/// <summary>Contains installed apps chosen for the two initial groups without creating pins or reading user files.</summary>
/// <param name="Everyday">Up to seven original catalog entries, with at most one app per everyday role.</param>
/// <param name="Tools">Up to four original catalog entries, with at most one app per tool role and no duplicates from Everyday.</param>
public sealed record SeedApps(IReadOnlyList<AppEntry> Everyday, IReadOnlyList<AppEntry> Tools);

/// <summary>Selects familiar installed apps by stable identity and exact localized names, without consulting usage history.</summary>
public static class SeedAppSelector
{
    private sealed record Role(string[] Identities, string[] Names);

    private static readonly Role[] EverydayRoles =
    [
        new(["MSEdge", "Chrome", "Firefox"], ["Microsoft Edge", "Google Chrome", "Mozilla Firefox", "Firefox", "Brave", "Vivaldi", "Opera", "Opera GX"]),
        new(["Microsoft.OutlookForWindows"], Localized(K.AppMail, "Outlook", "Outlook (new)", "Microsoft Outlook", "Thunderbird", "Mozilla Thunderbird")),
        new(["Microsoft.Windows.Photos"], Localized(K.AppPhotos, "Microsoft Photos")),
        new(["SpotifyAB.SpotifyMusic", "Microsoft.ZuneMusic"], ["Spotify", "Media Player", "Groove Music", "MusicBee", "foobar2000"]),
        new(["Microsoft.WindowsStore"], ["Microsoft Store", "Store"]),
        new([], ["Microsoft Teams", "Microsoft Teams (work or school)", "Slack", "Discord", "Telegram", "Telegram Desktop", "WhatsApp", "LINE"]),
        new(["Microsoft.ZuneVideo"], ["VLC media player", "VLC", "Movies & TV", "mpv"])
    ];

    private static readonly Role[] ToolRoles =
    [
        new(["Microsoft.WindowsNotepad"], Localized(K.AppNotepad, "Notepad++")),
        new(["Microsoft.WindowsCalculator"], Localized(K.AppCalculator, "Windows Calculator")),
        new([], ["Visual Studio Code", "VSCodium", "Sublime Text"]),
        new(["Microsoft.Paint", "Microsoft.MSPaint"], Localized(K.AppPaint)),
        new(["Microsoft.WindowsTerminal"], ["Windows Terminal", "Terminal"])
    ];

    /// <summary>Chooses a deterministic set of launchable catalog entries, taking one candidate per role and retaining original app identities.</summary>
    /// <param name="apps">Installed app catalog to inspect; entries are not modified and no targets are resolved, read or launched.</param>
    /// <returns>New read-only lists of up to seven everyday apps and four tools, with IDs and launch targets deduplicated case-insensitively.</returns>
    /// <exception cref="ArgumentNullException">The catalog is null.</exception>
    public static SeedApps Select(IReadOnlyList<AppEntry> apps)
    {
        ArgumentNullException.ThrowIfNull(apps);
        var candidates = apps.Where(IsLaunchable).ToArray();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<AppEntry> Choose(IEnumerable<Role> roles, int limit)
        {
            var result = new List<AppEntry>();
            foreach (var role in roles)
            {
                var match = candidates.Where(app => !ids.Contains(app.Id) && !targets.Contains(Identity(app.LaunchTarget)))
                    .Select(app => (App: app, Rank: Rank(app, role)))
                    .Where(item => item.Rank != int.MaxValue)
                    .OrderBy(item => item.Rank)
                    .ThenBy(item => item.App.IsShellApp ? 0 : 1)
                    .ThenBy(item => Name(item.App.Name), StringComparer.Ordinal)
                    .ThenBy(item => item.App.Id, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.App.LaunchTarget, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.App.Id, StringComparer.Ordinal)
                    .ThenBy(item => item.App.LaunchTarget, StringComparer.Ordinal)
                    .ThenBy(item => item.App.Name, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (match.App == null) continue;
                ids.Add(match.App.Id); targets.Add(Identity(match.App.LaunchTarget)); result.Add(match.App);
                if (result.Count == limit) break;
            }
            return result.AsReadOnly();
        }

        return new(Choose(EverydayRoles, 7), Choose(ToolRoles, 4));
    }

    private static bool IsLaunchable(AppEntry app) => !string.IsNullOrWhiteSpace(app.Id)
        && !string.IsNullOrWhiteSpace(app.Name) && !string.IsNullOrWhiteSpace(app.LaunchTarget)
        && (app.IsShellApp || new[] { ".exe", ".lnk", ".appref-ms" }.Any(extension => app.LaunchTarget.EndsWith(extension, StringComparison.OrdinalIgnoreCase)));

    private static int Rank(AppEntry app, Role role)
    {
        for (var i = 0; i < role.Identities.Length; i++)
            if (app.IsShellApp && (MatchesIdentity(app.Id, role.Identities[i]) || MatchesIdentity(app.LaunchTarget, role.Identities[i]))) return i;
        var name = Name(app.Name);
        for (var i = 0; i < role.Names.Length; i++)
            if (name == Name(role.Names[i])) return 100 + i;
        return int.MaxValue;
    }

    private static bool MatchesIdentity(string value, string expected)
    {
        var identity = Identity(value);
        return identity.Equals(expected, StringComparison.OrdinalIgnoreCase)
            || identity.StartsWith(expected + "_", StringComparison.OrdinalIgnoreCase) && identity.Contains('!');
    }

    private static string Identity(string value)
    {
        var identity = value.Replace('/', '\\').Trim();
        const string shellPrefix = "shell:AppsFolder\\";
        return identity.StartsWith(shellPrefix, StringComparison.OrdinalIgnoreCase) ? identity[shellPrefix.Length..] : identity;
    }

    private static string Name(string value) => value.Normalize(NormalizationForm.FormKC).Trim().ToUpperInvariant();
    private static string[] Localized(TextKey key, params string[] names) => names
        .Concat(TextCatalog.SupportedLanguages.Select(language => TextCatalog.Translate(key, language)))
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
