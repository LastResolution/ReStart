namespace ReStart.Core;

/// <summary>Recognizes a continuous one-second tile-over-tile hover without requiring modifier keys.</summary>
public sealed class TileFolderHover
{
    private string? _source;
    private string? _target;
    private long _since;
    /// <summary>Duration of an uninterrupted eligible hover in milliseconds.</summary>
    public const int DelayMilliseconds = 1000;

    /// <summary>Returns true after a stable eligible source/target pair has remained hovered for the required monotonic duration.</summary>
    public bool Update(string source, string? target, long timestamp)
    {
        if (target == null || source == target) { Reset(); return false; }
        if (_source != source || _target != target || timestamp < _since)
        { _source = source; _target = target; _since = timestamp; return false; }
        return timestamp - _since >= DelayMilliseconds;
    }

    /// <summary>Clears the dwell timer when a drag ends, is cancelled, or leaves an eligible tile.</summary>
    public void Reset() { _source = null; _target = null; _since = 0; }
}
