using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ReStart.App.Services;

/// <summary>Opens the Windows-owned search interface through its documented global shortcut, without injecting query text or using the clipboard.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsSearchLauncher
{
    internal static bool IsAvailable() => Native.GetShellWindow() != 0;

    internal static Native.Input[] CreateInputs() =>
    [
        Key(0x5B, false), Key(0x53, false), Key(0x53, true), Key(0x5B, true)
    ];

    internal static bool TryOpen()
    {
        if (!IsAvailable() || new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(key => (Native.GetAsyncKeyState(key) & 0x8000) != 0)) return false;
        var inputs = CreateInputs(); var size = Marshal.SizeOf<Native.Input>();
        var sent = Native.SendInput((uint)inputs.Length, inputs, size);
        if (sent == inputs.Length) return true;
        // Release only this chord after a partial insertion; never type into an unknown foreground window.
        if (sent > 0) Native.SendInput(2, [Key(0x53, true), Key(0x5B, true)], size);
        return false;
    }

    private static Native.Input Key(ushort key, bool up) => new() { Type = 1, Union = new() { Keyboard = new() { Vk = key, Flags = up ? 2u : 0 } } };
}
