using System.Text.Json;

namespace ReStart.App.Services;

/// <summary>Persists one bounded, private recovery transaction without overwriting another owner or following links.</summary>
/// <param name="directory">Caller-selected owned settings directory; normal startup uses the local ReStart profile, fixtures use a fresh temporary directory.</param>
internal sealed class TaskbarRecoveryStore(string directory) : ITaskbarRecoveryStore
{
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 8 };
    internal string DirectoryPath { get; } = Path.GetFullPath(directory);
    internal string MarkerPath => Path.Combine(DirectoryPath, "taskbar-recovery.json");

    public IDisposable Acquire()
    {
        var path = Path.Combine(DirectoryPath, "taskbar-recovery.lock");
        CheckLinks(path); Directory.CreateDirectory(DirectoryPath); CheckLinks(path);
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public TaskbarLeaseState? Read()
    {
        CheckLinks(MarkerPath);
        if (!File.Exists(MarkerPath)) return null;
        if (new FileInfo(MarkerPath).Length > 32768) throw new InvalidDataException("Invalid taskbar recovery size.");
        try
        {
            var state = JsonSerializer.Deserialize<TaskbarLeaseState>(File.ReadAllBytes(MarkerPath), Json);
            if (state == null || !state.IsValid()) throw new InvalidDataException("Invalid taskbar recovery state.");
            return state;
        }
        catch (JsonException) { throw new InvalidDataException("Invalid taskbar recovery state."); }
    }

    public void Save(TaskbarLeaseState state)
    {
        if (!state.IsValid()) throw new InvalidDataException("Invalid taskbar recovery state.");
        CheckLinks(MarkerPath);
        if (File.Exists(MarkerPath)) throw new IOException("An existing taskbar transaction requires recovery.");
        Directory.CreateDirectory(DirectoryPath); CheckLinks(MarkerPath);
        var temporary = Path.Combine(DirectoryPath, "taskbar-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(state, Json);
            if (bytes.Length > 32768) throw new InvalidDataException("Invalid taskbar recovery size.");
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(bytes); output.Flush(flushToDisk: true); }
            File.Move(temporary, MarkerPath, overwrite: false);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }

    public void Delete(string token)
    {
        if (Read() is not { } state) return;
        if (state.Token != token) throw new InvalidDataException("Taskbar recovery ownership changed.");
        File.Delete(MarkerPath);
    }

    internal static void CheckLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked taskbar recovery path.");
    }
}
