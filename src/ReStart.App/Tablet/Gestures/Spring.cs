using System.Windows.Media;

namespace ReStart.App.Tablet.Gestures;

/// <summary>
/// A damped spring in the style of iOS/SwiftUI: <c>response</c> is the period in seconds,
/// <c>damping</c> the damping fraction (1 = critically damped, below 1 = slight overshoot).
/// Values are driven by <see cref="FrameClock"/>; while a finger holds a value, set it directly.
/// </summary>
internal sealed class Spring(double value, double response = 0.38, double damping = 0.86)
{
    internal double Value = value, Velocity, Target = value;
    internal double Response = response, Damping = damping;

    internal bool Settled => Math.Abs(Value - Target) < 0.05 && Math.Abs(Velocity) < 0.05;

    /// <summary>Holds the value (a finger owns it); velocity is the tracking speed in units/s, for a natural release.</summary>
    internal void Set(double value, double velocity = 0) { Value = Target = value; Velocity = velocity; }
    internal void Snap(double value) { Value = Target = value; Velocity = 0; }

    internal void Step(double seconds)
    {
        if (Settled) { Value = Target; Velocity = 0; return; }
        var stiffness = Math.Pow(2 * Math.PI / Response, 2);
        var friction = 4 * Math.PI * Damping / Response;
        // Semi-implicit Euler in small slices stays stable for stiff, short springs.
        var remaining = seconds;
        while (remaining > 0)
        {
            var dt = Math.Min(remaining, 0.004);
            Velocity += (-stiffness * (Value - Target) - friction * Velocity) * dt;
            Value += Velocity * dt;
            remaining -= dt;
        }
    }
}

/// <summary>Per-frame callback on the WPF render clock, running only while someone needs frames.</summary>
internal sealed class FrameClock
{
    private readonly Func<double, bool> _tick;
    private TimeSpan _last;
    private bool _running;

    /// <param name="tick">Receives the elapsed seconds; returns true while more frames are needed.</param>
    internal FrameClock(Func<double, bool> tick) => _tick = tick;

    internal bool Running => _running;

    internal void Start()
    {
        if (_running) return;
        _running = true; _last = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
    }

    internal void Stop()
    {
        if (!_running) return;
        _running = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = e is RenderingEventArgs r ? r.RenderingTime : TimeSpan.FromMilliseconds(PointerInput.Now);
        if (now == _last) return; // Rendering can fire more than once per frame
        var dt = _last == TimeSpan.Zero ? 1 / 60.0 : Math.Clamp((now - _last).TotalSeconds, 0.001, 0.05);
        _last = now;
        var more = false;
        TabletNavigation.Guard("frame", () => more = _tick(dt));
        if (!more) Stop();
    }
}
