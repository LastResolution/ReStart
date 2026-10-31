using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Contains current requested access, not a grant. Host adapters construct it from the current package and connection.</summary>
internal sealed record TileIntegrationRequest(string TileId, string PackageId, string Integration, string RequestFingerprint,
    string ConnectionFingerprint, string[] Entities, string[] Actions);
/// <summary>Contains a separately saved host approval. It is never imported from a manifest or sent to a tile.</summary>
internal sealed record TileIntegrationGrant(string Token, string TileId, string PackageId, string Integration, string RequestFingerprint,
    string ConnectionFingerprint, string[] Entities, string[] Actions)
{
    internal bool IsValid() => Guid.TryParseExact(Token, "N", out _) && Guid.TryParseExact(TileId, "N", out _)
        && Guid.TryParseExact(PackageId, "N", out _) && ValidName(Integration) && ValidHash(RequestFingerprint) && ValidHash(ConnectionFingerprint)
        && ValidList(Entities) && ValidList(Actions);
    private static bool ValidHash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private static bool ValidList(string[] values) => values is { Length: > 0 and <= 64 } && values.All(ValidName)
        && values.Distinct(StringComparer.Ordinal).Count() == values.Length;
    private static bool ValidName(string value) => value is { Length: > 0 and <= 128 }
        && Regex.IsMatch(value, "^[A-Za-z0-9_.:-]+$", RegexOptions.CultureInvariant);
}

/// <summary>Checks every host operation against current requests and current local approval, including completion after an await.</summary>
/// <param name="current">Host-owned callback returning current request and grant; it must not trust frontend or manifest-supplied grants.</param>
internal sealed class TileIntegrationBroker(Func<(TileIntegrationRequest Request, TileIntegrationGrant? Grant)> current)
{
    /// <summary>Returns an approval revision only when the exact identity, integration, request and connection are still authorized.</summary>
    /// <param name="integration">Requested host integration name.</param>
    /// <param name="entity">One requested and explicitly approved entity.</param>
    /// <param name="action">One requested and explicitly approved action.</param>
    /// <returns>Opaque host-only approval revision, or null for denied, changed, revoked or unreadable approval.</returns>
    internal string? Stamp(string integration, string entity, string action)
    {
        try
        {
            var (request, grant) = current();
            if (grant == null || !grant.IsValid() || request.TileId != grant.TileId || request.PackageId != grant.PackageId
                || integration != request.Integration || integration != grant.Integration || request.RequestFingerprint != grant.RequestFingerprint
                || request.ConnectionFingerprint != grant.ConnectionFingerprint || request.Entities == null || request.Actions == null
                || !request.Entities.Contains(entity, StringComparer.Ordinal) || !request.Actions.Contains(action, StringComparer.Ordinal)
                || !grant.Entities.Contains(entity, StringComparer.Ordinal) || !grant.Actions.Contains(action, StringComparer.Ordinal)) return null;
            return grant.Token;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or JsonException) { return null; }
    }

    /// <summary>Runs an authorized host provider without accepting credentials or provider objects from a tile.</summary>
    /// <typeparam name="T">Display-safe provider result; the host adapter must remove secrets and bound its size.</typeparam>
    /// <param name="integration">Host integration name to revalidate before and after execution.</param>
    /// <param name="entity">Requested entity.</param>
    /// <param name="action">Requested action.</param>
    /// <param name="provider">Host-owned provider closure; credentials remain inside it.</param>
    /// <param name="cancellation">Cancellation forwarded to the provider.</param>
    /// <returns>The provider result only while the same approval revision remains current.</returns>
    /// <exception cref="UnauthorizedAccessException">Approval is missing, out of scope, changed or revoked before completion.</exception>
    internal async Task<T> InvokeAsync<T>(string integration, string entity, string action, Func<CancellationToken, Task<T>> provider, CancellationToken cancellation = default)
    {
        var stamp = Stamp(integration, entity, action) ?? throw new UnauthorizedAccessException("Tile integration permission required.");
        cancellation.ThrowIfCancellationRequested();
        var result = await provider(cancellation).ConfigureAwait(false);
        if (Stamp(integration, entity, action) != stamp) throw new UnauthorizedAccessException("Tile integration permission changed.");
        return result;
    }
}

/// <summary>Builds canonical request and connection fingerprints without creating approval.</summary>
internal static class TileDataPermissions
{
    internal static TileIntegrationRequest Request(string tileId, string packageId, ScriptTileManifest manifest, Dictionary<string, TileDataBinding> bindings)
    {
        manifest.Validate();
        var entities = manifest.Capabilities.Contains("data.sources") ? manifest.DataSources.Keys.Order(StringComparer.Ordinal).ToArray() : [];
        var requests = new { capabilities = manifest.Capabilities.Order(StringComparer.Ordinal), sources = manifest.DataSources.OrderBy(p => p.Key, StringComparer.Ordinal) };
        // Include the full chosen connection (including a one-way credential hash)
        // so a partial save cannot reuse approval for a different destination.
        return new(tileId, packageId, "data.sources", Hash(requests), Hash(bindings.OrderBy(p => p.Key, StringComparer.Ordinal)), entities, ["read"]);
    }

    private static string Hash<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, ScriptTileProtocol.Json);
        try { return Convert.ToHexStringLower(SHA256.HashData(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

}
