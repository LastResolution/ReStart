using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReStart.Core;

namespace ReStart.App.Services;

internal static partial class PortableUpdates
{
    private static string PreparedIndex(string installation) => Path.Combine(CacheRoot, "prepared-" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(installation).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant()))) + ".json");

    /// <summary>Persists only the owned job identity after an explicit download has been validated.</summary>
    /// <param name="id">Canonical workspace identifier; no installation path is included in the index contents.</param>
    /// <param name="installation">Portable installation used to derive its independent local cache index.</param>
    private static void RememberPrepared(string id, string installation)
    {
        Workspace(id);
        var path = PreparedIndex(installation); var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(id)); File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Forgets only an index that still refers to this job, preserving a newer preparation.</summary>
    /// <param name="id">Accepted or cancelled canonical workspace identifier.</param>
    /// <param name="installation">Portable installation associated with the index.</param>
    internal static void ForgetPrepared(string id, string installation)
    {
        try
        {
            Workspace(id); var path = PreparedIndex(installation);
            if (File.Exists(path) && new FileInfo(path).Length <= 128 && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0
                && JsonSerializer.Deserialize<string>(File.ReadAllText(path)) == id) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) { }
    }

    /// <summary>Restores an explicitly downloaded update without starting a helper or accepting a handoff.</summary>
    /// <param name="installation">Current portable installation; another installation's index is never used.</param>
    /// <returns>A fully revalidated newer package, or null for absent, stale, linked, modified or incomplete preparation.</returns>
    internal static Prepared? RestorePrepared(string installation)
    {
        try
        {
            Workspace("00000000000000000000000000000000");
            var path = PreparedIndex(installation);
            if (!File.Exists(path) || new FileInfo(path).Length > 128 || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return null;
            var id = JsonSerializer.Deserialize<string>(File.ReadAllText(path));
            if (id == null) return null;
            var request = ReadRequest(id, installation);
            if (!Same(request.Installation, installation)) return null;
            var directory = Workspace(id);
            if (File.Exists(Path.Combine(directory, "accepted")) || File.Exists(Path.Combine(directory, "outcome")) || File.Exists(Path.Combine(directory, "transaction.json"))) return null;
            var prepared = new Prepared(id, directory, request.Version, Path.GetFullPath(installation)); ValidatePrepared(prepared, request); return prepared;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or BadImageFormatException) { return null; }
    }

    /// <summary>Rechecks current distribution, cached payload and helper before restoring or launching a staged update.</summary>
    /// <param name="prepared">Canonical job identity, directory and expected version.</param>
    /// <param name="request">Bounded request already read from the same workspace.</param>
    private static void ValidatePrepared(Prepared prepared, Request request)
    {
        if (!Same(prepared.Directory, Workspace(prepared.Id)) || prepared.Version != request.Version) throw new InvalidDataException("Prepared update identity differs.");
        var previous = UpdatePackage.ReadManifest(request.Installation);
        var payload = Path.Combine(prepared.Directory, "payload"); var next = UpdatePackage.ReadManifest(payload);
        if (previous.Runtime != request.Runtime || next.Runtime != request.Runtime || next.Version != request.Version
            || ReleaseVersion.Parse(next.Version)!.CompareTo(ReleaseVersion.Parse(previous.Version)) <= 0) throw new InvalidDataException("Prepared update is stale.");
        UpdatePackage.VerifyFiles(request.Installation, previous); UpdatePackage.VerifyFiles(payload, next);
        var helper = previous.Files.SingleOrDefault(file => file.Path == "ReStart.exe") ?? throw new InvalidDataException("Missing helper inventory.");
        UpdatePackage.VerifyFiles(prepared.Directory, new(1, previous.Version, previous.Runtime, [helper]));
        var executable = Path.Combine(payload, "ReStart.exe");
        if (ReleaseVersion.Parse(System.Diagnostics.FileVersionInfo.GetVersionInfo(executable).ProductVersion)?.ToString() != next.Version)
            throw new InvalidDataException("Prepared executable version differs.");
        using var image = new System.Reflection.PortableExecutable.PEReader(File.OpenRead(executable));
        if (image.PEHeaders.CoffHeader.Machine != (next.Runtime == "win-arm64" ? System.Reflection.PortableExecutable.Machine.Arm64 : System.Reflection.PortableExecutable.Machine.Amd64))
            throw new InvalidDataException("Prepared executable architecture differs.");
    }
}
