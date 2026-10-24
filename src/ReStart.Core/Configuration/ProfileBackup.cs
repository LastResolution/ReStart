using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace ReStart.Core;

/// <summary>Creates private, bounded profile archives from explicit app-owned resources; connections, logs and caches are excluded.</summary>
public static class ProfileBackup
{
    private const long MaximumTotal = 512L * 1024 * 1024;
    private const long MaximumFile = 32L * 1024 * 1024;
    private const int MaximumEntries = 4096;
    private const string Index = "ReStart-backup.json";
    private sealed record Entry(string Path, long Size, string Sha256);
    private sealed record Inventory(int Version, Entry[] Files);
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = 8 };

    /// <summary>Writes an atomic profile ZIP from a detached layout and allowlisted local resources.</summary>
    /// <param name="archivePath">User-selected output filename; an existing file is replaced only after the complete archive is written.</param>
    /// <param name="dataDirectory">App-owned data root; symlink paths are rejected and external targets are never followed.</param>
    /// <param name="layout">Detached current layout; this object is not mutated. Custom source may contain private user-written information.</param>
    /// <exception cref="IOException">A required resource is missing, linked, oversized or unreadable, or the output cannot be written.</exception>
    /// <exception cref="InvalidDataException">A resource, package or archive destination violates the backup format or size limits.</exception>
    public static void Export(string archivePath, string dataDirectory, LayoutDocument layout)
    {
        var document = JsonSerializer.Deserialize<LayoutDocument>(JsonSerializer.Serialize(layout, LayoutStore.JsonOptions), LayoutStore.JsonOptions)!;
        document.Normalize(); ClearGrants(document);
        NoLinks(dataDirectory);
        var dataPrefix = Path.GetFullPath(dataDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetExtension(archivePath).Equals(".zip", StringComparison.OrdinalIgnoreCase) || Path.GetFullPath(archivePath).StartsWith(dataPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a ZIP destination outside the app data directory.");
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in Tiles(document).Where(t => t.Widget == "script").Select(t => t.ScriptId).Distinct())
        {
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("Invalid installed tile identifier.");
            AddPackage("tiles/" + id);
        }
        var library = SafePath(dataDirectory, "TileLibrary");
        if (Directory.Exists(library))
        {
            var directories = Directory.EnumerateDirectories(library).Take(129).ToArray();
            if (directories.Length > 128) throw new InvalidDataException("Too many library packages.");
            foreach (var directory in directories) AddPackage("TileLibrary/" + Path.GetFileName(directory));
        }
        if (document.Preferences.CustomBackgroundId.Length > 0)
        {
            var relative = "Backgrounds/" + document.Preferences.CustomBackgroundId + ".png";
            sources.Add(relative, SafePath(dataDirectory, relative));
        }
        var temporary = archivePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var entries = new List<Entry>(); long total = 0;
            using (var output = new ZipArchive(new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None), ZipArchiveMode.Create))
            {
                Add("layout.json", JsonSerializer.SerializeToUtf8Bytes(document, LayoutStore.JsonOptions));
                foreach (var (relative, source) in sources)
                {
                    NoLinks(source);
                    if (new FileInfo(source).Length > MaximumFile) throw new InvalidDataException("Backup resource is too large.");
                    Add(relative, File.ReadAllBytes(source));
                }
                var metadata = JsonSerializer.SerializeToUtf8Bytes(new Inventory(1, entries.ToArray()), Json);
                using var index = output.CreateEntry(Index).Open(); index.Write(metadata);
                void Add(string name, byte[] bytes)
                {
                    if (entries.Count >= MaximumEntries || (total += bytes.Length) > MaximumTotal || bytes.Length > MaximumFile || name == "layout.json" && bytes.Length > 5 * 1024 * 1024) throw new InvalidDataException("Backup size limit exceeded.");
                    entries.Add(new(name, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes))));
                    using var target = output.CreateEntry(name, CompressionLevel.Optimal).Open(); target.Write(bytes);
                }
            }
            if (new FileInfo(temporary).Length > MaximumTotal) throw new InvalidDataException("Backup archive is too large.");
            File.Move(temporary, archivePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }

        void AddPackage(string relative)
        {
            var root = SafePath(dataDirectory, relative); NoLinks(root);
            if (!File.Exists(Path.Combine(root, "manifest.json")) || !File.Exists(Path.Combine(root, "tile.js"))) throw new InvalidDataException("A tile package is incomplete.");
            foreach (var name in new[] { "manifest.json", "tile.js", "index.html", "tile.css" })
                if (File.Exists(Path.Combine(root, name))) sources.Add(relative + "/" + name, SafePath(root, name));
            var assets = SafePath(root, "assets");
            if (Directory.Exists(assets))
                foreach (var path in AssetFiles(assets))
                {
                    NoLinks(path);
                    var name = relative + "/assets/" + Path.GetRelativePath(assets, path).Replace('\\', '/');
                    if (!Allowed(name)) throw new InvalidDataException("Unsupported backup asset.");
                    sources.Add(name, path);
                    if (sources.Count >= MaximumEntries) throw new InvalidDataException("Too many backup resources.");
                }
            ValidatePackage(root);
        }
    }

    private static IEnumerable<string> AssetFiles(string root)
    {
        var pending = new Stack<string>(); pending.Push(root); var count = 0;
        while (pending.TryPop(out var directory))
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                if (++count > MaximumEntries) throw new InvalidDataException("Too many backup assets.");
                NoLinks(path);
                if (Directory.Exists(path)) pending.Push(path); else yield return path;
            }
    }

    /// <summary>Validates an archive and stages fresh resource IDs without replacing any existing file or saved layout.</summary>
    /// <param name="archivePath">User-selected ZIP, bounded to 512 MiB compressed and expanded.</param>
    /// <param name="dataDirectory">App-owned resource destination; existing resources remain untouched.</param>
    /// <returns>An owned restore transaction. Dispose on cancellation or failure; Commit persists its detached layout exactly once.</returns>
    /// <exception cref="IOException">The archive, inventory, hashes, paths, layout or required resources are invalid.</exception>
    /// <exception cref="InvalidDataException">The archive contains an unsupported inventory, invalid paths, modified resources or excessive sizes.</exception>
    public static ProfileRestore PrepareRestore(string archivePath, string dataDirectory)
    {
        NoLinks(dataDirectory); Directory.CreateDirectory(dataDirectory);
        var staging = SafePath(dataDirectory, ".restore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var created = new List<string>();
        try
        {
            if (new FileInfo(archivePath).Length > MaximumTotal) throw new InvalidDataException("Backup archive is too large.");
            using var zip = ZipFile.OpenRead(archivePath);
            if (zip.Entries.Count > MaximumEntries + 1) throw new InvalidDataException("Too many backup resources.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
            foreach (var entry in zip.Entries)
            {
                if (!names.Add(entry.FullName) || entry.FullName != Index && !Allowed(entry.FullName) || entry.Length > (entry.FullName == Index ? 2 * 1024 * 1024 : MaximumFile)
                    || (total += entry.Length) > MaximumTotal || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Invalid backup entry.");
                var destination = SafePath(staging, entry.FullName); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var input = entry.Open(); using var target = new FileStream(destination, FileMode.CreateNew);
                var buffer = new byte[81920]; long written = 0; int count;
                while ((count = input.Read(buffer)) > 0) { if ((written += count) > entry.Length) throw new InvalidDataException("Invalid expanded size."); target.Write(buffer, 0, count); }
                if (written != entry.Length) throw new InvalidDataException("Truncated backup entry.");
            }
            if (!names.Contains(Index) || !names.Contains("layout.json")) throw new InvalidDataException("Incomplete backup.");
            var inventory = JsonSerializer.Deserialize<Inventory>(File.ReadAllBytes(Path.Combine(staging, Index)), Json);
            if (inventory is not { Version: 1, Files.Length: > 0 and <= MaximumEntries } || inventory.Files.Any(f => f == null || !Allowed(f.Path))
                || inventory.Files.Select(f => f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != inventory.Files.Length
                || !names.SetEquals(inventory.Files.Select(f => f.Path).Append(Index))) throw new InvalidDataException("Invalid backup inventory.");
            foreach (var file in inventory.Files)
            {
                var path = SafePath(staging, file.Path);
                if (new FileInfo(path).Length != file.Size || !Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Backup checksum mismatch.");
            }
            var document = LayoutStore.Read(Path.Combine(staging, "layout.json")); ClearGrants(document);
            var packages = new Dictionary<string, string>();
            foreach (var root in new[] { "tiles", "TileLibrary" })
            {
                var parent = Path.Combine(staging, root); if (!Directory.Exists(parent)) continue;
                foreach (var source in Directory.GetDirectories(parent))
                {
                    ValidatePackage(source);
                    var id = Guid.NewGuid().ToString("N"); var destination = SafePath(dataDirectory, root + "/" + id);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!); Directory.Move(source, destination); created.Add(destination);
                    if (root == "tiles") packages[Path.GetFileName(source)] = id;
                }
            }
            foreach (var tile in Tiles(document).Where(t => t.Widget == "script"))
                tile.ScriptId = packages.TryGetValue(tile.ScriptId, out var id) ? id : throw new InvalidDataException("A referenced tile package is missing.");
            if (document.Preferences.CustomBackgroundId.Length > 0)
            {
                var source = SafePath(staging, "Backgrounds/" + document.Preferences.CustomBackgroundId + ".png");
                var id = Guid.NewGuid().ToString("N"); var destination = SafePath(dataDirectory, "Backgrounds/" + id + ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Move(source, destination); created.Add(destination); document.Preferences.CustomBackgroundId = id;
            }
            return new(document, staging, created);
        }
        catch { Cleanup(staging, created); throw; }
    }

    private static void ValidatePackage(string directory)
    {
        var manifest = Path.Combine(directory, "manifest.json"); var script = Path.Combine(directory, "tile.js");
        if (new FileInfo(manifest).Length > 16384 || new FileInfo(script).Length > ScriptTileProtocol.MaxSource) throw new InvalidDataException("Invalid tile package size.");
        var model = JsonSerializer.Deserialize<ScriptTileManifest>(File.ReadAllText(manifest), ScriptTileProtocol.Json) ?? throw new InvalidDataException("Missing tile manifest."); model.Validate();
        if (model.Renderer == "web")
            foreach (var name in new[] { "index.html", "tile.css" })
                if (new FileInfo(Path.Combine(directory, name)).Length > 256 * 1024) throw new InvalidDataException("Invalid web package size.");
        var assets = Path.Combine(directory, "assets");
        if (Directory.Exists(assets))
        {
            var files = Directory.GetFiles(assets, "*", SearchOption.AllDirectories);
            if (files.Length > 128 || files.Sum(p => new FileInfo(p).Length) > 16 * 1024 * 1024 || files.Any(p => new FileInfo(p).Length > 4 * 1024 * 1024)) throw new InvalidDataException("Invalid asset size.");
        }
    }
    private static IEnumerable<Tile> Tiles(LayoutDocument document) => document.Groups.SelectMany(g => g.Tiles).SelectMany(t => new[] { t }.Concat(t.Children));
    private static void ClearGrants(LayoutDocument document) { foreach (var tile in Tiles(document)) tile.AllowWebNetwork = false; }
    private static bool Allowed(string relative)
    {
        if (!ValidPath(relative)) return false;
        var parts = relative.Split('/');
        if (relative == "layout.json") return true;
        if (parts.Length == 2 && parts[0] == "Backgrounds") return Path.GetExtension(parts[1]) == ".png" && Guid.TryParseExact(Path.GetFileNameWithoutExtension(parts[1]), "N", out _);
        if (parts.Length < 3 || parts[0] is not ("tiles" or "TileLibrary") || parts[0] == "tiles" && !Guid.TryParseExact(parts[1], "N", out _)) return false;
        if (parts.Length == 3) return parts[2] is "manifest.json" or "tile.js" or "index.html" or "tile.css";
        return parts[2] == "assets" && !parts[^1].Equals("host-bindings.json", StringComparison.OrdinalIgnoreCase) && !parts[^1].Equals("data-bindings.dat", StringComparison.OrdinalIgnoreCase)
            && Path.GetExtension(relative).ToLowerInvariant() is ".js" or ".mjs" or ".css" or ".json" or ".svg" or ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".woff" or ".woff2" or ".ttf" or ".otf";
    }
    private static bool ValidPath(string relative) => relative.Length is > 0 and <= 240 && !relative.Contains('\\') && relative.Split('/').All(p => p.Length > 0 && p is not ("." or "..") && !p.EndsWith('.') && !p.EndsWith(' ') && !p.Any(c => c < 32 || ":<>\"|?*".Contains(c))
        && !System.Text.RegularExpressions.Regex.IsMatch(p, @"\A(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|\z)", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    private static string SafePath(string root, string relative)
    {
        if (!ValidPath(relative)) throw new InvalidDataException("Invalid backup path.");
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var result = Path.GetFullPath(Path.Combine(prefix, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Backup path escaped its root.");
        NoLinks(result); return result;
    }
    private static void NoLinks(string path)
    {
        for (var parent = Path.GetFullPath(path); !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
            if ((File.Exists(parent) || Directory.Exists(parent)) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked backup paths are not supported.");
    }
    internal static void Cleanup(string staging, IEnumerable<string> created)
    {
        foreach (var path in created.Reverse()) { NoLinks(path); if (Directory.Exists(path)) Directory.Delete(path, true); else if (File.Exists(path)) File.Delete(path); }
        NoLinks(staging); if (Directory.Exists(staging)) Directory.Delete(staging, true);
    }
}

/// <summary>Owns fresh staged resources until the user confirms persistence; existing resources are never replaced.</summary>
public sealed class ProfileRestore : IDisposable
{
    private readonly string _staging;
    private readonly List<string> _created;
    private bool _committed, _disposed;
    internal ProfileRestore(LayoutDocument layout, string staging, List<string> created) { Layout = layout; _staging = staging; _created = created; }
    /// <summary>Gets the detached restored layout with fresh package/background IDs and web networking disabled.</summary>
    public LayoutDocument Layout { get; }
    /// <summary>Persists the candidate exactly once before accepting ownership of its resources.</summary>
    /// <param name="persist">Non-null atomic layout writer, invoked on the caller's thread; must not perform UI rendering after the write.</param>
    /// <exception cref="InvalidOperationException">This transaction was already committed or disposed.</exception>
    public void Commit(Action<LayoutDocument> persist)
    {
        if (_disposed || _committed) throw new InvalidOperationException("Restore transaction is no longer pending.");
        persist(Layout); _committed = true;
    }
    /// <summary>Removes staged files and, unless committed, only newly created resources; originals are retained.</summary>
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        ProfileBackup.Cleanup(_staging, _committed ? [] : _created);
    }
}
