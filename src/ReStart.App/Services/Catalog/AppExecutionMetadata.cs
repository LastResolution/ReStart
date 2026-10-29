using System.Runtime.InteropServices;
using System.Xml;
using Microsoft.Win32;
using ReStart.Core;
using Windows.Management.Deployment;

namespace ReStart.App.Services;

/// <summary>Reads current-user package and App Paths metadata on the catalog's STA; never runs commands or searches arbitrary directories.</summary>
internal sealed class AppExecutionMetadata
{
    private sealed record CachedManifest(string Path, long Stamp, long Length, IReadOnlyDictionary<string, IReadOnlyList<string>> Names);
    private readonly Dictionary<string, CachedManifest> _manifests = new(StringComparer.OrdinalIgnoreCase);

    internal IReadOnlyList<AppEntry> AddPackageAliases(IEnumerable<AppEntry> apps)
    {
        var entries = apps.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
        var families = entries.Values.Where(a => a.IsShellApp && a.Id.Contains('!'))
            .GroupBy(a => a.Id[..a.Id.IndexOf('!')], StringComparer.OrdinalIgnoreCase);
        var retained = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        PackageManager? manager = null;
        foreach (var family in families)
        {
            try
            {
                manager ??= new PackageManager();
                var package = manager.FindPackagesForUser("", family.Key).FirstOrDefault();
                if (package == null) continue;
                var path = Path.Combine(package.InstalledLocation.Path, "AppxManifest.xml");
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > 2_000_000) continue;
                retained.Add(family.Key);
                if (!_manifests.TryGetValue(family.Key, out var cached) || cached.Path != path
                    || cached.Stamp != file.LastWriteTimeUtc.Ticks || cached.Length != file.Length)
                {
                    using var stream = File.OpenRead(path);
                    cached = new(path, file.LastWriteTimeUtc.Ticks, file.Length, AppSearchAliases.ReadPackageManifest(stream));
                    _manifests[family.Key] = cached;
                }
                foreach (var app in family)
                    if (cached.Names.TryGetValue(app.Id[(app.Id.IndexOf('!') + 1)..], out var names))
                        entries[app.Id] = app with { SearchAliases = app.SearchAliases.Concat(names).Distinct(StringComparer.OrdinalIgnoreCase).Take(64).ToArray() };
            }
            catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or System.Security.SecurityException or XmlException or ArgumentException or InvalidOperationException)
            { _manifests.Remove(family.Key); }
        }
        foreach (var family in _manifests.Keys.Where(k => !retained.Contains(k)).ToArray()) _manifests.Remove(family);
        return entries.Values.ToArray();
    }

    internal static IReadOnlyList<AppPathRegistration> ReadAppPaths()
    {
        var result = new List<AppPathRegistration>();
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in Environment.Is64BitOperatingSystem ? new[] { RegistryView.Registry64, RegistryView.Registry32 } : new[] { RegistryView.Registry32 })
                try
                {
                    using var root = RegistryKey.OpenBaseKey(hive, view);
                    using var paths = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths");
                    if (paths != null) result.AddRange(ReadAppPaths(paths));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return result;
    }

    // Accept an already-open key so native fixtures use a disposable ReStart.Tests key.
    internal static IReadOnlyList<AppPathRegistration> ReadAppPaths(RegistryKey paths)
    {
        var result = new List<AppPathRegistration>();
        foreach (var name in paths.GetSubKeyNames().Take(4096))
            try
            {
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                using var key = paths.OpenSubKey(name);
                if (key?.GetValue(null) is not string raw || raw.Length > 32767) continue;
                var target = Environment.ExpandEnvironmentVariables(raw).Trim().Trim('"');
                if (Path.IsPathFullyQualified(target) && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    && !target.Contains('"')) result.Add(new(name, target));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException) { }
        return result;
    }
}
