namespace ReStart.Core;

/// <summary>Bounds automatic retries for one tile; state is transient and never changes the saved layout.</summary>
public sealed class LiveTileRecovery
{
    private long _windowStart = long.MinValue;
    private int _failures;
    /// <summary>Gets whether repeated failures require an explicit Reload.</summary>
    public bool Paused { get; private set; }
    /// <summary>Gets the earliest retry time in monotonic milliseconds.</summary>
    public long RetryAt { get; private set; }
    /// <summary>Records one independent failed attempt, with two bounded retries before pausing.</summary>
    /// <param name="now">Current monotonic uptime in milliseconds; wall-clock changes do not affect retries.</param>
    /// <param name="automatic">Whether automatic recovery is enabled; false pauses immediately.</param>
    public void Fail(long now, bool automatic)
    {
        if (_windowStart == long.MinValue || now - _windowStart > 120_000) { _failures = 0; _windowStart = now; }
        _failures++;
        Paused = !automatic || _failures >= 3;
        RetryAt = Paused ? long.MaxValue : now + (_failures == 1 ? 2000 : 10000);
    }
    /// <summary>Checks whether a new render or browser recreation is allowed.</summary>
    /// <param name="now">Current monotonic uptime in milliseconds.</param>
    /// <returns>False while paused or before the retry deadline; true otherwise.</returns>
    public bool CanRun(long now) => !Paused && now >= RetryAt;
}
