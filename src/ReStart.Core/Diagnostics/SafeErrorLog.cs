using System.Globalization;
using System.Security;
using System.Text;

namespace ReStart.Core;

/// <summary>Writes bounded, local error categories without exception messages, stack traces, paths or user data.</summary>
public static class SafeErrorLog
{
    /// <summary>Maximum bytes in each log; at most three generations are retained.</summary>
    public const int MaximumBytes = 64 * 1024;
    private const string Header = "ReStart diagnostic log v1\n";
    private static readonly object Gate = new();

    /// <summary>Removes the old unstructured log on upgrade; inaccessible or linked files are left untouched.</summary>
    /// <param name="directory">App-owned local data directory, never an exported or remote destination.</param>
    public static void Prepare(string directory) => Write(directory, null);

    /// <summary>Appends only UTC time, a fixed category and numeric HRESULT; logging failures never escape.</summary>
    /// <param name="directory">App-owned local data directory; only error.log and two numbered generations are managed.</param>
    /// <param name="error">Optional failure to classify; null only removes a legacy unstructured current log.</param>
    public static void Write(string directory, Exception? error)
    {
        lock (Gate)
            try
            {
                for (var parent = Path.GetFullPath(directory); !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
                    if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0) return;
                Directory.CreateDirectory(directory);
                var paths = Enumerable.Range(0, 3).Select(i => Path.Combine(directory, i == 0 ? "error.log" : $"error.log.{i}")).ToArray();
                if (paths.Any(p => File.Exists(p) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)) return;
                foreach (var path in paths.Where(File.Exists))
                {
                    using var input = File.OpenRead(path);
                    var prefix = new byte[Encoding.UTF8.GetByteCount(Header)];
                    var safe = input.Length <= MaximumBytes && input.Read(prefix) == prefix.Length && Encoding.UTF8.GetString(prefix) == Header;
                    input.Dispose();
                    if (!safe) File.Delete(path);
                }
                if (error == null) return;
                var category = error switch
                {
                    UnauthorizedAccessException or SecurityException => "access",
                    IOException => "io",
                    OutOfMemoryException => "memory",
                    ArgumentException => "argument",
                    InvalidOperationException => "state",
                    _ => "unhandled"
                };
                var line = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " " + category + " " + error.HResult.ToString("X8", CultureInfo.InvariantCulture) + "\n";
                if (File.Exists(paths[0]) && new FileInfo(paths[0]).Length + Encoding.UTF8.GetByteCount(line) > MaximumBytes)
                {
                    if (File.Exists(paths[1])) File.Move(paths[1], paths[2], true);
                    File.Move(paths[0], paths[1], true);
                }
                if (!File.Exists(paths[0])) File.WriteAllText(paths[0], Header, new UTF8Encoding(false));
                File.AppendAllText(paths[0], line, new UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException) { }
    }
}
