namespace ReStart.App.Tablet.Design;

/// <summary>
/// Counts the frames of one transition and logs a <c>[perf]</c> line when it ends:
/// frame count, achieved rate against the panel's, and the longest frame (a hitch).
/// Allocation-free per frame.
/// </summary>
internal sealed class FrameMeter(string area)
{
    private string? _what;
    private int _frames;
    private double _total, _worst;

    /// <summary>Starts measuring a transition (ends the previous one if it was still running).</summary>
    internal void Begin(string what)
    {
        End();
        _what = what; _frames = 0; _total = 0; _worst = 0;
    }

    /// <summary>One rendered frame, <paramref name="dt"/> seconds after the previous one.</summary>
    internal void Frame(double dt)
    {
        if (_what == null) return;
        if (_frames > 0) { _total += dt; _worst = Math.Max(_worst, dt); } // the first dt is a guess
        _frames++;
    }

    /// <summary>Logs the transition, if one was being measured.</summary>
    internal void End()
    {
        if (_what == null) return;
        var what = _what; _what = null;
        if (_frames < 3 || _total <= 0) return;
        TabletLog.Write("perf", $"{area} {what}: {_frames} frames in {_total * 1000:0} ms = {(_frames - 1) / _total:0} fps (panel {TabletMotion.FrameRate} Hz), worst frame {_worst * 1000:0.0} ms");
    }
}
