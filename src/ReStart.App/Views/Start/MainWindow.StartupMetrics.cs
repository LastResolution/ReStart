using System.Diagnostics;

namespace ReStart.App;

public partial class MainWindow
{
    private readonly Stopwatch _startupWatch = Stopwatch.StartNew();
    private readonly Dictionary<string, long> _startupMilestones = [];
    private int _boardRenderCount;
    private void MarkStartup(string stage) => _startupMilestones.TryAdd(stage, _startupWatch.ElapsedMilliseconds);
    internal object StartupMetrics => new
    {
        milestones = _startupMilestones,
        boardRenders = _boardRenderCount,
        apps = _apps.Count,
        icons = _catalog.Icons.Count,
        iconCacheHits = _catalog.IconCacheHits,
        iconCacheMisses = _catalog.IconCacheMisses,
        scriptTiles = ScriptTileCount,
        scriptFrames = ScriptFrameCount,
        scriptAttempts = _scriptAttempts.Count,
        scriptRendering = _scriptSessions.Values.Count(s => s.Rendering),
        scriptPaused = _liveSessions.Recovery.Values.Count(b => b.Paused),
        htmlViews = _htmlTiles.Count,
        htmlReady = _htmlTiles.Values.Count(v => v.BrowserReady)
    };
}
