using System.Text.Json;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Installs the fixed manifest.json and tile.js pair under generated package identifiers.</summary>
internal sealed class ScriptTilePackages(string dataDirectory)
{
    private readonly Dictionary<string, ScriptTileManifest> _titleManifests = [];
    internal static IReadOnlyList<string> BundledIds { get; } = Array.AsReadOnly(new[] { "orbit", "calendar", "cpu", "memory", "focus", "year", "clock", "countdown", "network", "battery", "storage", "slideshow", "weather", "feed", "json-monitor" });
    internal static string BundledManifestPath(string id) => BundledIds.Contains(id, StringComparer.Ordinal)
        ? Path.Combine(AppContext.BaseDirectory, "Examples", id, "manifest.json") : throw new ArgumentException("Unknown bundled tile.", nameof(id));
    internal static ScriptTileManifest BundledManifest(string id) => ReadFolder(Path.GetDirectoryName(BundledManifestPath(id))!).Manifest;
    public string Root { get; } = Path.Combine(dataDirectory, "tiles");
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
    internal void InvalidateTitle(string id) => _titleManifests.Remove(id);
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
        if (new FileInfo(manifestPath).Length > 16384 || new FileInfo(sourcePath).Length > ScriptTileProtocol.MaxSource) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptPackageSize));
        var manifest = JsonSerializer.Deserialize<ScriptTileManifest>(File.ReadAllText(manifestPath), ScriptTileProtocol.Json) ?? throw new InvalidDataException(TextCatalog.T(K.ErrorScriptManifestMissing));
        manifest.Validate();
        return (manifest, File.ReadAllText(sourcePath));
    }
    /// <summary>Validates and copies the two package files before creating a pin; layouts never embed executable source paths.</summary>
    public Tile Import(string manifestPath)
    {
        var sourceFolder = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var (manifest, source) = ReadFolder(sourceFolder);
        var id = Guid.NewGuid().ToString("N");
        var destination = Folder(id); Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "manifest.json"), JsonSerializer.Serialize(manifest, ScriptTileProtocol.Json));
        File.WriteAllText(Path.Combine(destination, "tile.js"), source);
        _titleManifests[id] = manifest;
        return new() { AppId = "restart:script:" + id, Label = manifest.LocalizedTitles.GetValueOrDefault(TextCatalog.Language, manifest.Title), UseLocalizedName = true, Widget = "script", ScriptId = id, IsShellApp = false, Size = TileSize.Wide };
    }
    /// <summary>Opens the installed source or settings explicitly in Notepad, never through the .js shell association.</summary>
    public void Edit(string id, string file = "tile.js")
    {
        var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "notepad.exe")) { UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(Folder(id), file));
        System.Diagnostics.Process.Start(start);
    }
}
