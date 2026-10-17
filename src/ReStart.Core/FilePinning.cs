namespace ReStart.Core;

/// <summary>Creates launchable pins for existing filesystem objects without resolving or executing their contents.</summary>
public static class FilePinning
{
    /// <summary>Normalizes an absolute file, directory, or shortcut path and preserves shortcut arguments by pinning the shortcut itself.</summary>
    public static AppEntry FromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 32767 || path.Any(c => char.IsControl(c) || c == '"') || !Path.IsPathFullyQualified(path))
            throw new ArgumentException(L.T(K.ErrorPinPath), nameof(path));
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var directory = Directory.Exists(full);
        if (!directory && !File.Exists(full)) throw new FileNotFoundException(L.T(K.ErrorPinMissing));
        var name = Path.GetFileName(full);
        if (name.Length == 0) name = full;
        if (!directory && Path.GetExtension(full).ToLowerInvariant() is ".lnk" or ".url") name = Path.GetFileNameWithoutExtension(full);
        return new(full, name, full, false);
    }
}
