using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReStart.Core;

/// <summary>Describes one distribution-owned file, relative to the portable installation.</summary>
/// <param name="Path">Forward-slash relative filename; paths outside the package are invalid.</param>
/// <param name="Size">Exact uncompressed length in bytes, including zero for an empty file.</param>
/// <param name="Sha256">Lowercase or uppercase SHA-256 hexadecimal digest of the complete file.</param>
public sealed record PackageFile(string Path, long Size, string Sha256);

/// <summary>Lists the files owned by one portable distribution, excluding the manifest itself.</summary>
/// <param name="Schema">Manifest format version; only 1 is supported.</param>
/// <param name="Version">Semantic app version without build metadata.</param>
/// <param name="Runtime">Distribution architecture, win-x64 or win-arm64.</param>
/// <param name="Files">Complete distribution file inventory; local user files must never be added.</param>
public sealed record PackageManifest(int Schema, string Version, string Runtime, PackageFile[] Files);

/// <summary>Validates distribution manifests and extracts bounded, hash-checked portable packages.</summary>
public static class UpdatePackage
{
    /// <summary>Gets the reserved distribution manifest filename.</summary>
    public const string ManifestName = "ReStart-package.json";
    /// <summary>Gets the largest accepted compressed download, in bytes.</summary>
    public const long MaximumDownloadBytes = 256L * 1024 * 1024;
    internal const long MaximumExpandedBytes = 768L * 1024 * 1024;
    internal static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = false, WriteIndented = true, MaxDepth = 8 };

    /// <summary>Reads and validates a bounded manifest without reading other files.</summary>
    /// <param name="directory">Existing installation or extracted package directory; no ownership is transferred.</param>
    /// <returns>A validated inventory; duplicate, reserved and escaping filenames are rejected.</returns>
    /// <exception cref="IOException">The manifest is missing, unreadable or invalid.</exception>
    public static PackageManifest ReadManifest(string directory)
    {
        var path = SafePath(directory, ManifestName);
        if (new FileInfo(path).Length > 128 * 1024) throw new InvalidDataException("Manifest too large.");
        PackageManifest manifest;
        try { manifest = JsonSerializer.Deserialize<PackageManifest>(File.ReadAllBytes(path), Json) ?? throw new InvalidDataException("Missing manifest."); }
        catch (JsonException) { throw new InvalidDataException("Invalid manifest."); }
        if (manifest.Schema != 1 || manifest.Version == null || ReleaseVersion.Parse(manifest.Version)?.ToString() != manifest.Version
            || manifest.Runtime is not ("win-x64" or "win-arm64") || manifest.Files is not { Length: > 0 and <= 500 })
            throw new InvalidDataException("Unsupported manifest.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var file in manifest.Files)
        {
            if (file == null || !ValidRelativePath(file.Path) || !names.Add(file.Path) || file.Size < 0
                || file.Size > MaximumExpandedBytes || !ValidHash(file.Sha256) || (total += file.Size) > MaximumExpandedBytes)
                throw new InvalidDataException("Invalid package inventory.");
        }
        if (!names.Contains("ReStart.exe") || !names.Contains("TileHost/ReStart.TileHost.exe")) throw new InvalidDataException("Incomplete package.");
        return manifest;
    }

    /// <summary>Extracts a verified ZIP into a fresh directory and verifies every inventory hash.</summary>
    /// <param name="zipPath">Downloaded ZIP; it must not exceed the compressed size limit.</param>
    /// <param name="destination">New, nonexistent directory for extracted distribution files.</param>
    /// <param name="version">Exact expected semantic version, without a v prefix.</param>
    /// <param name="runtime">Expected win-x64 or win-arm64 distribution architecture.</param>
    /// <returns>The complete validated package inventory.</returns>
    /// <exception cref="IOException">The archive, manifest, paths, sizes or hashes are invalid, or extraction fails.</exception>
    public static PackageManifest Extract(string zipPath, string destination, string version, string runtime)
    {
        if (Directory.Exists(destination) || File.Exists(destination) || new FileInfo(zipPath).Length > MaximumDownloadBytes) throw new InvalidDataException("Unsafe extraction destination or size.");
        NoLinks(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > 550) throw new InvalidDataException("Too many archive entries.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            var directory = entry.FullName.EndsWith('/');
            var name = directory ? entry.FullName[..^1] : entry.FullName;
            if (!(ValidRelativePath(name) || name == ManifestName) || !names.Add(name)
                || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0
                || entry.Length < 0 || (total += entry.Length) > MaximumExpandedBytes || directory && entry.Length != 0)
                throw new InvalidDataException("Unsafe archive entry.");
        }
        Directory.CreateDirectory(destination);
        try
        {
            foreach (var entry in archive.Entries)
            {
                var path = SafePath(destination, entry.FullName.TrimEnd('/'));
                if (entry.FullName.EndsWith('/')) Directory.CreateDirectory(path);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    using var input = entry.Open(); using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    var buffer = new byte[81920]; long written = 0; int count;
                    while ((count = input.Read(buffer)) > 0)
                    {
                        if ((written += count) > entry.Length) throw new InvalidDataException("Archive entry exceeds its declared size.");
                        output.Write(buffer, 0, count);
                    }
                    if (written != entry.Length) throw new InvalidDataException("Truncated archive entry.");
                }
            }
            var manifest = ReadManifest(destination);
            if (manifest.Version != version || manifest.Runtime != runtime) throw new InvalidDataException("Package identity mismatch.");
            var files = Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(destination, p).Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!files.SetEquals(manifest.Files.Select(f => f.Path).Append(ManifestName))) throw new InvalidDataException("Unexpected package files.");
            VerifyFiles(destination, manifest);
            return manifest;
        }
        catch { Directory.Delete(destination, recursive: true); throw; }
    }

    /// <summary>Checks every listed file's exact size and SHA-256, rejecting links in its path.</summary>
    /// <param name="directory">Directory containing the distribution; unrelated files are ignored.</param>
    /// <param name="manifest">Validated inventory returned by ReadManifest or Extract.</param>
    /// <exception cref="IOException">A listed file is missing, modified or linked outside the distribution.</exception>
    public static void VerifyFiles(string directory, PackageManifest manifest)
    {
        foreach (var file in manifest.Files)
        {
            var path = SafePath(directory, file.Path);
            NoLinks(path);
            using var stream = File.OpenRead(path);
            if (stream.Length != file.Size || !Convert.ToHexString(SHA256.HashData(stream)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Distribution file mismatch.");
        }
    }

    internal static bool ValidHash(string? hash) => hash is { Length: 64 } && hash.All(char.IsAsciiHexDigit);
    internal static bool ValidRelativePath(string? path) => path is { Length: > 0 and <= 200 } && !path.Equals(ManifestName, StringComparison.OrdinalIgnoreCase)
        && path.Split('/').All(p => p.Length > 0 && p is not ("." or "..") && !p.EndsWith('.') && !p.EndsWith(' ')
            && Regex.IsMatch(p, @"\A[A-Za-z0-9_][A-Za-z0-9_. ()-]*\z", RegexOptions.CultureInvariant)
            && !Regex.IsMatch(p, @"\A(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|\z)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        && !path.StartsWith(".restart-", StringComparison.OrdinalIgnoreCase);
    internal static string SafePath(string root, string relative)
    {
        if (relative != ManifestName && !ValidRelativePath(relative)) throw new InvalidDataException("Invalid relative path.");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path escaped installation.");
        NoLinks(target);
        return target;
    }
    internal static void NoLinks(string path)
    {
        for (var item = Path.GetFullPath(path); !string.IsNullOrEmpty(item); item = Path.GetDirectoryName(item))
            if ((File.Exists(item) || Directory.Exists(item)) && (File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked update paths are not supported.");
    }
}
