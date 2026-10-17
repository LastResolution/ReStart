using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ReStart.Core;

/// <summary>Describes the outcome of a read-only release check.</summary>
public enum UpdateCheckStatus
{
    /// <summary>A newer published release with complete assets is available.</summary>
    Available,
    /// <summary>No newer eligible release was found.</summary>
    UpToDate,
    /// <summary>No eligible published release was found.</summary>
    NoReleases,
    /// <summary>The public release service is unavailable.</summary>
    ServiceUnavailable,
    /// <summary>GitHub declined the request due to a rate limit.</summary>
    RateLimited,
    /// <summary>The service could not be reached successfully.</summary>
    NetworkError,
    /// <summary>The response was malformed, too large, or exceeded the pagination bound.</summary>
    InvalidResponse
}

/// <summary>Describes a published release; its page URL is constructed only within the fixed repository.</summary>
public sealed record UpdateRelease(string Tag, ReleaseVersion Version, string Notes)
{
    /// <summary>Gets the browser release page; the app never executes a feed-supplied URL.</summary>
    public Uri Page => new($"https://github.com/LastResolution/ReStart/releases/tag/{Uri.EscapeDataString(Tag)}");
}

/// <summary>Contains a release check result without raw server errors.</summary>
public sealed record UpdateCheckResult(UpdateCheckStatus Status, UpdateRelease? Release = null);

/// <summary>Checks GitHub releases without downloading or installing executables. The caller owns the HTTP client.</summary>
public sealed class ReleaseClient(HttpClient client)
{
    /// <summary>Gets the repository's browser release listing.</summary>
    public static Uri ReleasesPage { get; } = new("https://github.com/LastResolution/ReStart/releases");

    /// <summary>Checks up to 500 releases, respecting prerelease preference, semantic versions and complete architecture assets.</summary>
    public async Task<UpdateCheckResult> CheckAsync(string currentVersion, string runtime, bool includePrereleases, CancellationToken cancellationToken = default)
    {
        var current = ReleaseVersion.Parse(currentVersion) ?? throw new ArgumentException("Invalid current version.", nameof(currentVersion));
        if (runtime is not ("win-x64" or "win-arm64")) throw new ArgumentException("Unsupported runtime.", nameof(runtime));
        UpdateRelease? latest = null;
        try
        {
            for (var page = 1; page <= 5; page++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/LastResolution/ReStart/releases?per_page=100&page={page}");
                request.Headers.UserAgent.ParseAdd("ReStart/" + current);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.StatusCode == HttpStatusCode.TooManyRequests
                    || response.StatusCode == HttpStatusCode.Forbidden && (response.Headers.RetryAfter != null
                        || response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.Contains("0")))
                    return new(UpdateCheckStatus.RateLimited);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound) return new(UpdateCheckStatus.ServiceUnavailable);
                if (!response.IsSuccessStatusCode) return new(UpdateCheckStatus.NetworkError);
                if (response.Content.Headers.ContentLength > 1024 * 1024) return new(UpdateCheckStatus.InvalidResponse);
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var buffer = new MemoryStream();
                var bytes = new byte[8192]; int count;
                while ((count = await stream.ReadAsync(bytes, cancellationToken)) > 0)
                {
                    if (buffer.Length + count > 1024 * 1024) return new(UpdateCheckStatus.InvalidResponse);
                    buffer.Write(bytes, 0, count);
                }
                using var json = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
                if (json.RootElement.ValueKind != JsonValueKind.Array) return new(UpdateCheckStatus.InvalidResponse);
                foreach (var entry in json.RootElement.EnumerateArray())
                {
                    if (!entry.TryGetProperty("draft", out var draft) || draft.ValueKind != JsonValueKind.False
                        || !entry.TryGetProperty("prerelease", out var preview) || preview.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                        || !entry.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String) continue;
                    var tagText = tag.GetString()!;
                    var version = ReleaseVersion.Parse(tagText);
                    if (version == null || !includePrereleases && (preview.GetBoolean() || version.Prerelease.Length > 0)) continue;
                    if (!entry.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) continue;
                    var names = assets.EnumerateArray().Where(a => a.TryGetProperty("state", out var state) && state.GetString() == "uploaded")
                        .Select(a => a.GetProperty("name").GetString()).ToHashSet(StringComparer.Ordinal);
                    if (!names.Contains($"ReStart-{runtime}.zip") || !names.Contains($"ReStart-{runtime}.zip.sha256")) continue;
                    var notes = entry.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String ? body.GetString() ?? "" : "";
                    if (latest == null || version.CompareTo(latest.Version) > 0) latest = new(tagText, version, notes[..Math.Min(notes.Length, 32768)]);
                }
                var hasNext = response.Headers.TryGetValues("Link", out var links) && links.Any(link => link.Contains("rel=\"next\"", StringComparison.Ordinal));
                if (!hasNext) return latest == null ? new(UpdateCheckStatus.NoReleases)
                    : new(latest.Version.CompareTo(current) > 0 ? UpdateCheckStatus.Available : UpdateCheckStatus.UpToDate, latest);
            }
            return new(UpdateCheckStatus.InvalidResponse);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(UpdateCheckStatus.NetworkError); }
        catch (Exception ex) when (ex is HttpRequestException or IOException) { return new(UpdateCheckStatus.NetworkError); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return new(UpdateCheckStatus.InvalidResponse); }
    }
}
