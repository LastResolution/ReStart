namespace ReStart.Core;

public enum WindowsKeyAction { Pass, Suppress, Toggle, ReplayWindowsDown }

/// <summary>Separates a bare Windows key from Win+letter shortcuts without changing their order.</summary>
public sealed class WindowsKeyState
{
    private int _held;
    private bool _shortcut;
    private bool _replayed;

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

    public int HeldKey => _held;
}
