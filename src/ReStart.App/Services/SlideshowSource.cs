using System.Text.Json;
using System.Windows.Media.Imaging;

namespace ReStart.App.Services;

/// <summary>Loads a bounded, non-recursive image selection from an explicitly chosen folder, outside the UI and JavaScript workers.</summary>
internal sealed class SlideshowSource(string bindingPath)
{
    internal sealed record Snapshot(BitmapSource? Image, int Count, int Index);
    internal event Action? Updated;
    private Snapshot _latest = new(null, 0, 0);
    private Task? _pending;
    private string? _loadedPath;
    private DateTime _loadedAt;
    private long _loadedLength;
    private string? _directory;
    private DateTime _directoryAt;
    private string[] _files = [];
    private readonly object _indexGate = new();
    private long? _manualAt;
    private int _manualIndex;

    internal void Advance(DateTimeOffset now)
    {
        lock (_indexGate) { _manualAt = now.ToUnixTimeSeconds(); _manualIndex = (_latest.Index + 1) % Math.Max(1, _latest.Count); }
    }

    internal Snapshot Refresh(DateTimeOffset now, int intervalSeconds)
    {
        if (_pending == null || _pending.IsCompleted)
            _pending = Task.Run(() => Load(now, Math.Clamp(intervalSeconds, 5, 3600)));
        return Volatile.Read(ref _latest);
    }

    internal static void Bind(string bindingPath, string directory)
    {
        if (!Path.IsPathFullyQualified(directory) || directory.Length > 32767 || !Directory.Exists(directory)) throw new ArgumentException("Choose an existing image folder.", nameof(directory));
        var temporary = bindingPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(new { folder = Path.GetFullPath(directory) })); File.Move(temporary, bindingPath, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private void Load(DateTimeOffset now, int interval)
    {
        try
        {
            if (!File.Exists(bindingPath) || new FileInfo(bindingPath).Length > 256 * 1024) { Publish(new(null, 0, 0)); return; }
            using var json = JsonDocument.Parse(File.ReadAllText(bindingPath), new JsonDocumentOptions { MaxDepth = 3 });
            var directory = json.RootElement.GetProperty("folder").GetString();
            if (directory == null || !Path.IsPathFullyQualified(directory) || !Directory.Exists(directory)) { Publish(new(null, 0, 0)); return; }
            var written = Directory.GetLastWriteTimeUtc(directory);
            if (_directory != directory || written != _directoryAt)
            {
                _files = Directory.EnumerateFiles(directory, "*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })
                    .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif")
                    .Take(1000).Order(StringComparer.OrdinalIgnoreCase).ToArray();
                _directory = directory; _directoryAt = written;
            }
            if (_files.Length == 0) { Publish(new(null, 0, 0)); return; }
            int index;
            lock (_indexGate) index = _manualAt.HasValue
                ? (int)((_manualIndex + Math.Max(0, now.ToUnixTimeSeconds() - _manualAt.Value) / interval) % _files.Length)
                : (int)((now.ToUnixTimeSeconds() / interval) % _files.Length);
            var path = _files[index]; var info = new FileInfo(path);
            if (!info.Exists || info.Length > 20 * 1024 * 1024) { Publish(new(null, _files.Length, index)); return; }
            if (_loadedPath == path && _loadedAt == info.LastWriteTimeUtc && _loadedLength == info.Length && _latest.Image != null)
            { if (_latest.Count != _files.Length || _latest.Index != index) Publish(new(_latest.Image, _files.Length, index)); return; }
            using var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (input.Length > 20 * 1024 * 1024) return;
            var bytes = new byte[(int)input.Length]; input.ReadExactly(bytes);
            using var stream = new MemoryStream(bytes, false);
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 768; image.StreamSource = stream; image.EndInit(); image.Freeze();
            _loadedPath = path; _loadedAt = info.LastWriteTimeUtc; _loadedLength = info.Length;
            Publish(new(image, _files.Length, index));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException)
        { if (_latest.Image == null) Publish(new(null, 0, 0)); }
    }

    private void Publish(Snapshot snapshot) { Volatile.Write(ref _latest, snapshot); Updated?.Invoke(); }
}
