using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReStart.Core;

/// <summary>Declares a named, read-only JSON or RSS/Atom input. Connections are configured separately on the user's PC.</summary>
public sealed class TileDataDefinition
{
    /// <summary>Gets or sets the parser: json or feed.</summary>
    public string Format { get; set; } = "json";
    /// <summary>Gets or sets the polling interval in seconds; HTTP has a minimum interval of 30 seconds.</summary>
    public int RefreshSeconds { get; set; } = 300;
    /// <summary>Gets or sets an optional JSON Pointer selecting a small portion of the response.</summary>
    public string JsonPointer { get; set; } = "";
    /// <summary>Gets or sets a public example address shown in the configuration dialog, never contacted automatically.</summary>
    public string SuggestedUrl { get; set; } = "";
    /// <summary>Gets or sets the initial connection editor type; this never authorizes access.</summary>
    public string SuggestedAccess { get; set; } = "http";
    /// <summary>Validates the bounded package declaration, which never includes credentials.</summary>
    public bool IsValid() => Format is "json" or "feed" && RefreshSeconds is >= 1 and <= 86400
        && JsonPointer != null && ValidPointer(JsonPointer)
        && SuggestedAccess is "http" or "file" && SuggestedUrl is { Length: <= 2048 }
        && (SuggestedUrl.Length == 0 || TileDataBinding.ValidHttpAddress(SuggestedUrl));
    internal static bool ValidPointer(string? pointer) => pointer == null || pointer.Length <= 256 && (pointer.Length == 0 || pointer.StartsWith('/'))
        && !Regex.IsMatch(pointer, "~(?![01])", RegexOptions.CultureInvariant);
}

/// <summary>Contains a locally chosen connection. Hosts must protect this object at rest and never send it to JavaScript.</summary>
public sealed class TileDataBinding
{
    /// <summary>Gets or sets whether the user enabled this connection.</summary>
    public bool Enabled { get; set; }
    /// <summary>Gets or sets http or file; packages cannot select local files.</summary>
    public string Access { get; set; } = "http";
    /// <summary>Gets or sets an explicitly configured URL or absolute file path.</summary>
    public string Location { get; set; } = "";
    /// <summary>Gets or sets a response selector: null inherits the package pointer, while an empty string selects the root.</summary>
    public string? JsonPointer { get; set; }
    /// <summary>Gets or sets the HTTP header carrying a local credential.</summary>
    public string HeaderName { get; set; } = "Authorization";
    /// <summary>Gets or sets the credential header value; it is never part of script context or package exports.</summary>
    public string Secret { get; set; } = "";
    /// <summary>Validates a configured address and prevents malformed or connection-control request headers.</summary>
    public bool IsValid() => Access is "http" or "file" && Location is { Length: <= 4096 } && !Location.Any(char.IsControl) && TileDataDefinition.ValidPointer(JsonPointer)
        && Secret is { Length: <= 4096 } && !Secret.Any(char.IsControl)
        && HeaderName is { Length: <= 64 } && Regex.IsMatch(HeaderName, "^[A-Za-z][A-Za-z0-9-]*$", RegexOptions.CultureInvariant)
        && !new[] { "Host", "Content-Length", "Connection", "Transfer-Encoding", "Proxy-Authorization", "Accept-Encoding", "Upgrade", "TE", "Cookie" }.Contains(HeaderName, StringComparer.OrdinalIgnoreCase)
        && (!Enabled || (Access == "http" ? ValidHttpAddress(Location) : Path.IsPathFullyQualified(Location) && !Location.StartsWith("\\\\", StringComparison.Ordinal)));
    /// <summary>Accepts HTTPS and explicit loopback HTTP, without embedded user information or URL fragments.</summary>
    public static bool ValidHttpAddress(string address) => Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0 && (uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback);
}

/// <summary>Provides machine-readable failure information without exposing URLs, file paths, credentials or exception text.</summary>
public sealed record TileDataError(string Code, int? HttpStatus = null);
/// <summary>Contains only bounded display data and freshness metadata safe to pass to a tile worker.</summary>
public sealed record TileDataSnapshot(string Status, JsonElement Value, DateTimeOffset? UpdatedAt, DateTimeOffset? CheckedAt, bool Stale, TileDataError? Error);
