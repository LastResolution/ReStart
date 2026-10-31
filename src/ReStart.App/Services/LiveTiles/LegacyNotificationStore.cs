using System.Runtime.Versioning;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Reads only live tile/badge rows for explicitly requested pinned package identities. Toasts, deleted records and other apps are never selected.</summary>
[SupportedOSPlatform("windows")]
internal static class LegacyNotificationStore
{
    internal sealed record Frame(IReadOnlyList<LegacyTileBinding> Bindings, string Badge);
    internal sealed record Snapshot(bool Available, IReadOnlyDictionary<string, Frame> Apps);
    private const string Query = "SELECT n.Type,n.Payload,n.Id FROM Notification AS n JOIN NotificationHandler AS h ON n.HandlerId=h.RecordId "
        + "WHERE h.PrimaryId=?1 COLLATE NOCASE AND n.Type COLLATE NOCASE IN ('tile','badge') AND n.PayloadType='Xml' COLLATE NOCASE "
        + "AND (n.ExpiryTime IS NULL OR n.ExpiryTime=0 OR n.ExpiryTime>?2) AND n.ArrivalTime<=?2 "
        + "AND (COALESCE(n.ExpiresOnReboot,'FALSE') IN (0,'FALSE','false') OR n.ArrivalTime>=?3) "
        + "AND LENGTH(n.Payload)<=65536 ORDER BY n.ArrivalTime DESC,n.\"Order\" DESC LIMIT 16";

    internal static Task<Snapshot> ReadAsync(IReadOnlyCollection<string> appIds, CancellationToken token) => Task.Run(() =>
        Read(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Notifications", "wpndatabase.db"),
            appIds, DateTimeOffset.UtcNow, token), token);

    internal static bool IsPackageAppId(string value) => value.Length <= 512 && System.Text.RegularExpressions.Regex.IsMatch(value,
        @"^[A-Za-z0-9][A-Za-z0-9._-]{1,255}_[A-Za-z0-9]{1,64}![A-Za-z0-9][A-Za-z0-9._-]{0,127}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    // The path injection is used only by owned native fixtures. Production always uses the current user's fixed OS location.
    internal static Snapshot Read(string path, IReadOnlyCollection<string> appIds, DateTimeOffset now, CancellationToken token = default)
    {
        var result = new Dictionary<string, Frame>(StringComparer.OrdinalIgnoreCase);
        var requested = appIds.Where(IsPackageAppId)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(32).ToArray();
        if (requested.Length == 0) return new(true, result);
        try
        {
            using var database = new ReadOnlySqlite(path, token);
            var boot = now.AddMilliseconds(-Environment.TickCount64).ToFileTime();
            foreach (var app in requested)
            {
                var bindings = new List<LegacyTileBinding>(); var badge = ""; var hasBadge = false;
                database.Query(Query, app, now.ToFileTime(), boot, row =>
                {
                    var type = row.Text(0, 16).ToLowerInvariant(); var payload = row.Text(1);
                    if (type == "tile")
                        foreach (var binding in LegacyTileContent.Read(payload, payload.Contains('{') ? Values(database, row.Integer(2), now.ToFileTime(), boot) : null))
                            if (bindings.All(b => b.Size != binding.Size)) bindings.Add(binding);
                    if (type == "badge" && !hasBadge) { badge = LegacyTileContent.ReadBadge(payload); hasBadge = true; }
                });
                if (bindings.Count != 0 || badge.Length > 0) result[app] = new(bindings, badge);
            }
            return new(true, result);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException
            or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { return new(false, new Dictionary<string, Frame>()); }
    }

    private static IReadOnlyDictionary<string, string> Values(ReadOnlySqlite database, long notification, long now, long boot)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            database.Query("SELECT Key,Value FROM NotificationData WHERE NotificationId=?1 AND ?2>0 AND ?3>0 LIMIT 64",
                notification.ToString(System.Globalization.CultureInfo.InvariantCulture), now, boot, row =>
                {
                    var key = row.Text(0, 128); var value = row.Text(1, 8192);
                    if (key.Length > 0 && !key.Any(char.IsControl)) values.TryAdd(key, value[..Math.Min(value.Length, 2048)]);
                }, 64);
        }
        catch (IOException) { }
        return values;
    }
}
