using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    internal void AdoptRestoredProfile(LayoutDocument document)
    {
        _scriptPresentationGeneration++; PauseScripts(); RetireHtmlTiles(all: true);
        _liveSessions.Dispose(); _liveSessions = new();
        _scriptPackages = new(Path.GetDirectoryName(_store.FilePath)!);
        Document = document; _selectionPlanCache = null; _openFolder = null;
        _layoutHistory.Reset(document); RefreshLayout(); PreferencesChanged?.Invoke();
        SetStatus(L.T(K.BackupRestored));
    }
}
