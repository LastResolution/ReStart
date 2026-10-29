using System.Xml;
using System.Xml.Linq;

namespace ReStart.Core;

/// <summary>Builds search-only executable names from Windows metadata without resolving or launching commands.</summary>
public static class AppSearchAliases
{
    /// <summary>Adds executable filenames and extensionless names to a detached catalog entry.</summary>
    /// <param name="app">Entry whose identity, display name and launch target are retained; its alias list is never mutated.</param>
    /// <param name="executables">Paths or filenames obtained from OS metadata. Null supplies no names; blank, invalid and non-executable values are ignored. Command arguments must not be supplied.</param>
    /// <param name="secondary">Whether the executable belongs to a shortcut with launch arguments; these names rank below ordinary display-name and argument-free executable matches.</param>
    /// <returns>A copy with at most 64 distinct aliases in the selected category, including existing aliases; no directories or arguments are retained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static AppEntry AddExecutables(AppEntry app, IEnumerable<string>? executables, bool secondary = false)
    {
        ArgumentNullException.ThrowIfNull(app);
        var names = (secondary ? app.SecondarySearchAliases : app.SearchAliases).Concat((executables ?? []).SelectMany(Names))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(64).ToArray();
        return secondary ? app with { SecondarySearchAliases = names } : app with { SearchAliases = names };
    }

    private static IEnumerable<string> Names(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 32767 || value.Contains("://", StringComparison.Ordinal)) yield break;
        var path = value.Trim().Trim('"').Replace('/', '\\');
        var name = path[(path.LastIndexOf('\\') + 1)..];
        if (name.Length is 0 or > 255 || name.Any(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c))) yield break;
        var extension = Path.GetExtension(name);
        if (!new[] { ".exe", ".com", ".cmd", ".bat", ".msc" }.Contains(extension, StringComparer.OrdinalIgnoreCase)) yield break;
        yield return name;
        yield return name[..^extension.Length];
    }

    /// <summary>Reads application-specific executable and execution-alias names from an installed package manifest.</summary>
    /// <param name="manifest">Readable XML stream positioned at its start; remains open and is consumed. DTDs and external resources are prohibited; XML is bounded to two million characters.</param>
    /// <returns>A new application-ID-to-alias map. Each application receives only its own executable and declared execution aliases; no executable is run and no launch target is changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> is null.</exception>
    /// <exception cref="XmlException">XML is malformed, declares a DTD or exceeds the character bound.</exception>
    /// <exception cref="IOException">The input cannot be read.</exception>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadPackageManifest(Stream manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        using var reader = XmlReader.Create(manifest, new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2_000_000, CloseInput = false });
        var document = XDocument.Load(reader);
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var application in document.Root?.Elements().Where(e => e.Name.LocalName == "Applications")
            .SelectMany(e => e.Elements().Where(a => a.Name.LocalName == "Application")) ?? [])
        {
            var id = (string?)application.Attribute("Id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            var executables = new List<string> { (string?)application.Attribute("Executable") ?? "" };
            foreach (var extension in application.Descendants().Where(e => e.Name.LocalName == "Extension"
                && (string?)e.Attribute("Category") == "windows.appExecutionAlias"))
                executables.AddRange(extension.Descendants().Where(e => e.Name.LocalName == "ExecutionAlias")
                    .Select(e => (string?)e.Attribute("Alias") ?? ""));
            result.TryAdd(id, AddExecutables(new(id, "", ""), executables).SearchAliases);
        }
        return result;
    }
}

/// <summary>Describes an App Paths registration without granting it a new catalog entry or changing how an app launches.</summary>
/// <param name="ExecutableName">Registered command filename, such as sample.exe; only a valid executable basename becomes searchable.</param>
/// <param name="TargetPath">Expanded absolute target path used solely to match existing catalog metadata; never searched or launched.</param>
public sealed record AppPathRegistration(string ExecutableName, string TargetPath);
