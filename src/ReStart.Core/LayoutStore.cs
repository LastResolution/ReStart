using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReStart.Core;

/// <summary>Loads and atomically replaces a local layout while retaining a backup and protecting unsupported schemas.</summary>
/// <param name="filePath">Local layout JSON path; its directory also contains backups and upgrade state.</param>
/// <param name="applicationVersion">Optional application version for migration metadata; null disables application-version tracking while retaining schema migration.</param>
public sealed class LayoutStore(string filePath, string? applicationVersion = null)
{
    private string? _migrationSource;
    private string? _upgradeSource;
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
    /// <summary>Gets the preserved legacy source created before this store first writes the current schema, or null.</summary>
    public string? MigrationBackupPath { get; private set; }
    /// <summary>Gets the producer application version read from disk, or empty for legacy/new settings.</summary>
    public string LoadedAppVersion { get; private set; } = "";
    /// <summary>Gets the source schema read before migration, or null for a new layout.</summary>
    public int? LoadedSchemaVersion { get; private set; }
    /// <summary>Gets the durable backup created for this store's application upgrade, or null before saving.</summary>
    public string? UpgradeBackupPath { get; private set; }

    /// <summary>Loads the layout or a valid backup; preserves corrupt input and marks unsupported versions read-only.</summary>
    /// <returns>The loaded or recovered normalized layout; null when no usable document exists. Recovery details are exposed by the store.</returns>
    public LayoutDocument? Load()
    {
        RecoveryMessage = null;
        IsReadOnly = false;
        _migrationSource = null;
        _upgradeSource = null;
        LoadedAppVersion = "";
        LoadedSchemaVersion = null;
        UpgradeBackupPath = null;
        MigrationBackupPath = null;
        if (!File.Exists(FilePath)) return null;
        try
        {
            var document = Read(FilePath);
            RegisterSource(document, FilePath);
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
                    RegisterSource(backup, FilePath + ".bak");
                    RecoveryMessage += L.T(K.RecoveryBackupRestored);
                    return backup;
                }
                catch (Exception backupEx) when (backupEx is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or NullReferenceException) { }
            }
            return null;
        }
    }

    private void RegisterSource(LayoutDocument document, string source)
    {
        LoadedSchemaVersion = document.MigratedFromVersion ?? document.Version;
        LoadedAppVersion = document.SavedWithAppVersion;
        if (document.MigratedFromVersion.HasValue) _migrationSource = source;
        if (applicationVersion != null && LoadedAppVersion != applicationVersion) _upgradeSource = source;
        if (ReleaseVersion.Parse(LoadedAppVersion) is { } producer && ReleaseVersion.Parse(applicationVersion) is { } current && producer.CompareTo(current) > 0)
        { IsReadOnly = true; RecoveryMessage = L.T(K.ErrorFutureLayout); }
    }

    /// <summary>Reads a size-bounded layout file and validates its schema and model invariants.</summary>
    /// <param name="path">Existing JSON layout file to import or inspect.</param>
    /// <returns>A validated, normalized layout read without changing the destination store.</returns>
    /// <exception cref="InvalidDataException">The file exceeds 5 MiB, contains no layout, or violates model/schema limits.</exception>
    /// <exception cref="JsonException">The file is not valid layout JSON.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static LayoutDocument Read(string path)
    {
        if (new FileInfo(path).Length > 5 * 1024 * 1024) throw new InvalidDataException(L.T(K.ErrorLayoutTooLarge));
        var doc = JsonSerializer.Deserialize<LayoutDocument>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException(L.T(K.ErrorLayoutEmpty));
        doc.Normalize();
        return doc;
    }

    /// <summary>Normalizes and writes a temporary file, then replaces the destination while retaining its previous version as a backup.</summary>
    /// <param name="document">Document to normalize and persist atomically, retaining a last-good backup; read-only newer schemas are rejected.</param>
    /// <exception cref="InvalidOperationException">A newer layout/application version made this store read-only.</exception>
    /// <exception cref="ArgumentException">The explicitly supplied producer application version is invalid.</exception>
    /// <exception cref="IOException">The temporary file, backup, or atomic replacement cannot be written.</exception>
    public void Save(LayoutDocument document)
    {
        if (IsReadOnly) throw new InvalidOperationException(L.T(K.ErrorFutureLayout));
        if (applicationVersion != null && ReleaseVersion.Parse(applicationVersion) == null) throw new ArgumentException("Invalid producer version.", nameof(applicationVersion));
        document.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        if (_migrationSource != null && MigrationBackupPath == null)
        {
            var migrationPath = FilePath + $".schema{LoadedSchemaVersion}-{Guid.NewGuid():N}.bak";
            File.Copy(_migrationSource, migrationPath, false);
            MigrationBackupPath = migrationPath;
        }
        if (_upgradeSource != null && applicationVersion != null)
        {
            if (UpgradeBackupPath == null)
            {
                UpgradeBackupPath = MigrationBackupPath;
                if (UpgradeBackupPath == null)
                {
                    var backup = FilePath + $".upgrade-{Guid.NewGuid():N}.bak";
                    File.Copy(_upgradeSource, backup, false);
                    UpgradeBackupPath = backup;
                }
            }
            document.LastUpgrade = new(LoadedAppVersion, applicationVersion, LoadedSchemaVersion ?? document.Version,
                document.Version, DateTimeOffset.UtcNow, Path.GetFileName(UpgradeBackupPath));
        }
        if (applicationVersion != null) document.SavedWithAppVersion = applicationVersion;
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(document, JsonOptions));
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true);
        else File.Move(temp, FilePath);
        _migrationSource = null;
        _upgradeSource = null;
    }
}
