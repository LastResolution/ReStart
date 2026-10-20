using System.Text.Json;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private AppActivity? _activity;
    private string ActivityPath => Path.Combine(Path.GetDirectoryName(_store.FilePath)!, "activity.json");
    private AppActivity Activity
    {
        get
        {
            if (_activity != null) return _activity;
            try { _activity = File.Exists(ActivityPath) && new FileInfo(ActivityPath).Length < 2_000_000 ? JsonSerializer.Deserialize<AppActivity>(File.ReadAllText(ActivityPath)) : null; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
            _activity ??= new();
            _activity.Entries = (_activity.Entries ?? []).Where(e => !string.IsNullOrWhiteSpace(e.Key) && e.Key.Length <= 4096 && e.Value != null)
                .Take(4096).ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
            return _activity;
        }
    }
    private void SaveActivity()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ActivityPath)!);
            File.WriteAllText(ActivityPath + ".tmp", JsonSerializer.Serialize(Activity));
            File.Move(ActivityPath + ".tmp", ActivityPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
    internal void ClearAppHistory() { Activity.Clear(); SaveActivity(); RenderApps(); SetStatus(L.T(K.HistoryCleared)); }
}
