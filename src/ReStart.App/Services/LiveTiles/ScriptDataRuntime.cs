using System.Text.Json;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Owns the configured inputs for one installed package without exposing connections to its worker.</summary>
internal sealed class ScriptDataRuntime : IDisposable
{
    private readonly Dictionary<string, TileDataSource> _sources = [];
    internal string Signature { get; }
    internal event Action? Updated;
    internal bool IsLoading => _sources.Values.Any(s => s.Snapshot.Status == "loading");
    internal ScriptDataRuntime(string path, string grantPath, string tileId, string packageId, ScriptTileManifest manifest,
        Func<ScriptTileManifest> currentManifest, TileDataTransport transport)
    {
        Signature = JsonSerializer.Serialize(manifest.DataSources, ScriptTileProtocol.Json);
        var bindings = ScriptDataBindings.Read(path);
        var original = ScriptTileGrants.DataRequest(tileId, packageId, manifest, bindings);
        var broker = new TileIntegrationBroker(() =>
        {
            var request = ScriptTileGrants.DataRequest(tileId, packageId, currentManifest(), ScriptDataBindings.Read(path));
            // A runtime can use only the exact connection and definition it was
            // constructed with. Reapproval of a different connection requires a new runtime.
            var grant = request.RequestFingerprint == original.RequestFingerprint && request.ConnectionFingerprint == original.ConnectionFingerprint
                ? ScriptTileGrants.Read(grantPath) : null;
            return (request, grant);
        });
        foreach (var (id, definition) in manifest.DataSources)
        {
            var provider = new TileDataSource(transport, definition, bindings.GetValueOrDefault(id) ?? new(), 8192 / Math.Max(1, manifest.DataSources.Count),
                () => broker.Stamp("data.sources", id, "read"));
            provider.Updated += () => Updated?.Invoke();
            _sources[id] = provider;
        }
    }
    internal Dictionary<string, TileDataSnapshot> Snapshot(DateTimeOffset now, bool force = false)
    {
        Refresh(now, force);
        return _sources.ToDictionary(p => p.Key, p => p.Value.Snapshot);
    }
    internal void Refresh(DateTimeOffset now, bool force = false)
    { foreach (var provider in _sources.Values) _ = provider.RefreshAsync(now, force); }
    internal void Stop() { foreach (var provider in _sources.Values) provider.Stop(); }
    public void Dispose() { Updated = null; foreach (var provider in _sources.Values) provider.Dispose(); }
}
