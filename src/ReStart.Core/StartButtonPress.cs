namespace ReStart.Core;

/// <summary>Describes whether a Start-button mouse event is forwarded, consumed, or completes a click.</summary>
public enum StartButtonAction
{
    /// <summary>Leave the event to Windows.</summary>
    Forward,
    /// <summary>Consume the event belonging to a captured button press.</summary>
    Suppress,
    /// <summary>Consume the release and toggle the launcher.</summary>
    Toggle
}

/// <summary>Captures only complete left clicks on an identified Start button and cancels release outside its bounds.</summary>
public sealed class StartButtonPress
{
    private int _pressed;
    /// <summary>Gets whether a captured mouse-down still needs its matching mouse-up.</summary>
    public bool IsPressed => Volatile.Read(ref _pressed) != 0;
    /// <summary>Resolves one left-button event; changing preferences during a press never forwards an unmatched release.</summary>
    public StartButtonAction Process(bool down, bool inside, bool enabled)
    {
        if (down)
        {
            if (!inside || !enabled) return StartButtonAction.Forward;
            Volatile.Write(ref _pressed, 1); return StartButtonAction.Suppress;
        }
        if (Interlocked.Exchange(ref _pressed, 0) == 0) return StartButtonAction.Forward;
        return inside && enabled ? StartButtonAction.Toggle : StartButtonAction.Suppress;
    }
}
