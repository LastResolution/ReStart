using System.ComponentModel;
using System.Text.Json;

namespace ReStart.App.Services;

/// <summary>Chooses a temporary window-visibility effect; neither mode changes Windows auto-hide configuration.</summary>
internal enum TaskbarLeaseMode { Reveal, Hide }
/// <summary>Stores physical screen pixels in private recovery state only.</summary>
internal readonly record struct TaskbarBounds(int X, int Y, int Width, int Height);
/// <summary>Identifies the exact Explorer window and its original and intended geometry for conditional recovery.</summary>
internal sealed record TaskbarWindowState(long Window, int ProcessId, long ProcessStarted, bool Visible, bool AutoHide,
    TaskbarBounds Monitor, TaskbarBounds Original, TaskbarBounds Target);
/// <summary>Stores a private, process-bound recovery transaction; never sent to tiles or shared diagnostics.</summary>
internal sealed record TaskbarLeaseState(int Version, string Token, int ParentId, long ParentStarted, TaskbarLeaseMode Mode, TaskbarWindowState[] Windows)
{
    internal bool IsValid() => Version == 1 && Guid.TryParseExact(Token, "N", out _) && ParentId > 0 && ParentStarted > 0
        && Enum.IsDefined(Mode) && Windows is { Length: > 0 and <= 32 }
        && Windows.All(item => item != null && item.Window > 0 && item.ProcessId > 0 && item.ProcessStarted > 0
            && Valid(item.Monitor) && Valid(item.Original) && Valid(item.Target))
        && Windows.Select(item => item.Window).Distinct().Count() == Windows.Length;
    private static bool Valid(TaskbarBounds bounds) => bounds.Width is > 0 and <= 65536 && bounds.Height is > 0 and <= 65536
        && bounds.X is >= -262144 and <= 262144 && bounds.Y is >= -262144 and <= 262144;
}

/// <summary>Provides durable private recovery storage. Save must refuse to overwrite an existing transaction.</summary>
internal interface ITaskbarRecoveryStore
{
    IDisposable Acquire();
    TaskbarLeaseState? Read();
    void Save(TaskbarLeaseState state);
    void Delete(string token);
}
/// <summary>Provides scoped shell operations; Restore must revalidate identities and preserve changes no longer owned by the lease.</summary>
internal interface ITaskbarLeaseBackend
{
    TaskbarLeaseState? Capture(TaskbarLeaseMode mode);
    bool IsCurrent(TaskbarLeaseState state);
    void Apply(TaskbarLeaseState state);
    void Restore(TaskbarLeaseState state);
}

/// <summary>Requires verified recovery persistence and a ready watchdog before any taskbar mutation.</summary>
/// <param name="store">Private recovery store, owned by the caller.</param>
/// <param name="backend">Window-identity-scoped backend; tests supply an inert fake.</param>
/// <param name="protect">Starts a recovery watchdog and returns only after readiness; its disposable signals completion after successful restore.</param>
internal sealed class TaskbarLease(ITaskbarRecoveryStore store, ITaskbarLeaseBackend backend, Func<TaskbarLeaseState, IDisposable> protect) : IDisposable
{
    private TaskbarLeaseState? _state;
    private IDisposable? _protection;
    private bool _disposed;
    internal bool IsActive => _state != null;
    internal bool RecoveryFailed { get; private set; }
    internal TaskbarLeaseMode? Mode => _state?.Mode;

    /// <summary>Enforces an optional temporary effect; failure never bypasses recovery prerequisites.</summary>
    /// <param name="mode">Desired effect, or null to restore the original owned state.</param>
    /// <returns>True when the requested effect is active, or restoration completed; false for unavailable/failed acquisition or recovery.</returns>
    internal bool Synchronize(TaskbarLeaseMode? mode)
    {
        if (_disposed) return mode == null && _state == null;
        if (mode == null && _state == null) return true;
        try
        {
            using var ownership = store.Acquire();
            return SynchronizeLocked(mode);
        }
        catch (Exception error) when (Expected(error)) { RecoveryFailed = _state != null; return false; }
    }

    private bool SynchronizeLocked(TaskbarLeaseMode? mode)
    {
        try
        {
            if (_state != null && (_state.Mode != mode || !backend.IsCurrent(_state)))
                if (!Restore()) return false;
            if (mode == null) return true;
            if (_state == null)
            {
                var candidate = backend.Capture(mode.Value);
                if (candidate == null) return false;
                if (!candidate.IsValid()) throw new InvalidDataException("Invalid taskbar recovery snapshot.");
                store.Save(candidate);
                var saved = store.Read();
                if (saved == null || !saved.IsValid() || JsonSerializer.Serialize(candidate) != JsonSerializer.Serialize(saved))
                    throw new InvalidDataException("Taskbar recovery verification failed.");
                // Ownership starts after durable verification, before the first
                // external mutation. A partially failing Apply can then be undone.
                _state = candidate;
                _protection = protect(candidate) ?? throw new InvalidOperationException("Missing taskbar recovery watchdog.");
            }
            backend.Apply(_state);
            RecoveryFailed = false;
            return true;
        }
        catch (Exception error) when (Expected(error))
        {
            if (_state != null) Restore();
            return false;
        }
    }

    private bool Restore()
    {
        if (_state == null) return true;
        try
        {
            backend.Restore(_state);
            store.Delete(_state.Token);
            _state = null;
            _protection?.Dispose(); _protection = null;
            RecoveryFailed = false;
            return true;
        }
        catch (Exception error) when (Expected(error)) { RecoveryFailed = true; return false; }
    }

    internal static bool Expected(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or Win32Exception;

    /// <summary>Restores owned effects; failed recovery retains the marker and watchdog for another recovery attempt.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        Synchronize(null); _disposed = true;
    }
}
