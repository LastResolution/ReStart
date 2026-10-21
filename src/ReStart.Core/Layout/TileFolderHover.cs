namespace ReStart.Core;

/// <summary>Recognizes a continuous tile-over-tile hover with an explicit monotonic dwell duration.</summary>
public sealed class TileFolderHover
{
    private string? _source;
    private string? _target;
    private long _since;
    private int _delay;
    /// <summary>Duration of an uninterrupted eligible hover in milliseconds.</summary>
    public const int DelayMilliseconds = 600;
    /// <summary>Short dwell for collecting independent tiles into an ordinary group.</summary>
    public const int GroupDelayMilliseconds = 300;
    /// <summary>Gets the currently tracked destination, allowing its hit region to retain a small movement tolerance.</summary>
    public string? TargetId => _target;

    /// <summary>Returns the fraction of the current dwell completed, without changing its source, target or deadline.</summary>
    /// <param name="timestamp">Monotonic milliseconds from the same clock used by Update; an earlier instant yields zero.</param>
    /// <returns>A value from zero to one, or zero when no eligible hover is active.</returns>
    public double Progress(long timestamp) => _target == null || _delay <= 0 ? 0 : Math.Clamp((timestamp - _since) / (double)_delay, 0, 1);

    /// <summary>Returns true after a stable eligible source/target pair has remained hovered for the required monotonic duration.</summary>
    /// <param name="source">Stable dragged tile ID.</param>
    /// <param name="target">Stable eligible target ID, or null to clear the hover.</param>
    /// <param name="timestamp">Monotonic elapsed time in milliseconds, from the same clock for successive calls.</param>
    /// <param name="delayMilliseconds">Positive required dwell duration in milliseconds; defaults to the folder delay.</param>
    /// <returns>Whether the unchanged source/target pair has reached its dwell deadline.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The dwell duration is zero or negative.</exception>
    public bool Update(string source, string? target, long timestamp, int delayMilliseconds = DelayMilliseconds)
    {
        if (delayMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(delayMilliseconds));
        if (target == null || source == target) { Reset(); return false; }
        if (_source != source || _target != target || _delay != delayMilliseconds || timestamp < _since)
        { _source = source; _target = target; _since = timestamp; _delay = delayMilliseconds; return false; }
        return timestamp - _since >= _delay;
    }

    /// <summary>Clears the dwell timer when a drag ends, is cancelled, or leaves an eligible tile.</summary>
    public void Reset() { _source = null; _target = null; _since = 0; _delay = 0; }
}
