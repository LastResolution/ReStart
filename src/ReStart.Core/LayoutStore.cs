using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReStart.Core;

public sealed class LayoutStore(string filePath)
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        MaxDepth = 16
    };

    public string FilePath { get; } = Path.GetFullPath(filePath);
    public string? RecoveryMessage { get; private set; }
    public bool IsReadOnly { get; private set; }

    public LayoutDocument? Load()
    {
        RecoveryMessage = null;
        IsReadOnly = false;
        if (!File.Exists(FilePath)) return null;
        try { return Read(FilePath); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or NullReferenceException)
        {
            // A future-version file must never be silently overwritten by an older binary.
            IsReadOnly = ex is InvalidDataException && ex.Message.Contains(L.T("未対応"), StringComparison.Ordinal);
            RecoveryMessage = L.F("レイアウトを読み込めませんでした。元のファイルは保持されています。{0}", ex.Message);
            if (!IsReadOnly) File.Copy(FilePath, FilePath + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}", false);
            if (!IsReadOnly && File.Exists(FilePath + ".bak"))
            {
                try { var backup = Read(FilePath + ".bak"); RecoveryMessage += L.T(" バックアップを復元しました。"); return backup; }
                catch (Exception backupEx) when (backupEx is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or NullReferenceException) { }
            }
            return null;
        }
    }

    public static LayoutDocument Read(string path)
    {
        if (new FileInfo(path).Length > 5 * 1024 * 1024) throw new InvalidDataException(L.T("レイアウトが5MBを超えています。"));
        var doc = JsonSerializer.Deserialize<LayoutDocument>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException(L.T("レイアウトが空です。"));
        doc.Normalize();
        return doc;
    }

    public void Save(LayoutDocument document)
    {
        if (IsReadOnly) throw new InvalidOperationException(L.T("新しい形式のレイアウトは保護されています。"));
        document.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(document, JsonOptions));
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true);
        else File.Move(temp, FilePath);
    }
}
