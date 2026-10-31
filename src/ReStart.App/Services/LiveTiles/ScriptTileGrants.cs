using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Stores explicit tile-specific approvals with host-only DPAPI protection, separate from connection credentials and packages.</summary>
internal static class ScriptTileGrants
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ReStart:TileIntegrationGrant:v1");
    internal static TileIntegrationGrant? Read(string path)
    {
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 32768) throw new InvalidDataException("Invalid tile approval.");
        CheckLinks(path);
        var bytes = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
        try
        {
            var grant = JsonSerializer.Deserialize<TileIntegrationGrant>(bytes);
            if (grant == null || !grant.IsValid()) throw new InvalidDataException("Invalid tile approval.");
            return grant;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    /// <summary>Creates approval only from an explicit native-editor save; package import and declaration parsing never call it.</summary>
    internal static void ApproveData(string path, string tileId, string packageId, ScriptTileManifest manifest, Dictionary<string, TileDataBinding> bindings)
    {
        CheckLinks(path);
        var entities = bindings.Where(p => p.Value.Enabled).Select(p => p.Key).Order(StringComparer.Ordinal).ToArray();
        if (entities.Length == 0) { File.Delete(path); return; }
        var request = DataRequest(tileId, packageId, manifest, bindings);
        var grant = new TileIntegrationGrant(Guid.NewGuid().ToString("N"), tileId, packageId, request.Integration,
            request.RequestFingerprint, request.ConnectionFingerprint, entities, ["read"]);
        if (!grant.IsValid() || entities.Any(id => !request.Entities.Contains(id, StringComparer.Ordinal))) throw new InvalidDataException("Invalid tile approval.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(grant);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser)); output.Flush(flushToDisk: true); }
            File.Move(temporary, path, overwrite: true);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); File.Delete(temporary); }
    }

    internal static TileIntegrationRequest DataRequest(string tileId, string packageId, ScriptTileManifest manifest, Dictionary<string, TileDataBinding> bindings)
        => TileDataPermissions.Request(tileId, packageId, manifest, bindings);

    private static void CheckLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked tile approval path.");
    }
}
