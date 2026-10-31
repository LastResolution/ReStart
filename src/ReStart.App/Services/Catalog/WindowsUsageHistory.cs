using System.Runtime.InteropServices;
using System.Security;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Reads exact current-catalog UserAssist values without enumerating unrelated history, writing registry data or retaining a disk cache.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsUsageHistory
{
    internal sealed record Snapshot(bool Available, IReadOnlyDictionary<string, WindowsAppUsage> Entries);
    private const string Explorer = @"Software\Microsoft\Windows\CurrentVersion\Explorer";
    private static readonly string[] Counts =
    [
        Explorer + @"\UserAssist\{CEBFF5CD-ACE2-4F4F-9178-9926F41749EA}\Count",
        Explorer + @"\UserAssist\{F4E57C4B-2036-45F0-A9AB-443BCFE33D9F}\Count"
    ];
    private static readonly string[] FolderIds =
    [
        "D65231B0-B2F1-4857-A4CE-A8E7C6EA7D27", "1AC14E77-02E7-4E5D-B744-2EB1AE5198B7",
        "F38BF404-1D43-42F2-9305-67DE0B28FC23", "7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E",
        "6D809377-6AF0-444B-8957-A3773F02200E", "A77F5D77-2E2B-44C3-A6A2-ABA601054A51",
        "0139D44E-6AFE-49F2-8690-3DAFCAE6FFB8", "625B53C3-AB48-4EC1-BA1F-A1EF4146FC19",
        "A4115719-D62E-491D-AA7C-E74B8BE3B067"
    ];

    internal static Task<Snapshot> ReadAsync(IReadOnlyCollection<AppEntry> apps, CancellationToken token) => Task.Run(() =>
    {
        using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);
        return Read(user, machine, apps, KnownPrefixes(), DateTimeOffset.UtcNow, token);
    }, token);

    // Injected roots let the native fixture exercise real registry reads only in its owned disposable namespace.
    internal static Snapshot Read(RegistryKey user, RegistryKey machine, IReadOnlyCollection<AppEntry> apps,
        IReadOnlyDictionary<string, string> prefixes, DateTimeOffset now, CancellationToken token = default)
    {
        var result = new Dictionary<string, WindowsAppUsage>(StringComparer.OrdinalIgnoreCase);
        try
        {
            int Value(RegistryKey root, string path, string name, int fallback = 0)
            {
                using var key = root.OpenSubKey(path, false);
                return key?.GetValue(name) is int value ? value : fallback;
            }
            // Missing/unknown tracking state fails closed. Never turn Windows tracking on.
            if (Value(user, Explorer + @"\Advanced", "Start_TrackProgs") != 1
                || new[] { user, machine }.Any(root => Value(root, @"Software\Policies\Microsoft\Windows\EdgeUI", "DisableMFUTracking") != 0
                    || Value(root, @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoInstrumentation") != 0
                    || Value(root, @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoStartMenuMFUprogramsList") != 0)) return new(false, result);
            var deadline = Environment.TickCount64 + 2000;
            foreach (var path in Counts)
            {
                using var count = user.OpenSubKey(path, false);
                if (count == null) continue;
                foreach (var app in apps.Take(4096))
                {
                    token.ThrowIfCancellationRequested();
                    if (Environment.TickCount64 > deadline) return new(false, new Dictionary<string, WindowsAppUsage>());
                    foreach (var name in Candidates(app, prefixes))
                    {
                        var bytes = new byte[72]; uint size = 72;
                        if (RegQueryValueEx(count.Handle, UserAssistFormat.TransformName(name), 0, out var type, bytes, ref size) != 0
                            || type != 3 || size != 72 || UserAssistFormat.Read(bytes, now) is not { } usage) continue;
                        if (!result.TryGetValue(app.Id, out var prior) || usage.Launches > prior.Launches
                            || usage.Launches == prior.Launches && usage.LastLaunched > prior.LastLaunched) result[app.Id] = usage;
                    }
                }
            }
            return new(true, result);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
        { return new(false, new Dictionary<string, WindowsAppUsage>()); }
    }

    internal static IReadOnlyList<string> Candidates(AppEntry app, IReadOnlyDictionary<string, string> prefixes)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in new[] { app.Id, app.LaunchTarget, app.ShortcutPath, app.ExecutablePath })
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 32767 || value.Any(char.IsControl)) continue;
            if (value.Contains('!') && !value.Contains(':') && !value.Contains('\\') && !value.Contains('/')) result.Add(value);
            if (!Path.IsPathFullyQualified(value) || Path.GetExtension(value).ToLowerInvariant() is not (".exe" or ".lnk")) continue;
            result.Add(value);
            foreach (var (guid, prefix) in prefixes)
                if (value.StartsWith(prefix.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                    result.Add("{" + guid.ToUpperInvariant() + "}" + value[prefix.TrimEnd('\\').Length..]);
        }
        return result.Take(48).ToArray();
    }

    private static IReadOnlyDictionary<string, string> KnownPrefixes()
    {
        var paths = new Dictionary<string, string>();
        foreach (var text in FolderIds)
        {
            var guid = Guid.Parse(text); nint path = 0;
            try { if (SHGetKnownFolderPath(ref guid, 0x4000, 0, out path) >= 0 && Marshal.PtrToStringUni(path) is { Length: > 0 } value) paths[text] = value; }
            finally { if (path != 0) Marshal.FreeCoTaskMem(path); }
        }
        return paths;
    }

    [DllImport("advapi32.dll", EntryPoint = "RegQueryValueExW", CharSet = CharSet.Unicode)]
    private static extern int RegQueryValueEx(SafeRegistryHandle key, string name, uint reserved, out uint type, [Out] byte[] data, ref uint size);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, nint token, out nint path);
}
