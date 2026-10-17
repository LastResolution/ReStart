using System.Text.Json;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Installs the fixed manifest.json and tile.js pair under generated package identifiers.</summary>
internal sealed class ScriptTilePackages(string dataDirectory)
{
    public string Root { get; } = Path.Combine(dataDirectory, "tiles");
    /// <summary>Resolves only an installed GUID identifier; arbitrary paths and traversal are rejected.</summary>
    public string Folder(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException(TextCatalog.T(K.ErrorScriptPackageId));
        return Path.Combine(Root, id);
    }
    public (ScriptTileManifest Manifest, string Source) Read(string id) => ReadFolder(Folder(id));
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
        return new() { AppId = "restart:script:" + id, Label = manifest.Title, Widget = "script", ScriptId = id, IsShellApp = false, Size = TileSize.Wide };
    }
    /// <summary>Opens the installed source or settings explicitly in Notepad, never through the .js shell association.</summary>
    public void Edit(string id, string file = "tile.js")
    {
        var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "notepad.exe")) { UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(Folder(id), file));
        System.Diagnostics.Process.Start(start);
    }
}
