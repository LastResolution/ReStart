using System.Collections.Concurrent;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Reuses four isolated workers across any number of tiles; every render still creates a fresh JavaScript realm.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal sealed class ScriptTileWorkerPool : IDisposable
{
    private readonly ScriptTileSession[] _workers;
    private readonly ConcurrentQueue<ScriptTileSession> _idle;
    private readonly SemaphoreSlim _slots = new(4, 4);
    private readonly CancellationTokenSource _stop = new();
    private readonly object _gate = new();
    private int _disposed;
    internal ScriptTileWorkerPool(string? executable = null)
    {
        _workers = Enumerable.Range(0, 4).Select(_ => new ScriptTileSession(executable)).ToArray();
        _idle = new(_workers);
    }

    internal async Task<(ScriptTileFrame? Frame, string? Error)> RenderAsync(string source, object context)
    {
        Task waiting;
        lock (_gate) { if (_disposed != 0) return (null, null); waiting = _slots.WaitAsync(_stop.Token); }
        try { await waiting; }
        catch (OperationCanceledException) { return (null, null); }
        if (!_idle.TryDequeue(out var worker)) { _slots.Release(); throw new InvalidOperationException("Missing tile worker slot."); }
        try { var frame = await worker.RenderAsync(source, context); return (frame, worker.Error); }
        finally { _idle.Enqueue(worker); _slots.Release(); }
    }

    public void Dispose()
    {
        lock (_gate) { if (_disposed != 0) return; _disposed = 1; _stop.Cancel(); }
        _ = Task.Run(async () =>
        {
            for (var i = 0; i < 4; i++) await _slots.WaitAsync();
            foreach (var worker in _workers) worker.Dispose();
            _slots.Dispose(); _stop.Dispose();
        });
    }
}
