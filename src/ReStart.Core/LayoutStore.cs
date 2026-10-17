using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReStart.Core;

/// <summary>Loads and atomically replaces a local layout while retaining a backup and protecting unsupported schemas.</summary>
public sealed class LayoutStore(string filePath)
{
    private string? _migrationSource;
    /// <summary>Gets the versioned layout serialization settings; this is distinct from the JavaScript wire protocol.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        MaxDepth = 16
    };

    /// <summary>Gets the absolute path of the layout file.</summary>
    public string FilePath { get; } = Path.GetFullPath(filePath);
    /// <summary>Gets the translated recovery status from the latest load attempt, or null.</summary>
    public string? RecoveryMessage { get; private set; }
    /// <summary>Gets whether loading an unsupported schema disabled writes to protect the original file.</summary>
    public bool IsReadOnly { get; private set; }
    /// <summary>Gets the preserved schema-one source created before this store first writes schema two, or null.</summary>
    public string? MigrationBackupPath { get; private set; }

    /// <summary>Loads the layout or a valid backup; preserves corrupt input and marks unsupported versions read-only.</summary>
    public LayoutDocument? Load()
    {
        RecoveryMessage = null;
        IsReadOnly = false;
        _migrationSource = null;
        MigrationBackupPath = null;
        if (!File.Exists(FilePath)) return null;
        try
        {
            var document = Read(FilePath);
            if (document.MigratedFromVersion == 1) _migrationSource = FilePath;
            return document;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or NullReferenceException)
        {
            // A future-version file must never be silently overwritten by an older binary.
            IsReadOnly = ex is UnsupportedLayoutVersionException;
            RecoveryMessage = L.F(K.ErrorLayoutRead, ex.Message);
            if (!IsReadOnly) File.Copy(FilePath, FilePath + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}", false);
            if (!IsReadOnly && File.Exists(FilePath + ".bak"))
            {
                try
                {
                    var backup = Read(FilePath + ".bak");
                    if (backup.MigratedFromVersion == 1) _migrationSource = FilePath + ".bak";
                    RecoveryMessage += L.T(K.RecoveryBackupRestored);
                    return backup;
                }
                catch (Exception backupEx) when (backupEx is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or NullReferenceException) { }
            }
            return null;
        }
    }

    /// <summary>Reads a size-bounded layout file and validates its schema and model invariants.</summary>
    public static LayoutDocument Read(string path)
    {
        if (new FileInfo(path).Length > 5 * 1024 * 1024) throw new InvalidDataException(L.T(K.ErrorLayoutTooLarge));
        var doc = JsonSerializer.Deserialize<LayoutDocument>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException(L.T(K.ErrorLayoutEmpty));
        doc.Normalize();
        return doc;
    }

    /// <summary>Normalizes and writes a temporary file, then replaces the destination while retaining its previous version as a backup.</summary>
    public void Save(LayoutDocument document)
    {
        if (IsReadOnly) throw new InvalidOperationException(L.T(K.ErrorFutureLayout));
        document.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        if (_migrationSource != null && MigrationBackupPath == null)
        {
            var migrationPath = FilePath + $".schema1-{Guid.NewGuid():N}.bak";
            File.Copy(_migrationSource, migrationPath, false);
            MigrationBackupPath = migrationPath;
        }
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(document, JsonOptions));
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true);
        else File.Move(temp, FilePath);
        _migrationSource = null;
    }
}
