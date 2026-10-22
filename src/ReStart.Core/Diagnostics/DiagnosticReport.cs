using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReStart.Core;

/// <summary>Lists safe error categories; exception messages, URLs and stack traces are never recorded.</summary>
public enum DiagnosticCode
{
    /// <summary>No recent classified failure.</summary>
    None,
    /// <summary>Package download, validation or staging failed.</summary>
    UpdatePreparationFailed,
    /// <summary>The installation could not be updated and was restored.</summary>
    UpdateRolledBack,
    /// <summary>Automatic recovery could not finish and needs manual repair.</summary>
    UpdateRecoveryFailed,
    /// <summary>The dispatcher received an unhandled exception.</summary>
    UnhandledError,
    /// <summary>The public release check failed.</summary>
    UpdateCheckFailed
}

/// <summary>Builds a sharing report from an explicit whitelist, without accepting arbitrary logs or machine identifiers.</summary>
public static class DiagnosticReport
{
    /// <summary>Serializes a bounded report containing versions, presentation mode and classified outcomes only.</summary>
    /// <param name="appVersion">App informational version; invalid values become unknown and build metadata is discarded.</param>
    /// <param name="runtime">win-x64 or win-arm64; unsupported values become unknown.</param>
    /// <param name="windowsVersion">Windows numeric version; null becomes unknown.</param>
    /// <param name="dotnetVersion">Runtime numeric version; null becomes unknown.</param>
    /// <param name="webViewVersion">WebView2 version; only a numeric version prefix is retained.</param>
    /// <param name="language">Supported UI language tag; all other strings become unknown.</param>
    /// <param name="light">Whether the resolved app theme is light.</param>
    /// <param name="highContrast">Whether Windows high contrast is active.</param>
    /// <param name="schema">Current layout schema number, clamped to 0–10000.</param>
    /// <param name="updateStatus">Last release-check outcome, or null when no check ran.</param>
    /// <param name="recentCodes">In-memory classified failures; at most eight valid nonzero codes are included. Null means none.</param>
    /// <returns>Readable JSON suitable for preview and explicit clipboard copy; nothing is uploaded or written to disk.</returns>
    public static string Create(string? appVersion, string? runtime, Version? windowsVersion, Version? dotnetVersion,
        string? webViewVersion, string? language, bool light, bool highContrast, int schema,
        UpdateCheckStatus? updateStatus, IEnumerable<DiagnosticCode>? recentCodes)
    {
        var match = Regex.Match(webViewVersion is { Length: <= 128 } ? webViewVersion : "", @"\A[0-9]{1,6}(?:\.[0-9]{1,6}){1,3}(?:\z| )", RegexOptions.CultureInvariant);
        return JsonSerializer.Serialize(new
        {
            reportVersion = 1,
            appVersion = ReleaseVersion.Parse(appVersion)?.ToString() ?? "unknown",
            runtime = runtime is "win-x64" or "win-arm64" ? runtime : "unknown",
            windowsVersion = windowsVersion?.ToString() ?? "unknown",
            dotnetVersion = dotnetVersion?.ToString() ?? "unknown",
            webViewVersion = match.Success ? match.Value.Trim() : "unavailable",
            language = TextCatalog.SupportedLanguages.Contains(language) ? language : "unknown",
            theme = highContrast ? "high-contrast" : light ? "light" : "dark",
            layoutSchema = Math.Clamp(schema, 0, 10000),
            updateStatus = updateStatus is { } status && Enum.IsDefined(status) ? status.ToString() : "not-checked",
            recentCodes = (recentCodes ?? []).Take(64).Where(c => c != DiagnosticCode.None && Enum.IsDefined(c)).TakeLast(8).Select(c => c.ToString()).ToArray()
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
