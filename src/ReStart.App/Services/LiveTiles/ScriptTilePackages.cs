using System.Text.Json;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Installs the fixed manifest.json and tile.js pair under generated package identifiers.</summary>
internal sealed class ScriptTilePackages(string dataDirectory)
{
    private readonly Dictionary<string, ScriptTileManifest> _titleManifests = [];
    private readonly Dictionary<string, IReadOnlyDictionary<string, WebAsset>> _webAssets = [];
    private int _sourceRevision;
    internal sealed record WebAsset(byte[] Bytes, string ContentType);
    internal static IReadOnlyList<string> BundledIds { get; } = Array.AsReadOnly(new[] { "orbit", "calendar", "cpu", "memory", "focus", "year", "clock", "countdown", "network", "battery", "storage", "slideshow", "weather", "feed", "json-monitor", "weather-scene", "system-dashboard", "news-board", "canvas-garden" });
    internal static string BundledManifestPath(string id) => BundledIds.Contains(id, StringComparer.Ordinal)
        ? Path.Combine(AppContext.BaseDirectory, "Examples", id, "manifest.json") : throw new ArgumentException("Unknown bundled tile.", nameof(id));
    internal static ScriptTileManifest BundledManifest(string id) => ReadFolder(Path.GetDirectoryName(BundledManifestPath(id))!).Manifest;
    public string Root { get; } = Path.Combine(dataDirectory, "tiles");
    internal string BrowserDataRoot { get; } = Path.Combine(dataDirectory, "WebView2");
    internal string LibraryRoot { get; } = Path.Combine(dataDirectory, "TileLibrary");
    internal sealed record LibraryTile(string ManifestPath, ScriptTileManifest? Manifest, string? Error);

    internal IReadOnlyList<LibraryTile> DiscoverLibrary()
    {
        if (!Directory.Exists(LibraryRoot)) return [];
        var entries = new List<LibraryTile>();
        foreach (var directory in Directory.EnumerateDirectories(LibraryRoot, "*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }).Take(128).Order(StringComparer.OrdinalIgnoreCase))
        {
            var manifest = Path.Combine(directory, "manifest.json");
            if (!File.Exists(manifest)) continue;
            try { entries.Add(new(manifest, ReadFolder(directory).Manifest, null)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            { entries.Add(new(manifest, null, ex.Message)); }
        }
        return entries;
    }
    /// <summary>Resolves only an installed GUID identifier; arbitrary paths and traversal are rejected.</summary>
    public string Folder(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptPackageId));
        return Path.Combine(Root, id);
    }
    public (ScriptTileManifest Manifest, string Source) Read(string id)
    {
        var package = ReadFolder(Folder(id)); _titleManifests[id] = package.Manifest; return package;
    }
    internal sealed record RenderSource(ScriptTileManifest Manifest, string Script, string Html, string Css, IReadOnlyDictionary<string, WebAsset> Assets);

    /// <summary>Reads bounded package files away from the dispatcher; callers resume on their owning context before changing presentation state.</summary>
    /// <param name="id">Installed package identity; never an arbitrary directory.</param>
    /// <returns>A detached source snapshot with the existing cached assets, or freshly read assets for its first web render.</returns>
    internal async Task<RenderSource> ReadForRenderAsync(string id)
    {
        var folder = Folder(id);
        var revision = _sourceRevision;
        _webAssets.TryGetValue(id, out var assets);
        var snapshot = await Task.Run(() =>
        {
            var (manifest, source) = ReadFolder(folder);
            var web = manifest.Renderer == "web" ? ReadWebFolder(folder) : default;
            return new RenderSource(manifest, source, web.Html ?? "", web.Css ?? "",
                manifest.Renderer == "web" ? assets ?? ReadAssets(folder) : new Dictionary<string, WebAsset>());
        });
        if (revision == _sourceRevision)
        {
            _titleManifests[id] = snapshot.Manifest;
            if (snapshot.Manifest.Renderer == "web") _webAssets.TryAdd(id, snapshot.Assets);
        }
        return snapshot;
    }
    internal void InvalidateTitle(string id) { _sourceRevision++; _titleManifests.Remove(id); _webAssets.Remove(id); }
    internal bool IsWeb(string id)
    {
        try { return (_titleManifests.TryGetValue(id, out var manifest) ? manifest : Read(id).Manifest).Renderer == "web"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException) { return false; }
    }
    internal string DisplayName(Tile tile, string language)
    {
        try
        {
            if (!_titleManifests.TryGetValue(tile.ScriptId, out var manifest)) manifest = Read(tile.ScriptId).Manifest;
            tile.UseLocalizedName ??= tile.Label == manifest.Title || manifest.LocalizedTitles.Values.Contains(tile.Label, StringComparer.Ordinal);
            return tile.UseLocalizedName == true ? manifest.LocalizedTitles.GetValueOrDefault(TextCatalog.Resolve(language), manifest.Title) : tile.Label;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException) { return tile.Label; }
    }
    internal string SlideshowBindingPath(string id) => Path.Combine(Folder(id), "host-bindings.json");
    internal string DataBindingPath(string id) => Path.Combine(Folder(id), "data-bindings.dat");
    private static (ScriptTileManifest Manifest, string Source) ReadFolder(string folder)
    {
        var manifestPath = Path.Combine(folder, "manifest.json"); var sourcePath = Path.Combine(folder, "tile.js");
        if (!File.Exists(manifestPath) || !File.Exists(sourcePath)) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptSourceMissing));
        if (new FileInfo(manifestPath).Length > 16384 || new FileInfo(sourcePath).Length > ScriptTileProtocol.MaxSource) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptPackageSize));
        var manifest = JsonSerializer.Deserialize<ScriptTileManifest>(File.ReadAllText(manifestPath), ScriptTileProtocol.Json) ?? throw new InvalidDataException(TextCatalog.T(K.ErrorScriptManifestMissing));
        manifest.Validate();
        if (manifest.Renderer == "web") ReadWebFolder(folder);
        return (manifest, File.ReadAllText(sourcePath));
    }
    internal (string Html, string Css) ReadWeb(string id) => ReadWebFolder(Folder(id));
    internal IReadOnlyDictionary<string, WebAsset> WebAssets(string id)
    {
        if (!_webAssets.TryGetValue(id, out var assets)) _webAssets[id] = assets = ReadAssets(Folder(id));
        return assets;
    }
    internal static IReadOnlyDictionary<string, WebAsset> ReadAssets(string folder)
    {
        var root = Path.Combine(folder, "assets");
        var assets = new Dictionary<string, WebAsset>(StringComparer.Ordinal);
        if (!Directory.Exists(root)) return assets;
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptPackageSize));
        long total = 0;
        foreach (var path in Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false }))
        {
            var length = new FileInfo(path).Length; total += length;
            if (assets.Count >= 128 || length > 4 * 1024 * 1024 || total > 16 * 1024 * 1024) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptPackageSize));
            var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
            var type = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".js" or ".mjs" => "text/javascript",
                ".css" => "text/css",
                ".json" => "application/json",
                ".svg" => "image/svg+xml",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                ".ttf" => "font/ttf",
                ".wasm" => "application/wasm",
                _ => "application/octet-stream"
            };
            assets.Add("/assets/" + relative, new(File.ReadAllBytes(path), type));
        }
        return assets;
    }
    internal static (string Html, string Css) ReadWebFolder(string folder)
    {
        string Read(string name)
        {
            var path = Path.Combine(folder, name);
            if (!File.Exists(path)) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptSourceMissing));
            if (new FileInfo(path).Length > 262144) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptPackageSize));
            return File.ReadAllText(path);
        }
        return (Read("index.html"), Read("tile.css"));
    }
    /// <summary>Validates and copies the two package files before creating a pin; layouts never embed executable source paths.</summary>
    public Tile Import(string manifestPath)
    {
        var sourceFolder = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var (manifest, source) = ReadFolder(sourceFolder);
        var assets = manifest.Renderer == "web" ? ReadAssets(sourceFolder) : new Dictionary<string, WebAsset>();
        var id = Guid.NewGuid().ToString("N");
        var destination = Folder(id); Directory.CreateDirectory(destination);
        var web = manifest.Renderer == "web" ? ReadWebFolder(sourceFolder) : default;
        SavePair(destination, JsonSerializer.Serialize(manifest, ScriptTileProtocol.Json), source, web.Html, web.Css);
        foreach (var (name, asset) in assets)
        {
            var path = Path.Combine(destination, name.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, asset.Bytes);
        }
        _titleManifests[id] = manifest;
        return new() { AppId = "restart:script:" + id, Label = manifest.LocalizedTitles.GetValueOrDefault(TextCatalog.Language, manifest.Title), UseLocalizedName = true, Widget = "script", ScriptId = id, IsShellApp = false, Size = TileSize.Wide };
    }
    /// <summary>Validates the same UTF-8 file limits as import, without executing JavaScript.</summary>
    internal static ScriptTileManifest ValidateDraft(string manifestJson, string source)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(manifestJson) > 16384 || System.Text.Encoding.UTF8.GetByteCount(source) > ScriptTileProtocol.MaxSource)
            throw new InvalidDataException(TextCatalog.T(K.ErrorScriptPackageSize));
        var manifest = JsonSerializer.Deserialize<ScriptTileManifest>(manifestJson, ScriptTileProtocol.Json)
            ?? throw new InvalidDataException(TextCatalog.T(K.ErrorScriptManifestMissing));
        manifest.Validate(); return manifest;
    }

    /// <summary>Saves a local library draft under a generated ID, returning its manifest path for an optional import.</summary>
    internal string SaveLibrary(string manifestJson, string source, string? libraryId = null, string? html = null, string? css = null)
    {
        ValidateDraft(manifestJson, source);
        var id = libraryId ?? Guid.NewGuid().ToString("N");
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptPackageId));
        var folder = Path.Combine(LibraryRoot, id);
        SavePair(folder, manifestJson, source, html, css);
        return Path.Combine(folder, "manifest.json");
    }

    /// <summary>Replaces or repairs an installed package, preserving its ID, tile positions and host bindings.</summary>
    internal void SaveInstalled(string id, string manifestJson, string source, string? html = null, string? css = null)
    {
        ValidateDraft(manifestJson, source);
        SavePair(Folder(id), manifestJson, source, html, css); InvalidateTitle(id);
    }

    private static void SavePair(string folder, string manifestJson, string source, string? html = null, string? css = null)
    {
        var manifest = ValidateDraft(manifestJson, source);
        var files = new Dictionary<string, string> { ["tile.js"] = source, ["manifest.json"] = manifestJson };
        if (manifest.Renderer == "web")
        {
            if (html == null || css == null) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptSourceMissing));
            if (System.Text.Encoding.UTF8.GetByteCount(html) > 262144 || System.Text.Encoding.UTF8.GetByteCount(css) > 262144) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptPackageSize));
            files["index.html"] = html; files["tile.css"] = css;
        }
        Directory.CreateDirectory(folder);
        var token = Guid.NewGuid().ToString("N");
        var replaced = new List<string>(); var completed = false;
        string Temp(string name) => Path.Combine(folder, token + "." + name + ".tmp");
        string Backup(string name) => Path.Combine(folder, token + "." + name + ".backup");
        try
        {
            foreach (var (name, value) in files) File.WriteAllText(Temp(name), value, new System.Text.UTF8Encoding(false));
            // Commit the manifest last so a library watcher never accepts a new incomplete package.
            foreach (var name in files.Keys.OrderBy(name => name == "manifest.json"))
            {
                var path = Path.Combine(folder, name);
                if (File.Exists(path)) File.Move(path, Backup(name));
                replaced.Add(name); File.Move(Temp(name), path);
            }
            completed = true;
        }
        catch
        {
            foreach (var name in replaced.AsEnumerable().Reverse())
            {
                var path = Path.Combine(folder, name);
                if (File.Exists(Backup(name))) File.Move(Backup(name), path, true);
                else File.Delete(path);
            }
            throw;
        }
        finally { foreach (var name in files.Keys) { File.Delete(Temp(name)); if (completed) File.Delete(Backup(name)); } }
    }
    /// <summary>Opens the installed source or settings explicitly in Notepad, never through the .js shell association.</summary>
    public void Edit(string id, string file = "tile.js")
    {
        var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "notepad.exe")) { UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(Folder(id), file));
        System.Diagnostics.Process.Start(start);
    }
}
