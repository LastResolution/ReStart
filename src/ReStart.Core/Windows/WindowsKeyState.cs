namespace ReStart.Core;

/// <summary>Action requested from the native low-level keyboard adapter.</summary>
public enum WindowsKeyAction
{
    /// <summary>Forward the original event to Windows.</summary>
    Pass,
    /// <summary>Consume the physical Windows-key transition.</summary>
    Suppress,
    /// <summary>Consume the release and toggle the launcher.</summary>
    Toggle,
    /// <summary>Replay the suppressed Windows-key press before forwarding a shortcut key.</summary>
    ReplayWindowsDown
}

/// <summary>Separates a bare Windows key from Win+letter shortcuts without changing their order.</summary>
public sealed class WindowsKeyState
{
    private int _held;
    private bool _shortcut;
    private bool _replayed;

    /// <summary>Consumes a physical key transition and returns the hook action; a bare Windows key toggles only on release.</summary>
    /// <param name="virtualKey">Physical Windows virtual-key code, including left/right Windows keys.</param>
    /// <param name="down">True for a physical key-down transition; false for key-up.</param>
    /// <param name="modifierAlreadyHeld">Whether a shortcut modifier was already held when this Windows-key press began.</param>
    /// <returns>Native hook action to forward, suppress, toggle, or replay the Windows-key press.</returns>
    public WindowsKeyAction Process(int virtualKey, bool down, bool modifierAlreadyHeld = false)
    {
        if (virtualKey is 0x5B or 0x5C)
        {
            if (down)
            {
                if (_held != 0) return WindowsKeyAction.Suppress;
                _held = virtualKey;
                _shortcut = modifierAlreadyHeld;
                _replayed = modifierAlreadyHeld;
                return modifierAlreadyHeld ? WindowsKeyAction.Pass : WindowsKeyAction.Suppress;
            }
            if (_held != virtualKey) return WindowsKeyAction.Pass;
            _held = 0;
            return _shortcut ? WindowsKeyAction.Pass : WindowsKeyAction.Toggle;
        }
        if (_held != 0 && down && !_replayed)
        {
            _shortcut = true;
            _replayed = true;
            return WindowsKeyAction.ReplayWindowsDown;
        }
        return WindowsKeyAction.Pass;
    }

    /// <summary>Gets the suppressed Windows virtual-key code to replay before a shortcut, or zero.</summary>
    public int HeldKey => _held;
}
