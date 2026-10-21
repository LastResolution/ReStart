using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Stores user-selected connections separately from packages, encrypted for the current Windows user with DPAPI.</summary>
internal static class ScriptDataBindings
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ReStart:TileData:v1");
    internal static Dictionary<string, TileDataBinding> Read(string path)
    {
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Invalid local data bindings.");
        var bytes = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
        try
        {
            var bindings = JsonSerializer.Deserialize<Dictionary<string, TileDataBinding>>(bytes, ScriptTileProtocol.Json);
            if (bindings == null || bindings.Count > 4 || bindings.Any(p => p.Key.Length is < 1 or > 32 || p.Value == null || !p.Value.IsValid()))
                throw new InvalidDataException("Invalid local data bindings.");
            return bindings;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    internal static void Save(string path, Dictionary<string, TileDataBinding> bindings)
    {
        if (bindings.Count > 4 || bindings.Any(p => p.Key.Length is < 1 or > 32 || p.Value == null || !p.Value.IsValid()))
            throw new InvalidDataException("Invalid local data bindings.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(bindings, ScriptTileProtocol.Json);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            if (bytes.Length > 32768) throw new InvalidDataException("Invalid local data bindings.");
            File.WriteAllBytes(temporary, ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser));
            File.Move(temporary, path, true);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
