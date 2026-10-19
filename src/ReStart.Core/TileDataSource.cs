using System.Net.Http;
using System.Text.Json;
using System.Xml;

namespace ReStart.Core;

/// <summary>Coalesces asynchronous reads, caches successful values and retains stale data during bounded retry backoff.</summary>
public sealed class TileDataSource : IDisposable
{
    private readonly TileDataTransport _transport;
    private readonly TileDataDefinition _definition;
    private readonly TileDataBinding _binding;
    private readonly int _budget;
    private readonly object _gate = new();
    private TileDataSnapshot _snapshot;
    private Task _pending = Task.CompletedTask;
    private CancellationTokenSource? _cancellation;
    private DateTimeOffset _next, _started = DateTimeOffset.MinValue;
    private string? _etag;
    private DateTimeOffset? _modified;
    private int _failures;
    private bool _disposed;
    /// <summary>Raised after a result is published; subscribers marshal UI work to their dispatcher.</summary>
    public event Action? Updated;
    /// <summary>Gets the latest immutable data-only view without blocking rendering.</summary>
    public TileDataSnapshot Snapshot => Volatile.Read(ref _snapshot);
    /// <summary>Creates a provider for one explicit connection and a share of the tile's 8 KiB data budget.</summary>
    public TileDataSource(TileDataTransport transport, TileDataDefinition definition, TileDataBinding binding, int budget = 8192)
    {
        if (!definition.IsValid() || !binding.IsValid() || budget is < 512 or > 8192) throw new ArgumentException("Invalid data source configuration.");
        _transport = transport; _definition = definition; _binding = binding; _budget = budget;
        _snapshot = new(binding.Enabled ? "loading" : binding.Location.Length == 0 ? "unconfigured" : "disabled", JsonSerializer.SerializeToElement<object?>(null), null, null, false, null);
    }
    /// <summary>Starts a due update, or shares an existing request. Rendering can read Snapshot immediately without awaiting it.</summary>
    public Task RefreshAsync(DateTimeOffset now, bool force = false)
    {
        lock (_gate)
        {
            if (_disposed || !_binding.Enabled || now < _next && !force || force && now - _started < TimeSpan.FromSeconds(5)) return Task.CompletedTask;
            if (!_pending.IsCompleted) return _pending;
            _started = now;
            _cancellation?.Dispose(); _cancellation = new();
            return _pending = Task.Run(() => FetchAsync(now, _cancellation.Token));
        }
    }
    private async Task FetchAsync(DateTimeOffset now, CancellationToken cancellation)
    {
        var interval = Math.Max(_binding.Access == "http" ? 30 : 1, _definition.RefreshSeconds);
        TileDataSnapshot result;
        try
        {
            var fetched = await _transport.ReadAsync(_definition, _binding, _budget, _etag, _modified, cancellation).ConfigureAwait(false);
            if (cancellation.IsCancellationRequested) return;
            if (fetched.NotModified && Snapshot.UpdatedAt == null) throw new TileDataTransport.Failure("http", 304);
            var value = fetched.NotModified ? Snapshot.Value : fetched.Value;
            _etag = fetched.ETag; _modified = fetched.Modified; _failures = 0;
            result = new("ready", value, now, now, false, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or JsonException or XmlException or OperationCanceledException
            or TileDataTransport.Failure or System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            if (cancellation.IsCancellationRequested) return;
            var error = ex is TileDataTransport.Failure failure ? failure.Error : new TileDataError(ex switch
            { OperationCanceledException => "timeout", HttpRequestException => "network", IOException or UnauthorizedAccessException => "io", _ => "format" });
            result = Snapshot with { Status = "error", CheckedAt = now, Stale = Snapshot.UpdatedAt != null, Error = error };
            _failures = Math.Min(8, _failures + 1);
            interval = Math.Min(3600, Math.Max(30, interval) * (1 << (_failures - 1)));
        }
        lock (_gate)
        {
            if (_disposed || cancellation.IsCancellationRequested) return;
            _next = now.AddSeconds(interval); Volatile.Write(ref _snapshot, result);
        }
        Updated?.Invoke();
    }
    /// <summary>Cancels an in-flight request when hidden, while preserving its last good value for reopening.</summary>
    public void Stop() { lock (_gate) _cancellation?.Cancel(); }
    /// <summary>Cancels pending work and detaches callbacks when a tile or connection is retired.</summary>
    public void Dispose() { lock (_gate) { _disposed = true; _cancellation?.Cancel(); Updated = null; } }
}
