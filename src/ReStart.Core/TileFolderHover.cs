namespace ReStart.Core;

/// <summary>Recognizes a continuous tile-over-tile hover with an explicit monotonic dwell duration.</summary>
public sealed class TileFolderHover
{
    private string? _source;
    private string? _target;
    private long _since;
    private int _delay;
    /// <summary>Duration of an uninterrupted eligible hover in milliseconds.</summary>
    public const int DelayMilliseconds = 1000;
    /// <summary>Short dwell for collecting independent tiles into an ordinary group.</summary>
    public const int GroupDelayMilliseconds = 300;
    /// <summary>Gets the currently tracked destination, allowing its hit region to retain a small movement tolerance.</summary>
    public string? TargetId => _target;

    /// <summary>Returns true after a stable eligible source/target pair has remained hovered for the required monotonic duration.</summary>
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
