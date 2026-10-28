namespace ReStart.App.Tablet;

/// <summary>Bounded plain-text log shared by the tablet features: %LOCALAPPDATA%\ReStart\tablet.log.</summary>
internal static class TabletLog
{
    private static readonly object Gate = new();
    internal static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReStart", "tablet.log");

    /// <summary>Appends one line. Never throws; logging must not break the shell.</summary>
    internal static void Write(string area, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 512 * 1024) File.Move(FilePath, FilePath + ".old", true);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{area}] {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
