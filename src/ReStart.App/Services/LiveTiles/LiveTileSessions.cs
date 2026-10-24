using System.Text.Json;
using System.Windows.Media.Imaging;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Owns dispatcher-bound tile state, retry budgets, data readers and worker lifetime independently of the window.</summary>
internal sealed class LiveTileSessions : IDisposable
{
    internal sealed record Input(string Type, long UnixMs, double? X = null, double? Y = null);
    internal sealed class Runtime
    {
        internal int Attempts;
        internal bool Rendering;
        internal DateTimeOffset NextUpdate;
        internal JsonElement State = JsonSerializer.SerializeToElement(new { });
        internal Queue<Input> Inputs = new();
    }
    internal Dictionary<string, Runtime> Sessions { get; } = [];
    internal Dictionary<string, ScriptTileFrame> Frames { get; } = [];
    internal Dictionary<string, (string Package, ScriptDataRuntime Runtime)> Data { get; } = [];
    internal Dictionary<string, (string Package, SlideshowSource Source)> Slideshows { get; } = [];
    internal Dictionary<string, BitmapSource> Images { get; } = [];
    internal Dictionary<string, LiveTileRecovery> Recovery { get; } = [];
    internal HashSet<string> MetricTiles { get; } = [];
    internal ScriptTileWorkerPool? Workers;
    internal SystemMetricsSampler? Metrics;
    internal TileDataTransport? Transport;
    internal LiveTileRecovery Budget(string id)
    {
        if (!Recovery.TryGetValue(id, out var budget)) Recovery[id] = budget = new();
        return budget;
    }
    internal void Pause()
    {
        Workers?.Dispose(); Workers = null; Metrics?.SetEnabled(false);
        foreach (var data in Data.Values) data.Runtime.Stop();
        foreach (var session in Sessions.Values) session.NextUpdate = DateTimeOffset.MinValue;
    }
    internal void Prune(IReadOnlySet<string> ids)
    {
        foreach (var id in Data.Keys.Where(id => !ids.Contains(id)).ToArray()) { Data.Remove(id, out var data); data.Runtime.Dispose(); }
        foreach (var id in Sessions.Keys.Where(id => !ids.Contains(id)).ToArray()) Sessions.Remove(id);
        foreach (var id in Frames.Keys.Where(id => !ids.Contains(id)).ToArray()) Frames.Remove(id);
        foreach (var id in Recovery.Keys.Where(id => !ids.Contains(id)).ToArray()) Recovery.Remove(id);
        foreach (var id in Slideshows.Keys.Where(id => !ids.Contains(id)).ToArray()) Slideshows.Remove(id);
        foreach (var id in Images.Keys.Where(id => !ids.Contains(id)).ToArray()) Images.Remove(id);
        MetricTiles.RemoveWhere(id => !ids.Contains(id));
    }
    public void Dispose()
    {
        Pause(); foreach (var data in Data.Values) data.Runtime.Dispose(); Data.Clear();
        Transport?.Dispose(); Metrics?.Dispose(); Sessions.Clear(); Frames.Clear(); Recovery.Clear(); Slideshows.Clear(); Images.Clear(); MetricTiles.Clear();
    }
}
