namespace ReStart.Core;

/// <summary>Creates launchable pins for existing filesystem objects without resolving or executing their contents.</summary>
public static class FilePinning
{
    /// <summary>Normalizes an absolute file, directory, or shortcut path and preserves shortcut arguments by pinning the shortcut itself.</summary>
    /// <param name="path">Fully qualified existing filesystem path, without quotes or control characters.</param>
    /// <returns>A launch entry retaining the file, directory, or shortcut itself as its target.</returns>
    /// <exception cref="ArgumentException">The path is not fully qualified or contains unsupported quoting, control characters, or excessive length.</exception>
    /// <exception cref="FileNotFoundException">The target file or directory does not exist.</exception>
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
