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
    private readonly Func<string?> _authorization;
    private string? _approval;
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
    /// <summary>Gets the latest immutable data-only view after checking the host approval callback; revoked approvals clear cached data.</summary>
    public TileDataSnapshot Snapshot
    {
        get
        {
            if (_binding.Enabled)
            {
                var stamp = Permission();
                if (stamp == null || _approval != null && stamp != _approval)
                    lock (_gate) { Deny(); }
            }
            return Volatile.Read(ref _snapshot);
        }
    }
    /// <summary>Creates a provider for one explicit connection and a share of the tile's 8 KiB data budget.</summary>
    /// <param name="transport">Shared transport used for authorized reads; the source does not own its lifetime.</param>
    /// <param name="definition">Validated package data-source declaration with format, selector and refresh interval.</param>
    /// <param name="binding">Explicit user-selected local connection, including enabled state and any protected credential.</param>
    /// <param name="budget">Maximum data budget in bytes, from 512 through 8192 for this source.</param>
    /// <param name="authorization">Host-owned, thread-safe current-approval callback, called from rendering and I/O threads. Null means the trusted caller directly authorizes its explicit binding; package hosts must supply a callback. Return null to deny, or an opaque approval revision that changes on reapproval. No revision is exposed to tile contexts.</param>
    /// <exception cref="ArgumentException">The source definition, local binding, or byte budget is invalid.</exception>
    public TileDataSource(TileDataTransport transport, TileDataDefinition definition, TileDataBinding binding, int budget = 8192, Func<string?>? authorization = null)
    {
        if (!definition.IsValid() || !binding.IsValid() || budget is < 512 or > 8192) throw new ArgumentException("Invalid data source configuration.");
        _transport = transport; _definition = definition; _binding = binding; _budget = budget; _authorization = authorization ?? (() => "host");
        _snapshot = new(binding.Enabled ? "loading" : binding.Location.Length == 0 ? "unconfigured" : "disabled", JsonSerializer.SerializeToElement<object?>(null), null, null, false, null);
    }
    /// <summary>Starts a due update, or shares an existing request. Rendering can read Snapshot immediately without awaiting it.</summary>
    /// <param name="now">Current host time used for due checks, freshness metadata and retry scheduling.</param>
    /// <param name="force">Whether to bypass the normal interval; forced reads still have a five-second minimum spacing.</param>
    /// <returns>A shared pending refresh task, or a completed task when disabled, disposed, throttled, or not yet due.</returns>
    public Task RefreshAsync(DateTimeOffset now, bool force = false)
    {
        lock (_gate)
        {
            if (_disposed || !_binding.Enabled) return Task.CompletedTask;
            var approval = Permission();
            if (approval == null) { Deny(); return Task.CompletedTask; }
            if (_approval != approval)
            {
                _cancellation?.Cancel(); _next = DateTimeOffset.MinValue; _etag = null; _modified = null; _failures = 0;
                _approval = approval;
                _snapshot = new("loading", JsonSerializer.SerializeToElement<object?>(null), null, null, false, null);
            }
            if (_disposed || !_binding.Enabled || now < _next && !force || force && now - _started < TimeSpan.FromSeconds(5)) return Task.CompletedTask;
            if (!_pending.IsCompleted) return _pending;
            _started = now;
            _cancellation?.Dispose(); _cancellation = new();
            var cancellation = _cancellation.Token;
            return _pending = Task.Run(() => FetchAsync(now, approval, cancellation));
        }
    }
    private async Task FetchAsync(DateTimeOffset now, string approval, CancellationToken cancellation)
    {
        var interval = Math.Max(_binding.Access == "http" ? 30 : 1, _definition.RefreshSeconds);
        TileDataSnapshot result;
        try
        {
            var fetched = await _transport.ReadAsync(_definition, _binding, _budget, _etag, _modified, cancellation, () => Permission() == approval).ConfigureAwait(false);
            if (cancellation.IsCancellationRequested) return;
            if (Permission() != approval) { lock (_gate) Deny(); return; }
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
            result = error.Code == "permission" ? Denied() : Snapshot with { Status = "error", CheckedAt = now, Stale = Snapshot.UpdatedAt != null, Error = error };
            _failures = Math.Min(8, _failures + 1);
            interval = Math.Min(3600, Math.Max(30, interval) * (1 << (_failures - 1)));
        }
        lock (_gate)
        {
            if (_disposed || cancellation.IsCancellationRequested) return;
            if (Permission() != approval) { Deny(); return; }
            _next = now.AddSeconds(interval); Volatile.Write(ref _snapshot, result);
        }
        Updated?.Invoke();
    }
    private string? Permission()
    {
        try { return _authorization(); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or System.Security.Cryptography.CryptographicException) { return null; }
    }
    private static TileDataSnapshot Denied() => new("permission", JsonSerializer.SerializeToElement<object?>(null), null, null, false, new("permission"));
    private void Deny()
    {
        _cancellation?.Cancel(); _approval = null; _etag = null; _modified = null; _next = DateTimeOffset.MinValue;
        Volatile.Write(ref _snapshot, Denied());
    }
    /// <summary>Cancels an in-flight request when hidden, while preserving its last good value for reopening.</summary>
    public void Stop() { lock (_gate) _cancellation?.Cancel(); }
    /// <summary>Cancels pending work and detaches callbacks when a tile or connection is retired.</summary>
    public void Dispose() { lock (_gate) { _disposed = true; _cancellation?.Cancel(); Updated = null; } }
}
