using System.Text.Json;
using ReStart.Core;
using ReStart.App.Services;

namespace ReStart.App;

public partial class MainWindow
{
    private AppActivity? _activity;
    private IReadOnlyDictionary<string, WindowsAppUsage> _windowsUsage = new Dictionary<string, WindowsAppUsage>();
    private CancellationTokenSource? _windowsUsageCancellation;
    private long _windowsUsageNextRead;
    private Func<AppEntry[], CancellationToken, Task<WindowsUsageHistory.Snapshot>>? _windowsUsageFixture;

    private void RefreshWindowsUsage()
    {
        if (_preview && _windowsUsageFixture == null || _exit || !Document.Preferences.ShowMostUsed || !Document.Preferences.UseWindowsAppHistory)
        { StopWindowsUsage(); return; }
        if (_apps.Count == 0 || _windowsUsageCancellation != null || Environment.TickCount64 < _windowsUsageNextRead) return;
        _windowsUsageNextRead = Environment.TickCount64 + 60_000;
        var cancellation = new CancellationTokenSource(); _windowsUsageCancellation = cancellation;
        _ = LoadWindowsUsageAsync(_apps.ToArray(), cancellation);
    }

    private async Task LoadWindowsUsageAsync(AppEntry[] apps, CancellationTokenSource cancellation)
    {
        try
        {
            var snapshot = await (_windowsUsageFixture?.Invoke(apps, cancellation.Token) ?? WindowsUsageHistory.ReadAsync(apps, cancellation.Token));
            cancellation.Token.ThrowIfCancellationRequested();
            if (_exit || !Document.Preferences.ShowMostUsed || !Document.Preferences.UseWindowsAppHistory) return;
            _windowsUsage = snapshot.Entries;
            RenderApps();
            RefreshSearchResults();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { _windowsUsage = new Dictionary<string, WindowsAppUsage>(); }
        finally
        {
            if (_windowsUsageCancellation == cancellation) _windowsUsageCancellation = null;
            cancellation.Dispose();
        }
    }

    private void StopWindowsUsage()
    {
        _windowsUsageCancellation?.Cancel(); _windowsUsageCancellation = null;
        _windowsUsage = new Dictionary<string, WindowsAppUsage>(); _windowsUsageNextRead = 0;
    }
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
                .DistinctBy(e => e.Key, StringComparer.OrdinalIgnoreCase).Take(4096).ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
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
    internal void ClearAppHistory() { Activity.Clear(); SaveActivity(); RenderApps(); RefreshSearchResults(); SetStatus(L.T(K.HistoryCleared)); }
}
