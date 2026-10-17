using System.Reflection;
using System.Runtime.InteropServices;
using ReStart.Core;

namespace ReStart.App.Services;

internal static class AppBuildInfo
{
    internal static string Build => typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
    internal static string Version => ReleaseVersion.Parse(Build)?.ToString() ?? "0.0.0";
    internal static string Runtime => RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
    internal static IReadOnlyDictionary<string, string> Licenses()
    {
        var assembly = typeof(App).Assembly;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("ReStart.Licenses.", StringComparison.Ordinal))
            .OrderBy(n => !n.EndsWith("ReStart (MIT)", StringComparison.Ordinal)).ThenBy(n => n, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            result[name["ReStart.Licenses.".Length..]] = reader.ReadToEnd();
        }
        var notices = Path.Combine(AppContext.BaseDirectory, "third-party");
        if (Directory.Exists(notices))
            foreach (var path in Directory.EnumerateFiles(notices, "*.txt").Take(20))
                if (new FileInfo(path).Length <= 2 * 1024 * 1024) result[Path.GetFileName(path)] = File.ReadAllText(path);
        return result;
    }
}
