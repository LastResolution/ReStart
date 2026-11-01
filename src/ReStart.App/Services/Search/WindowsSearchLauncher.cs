using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ReStart.App.Services;

/// <summary>Builds balanced native search keys and recognizes only known protected Windows Search executable locations.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsSearchLauncher
{
    internal static bool IsAvailable() => Native.GetShellWindow() != 0;

    internal static Native.Input[] CreateInputs() =>
    [
        Key(0x5B, false), Key(0x53, false), Key(0x53, true), Key(0x5B, true)
    ];

    internal static Native.Input[] CreateDismissInputs() => [Key(0x1B, false), Key(0x1B, true)];

    internal static bool IsSearchExecutable(string? path, string windowsDirectory)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(windowsDirectory)) return false;
        var packages = new[]
        {
            ("MicrosoftWindows.Client.CBS_cw5n1h2txyewy", "SearchHost.exe"),
            ("MicrosoftWindows.Client.Core_cw5n1h2txyewy", "SearchHost.exe"),
            ("Microsoft.Windows.Search_cw5n1h2txyewy", "SearchApp.exe"),
            ("Microsoft.Windows.Cortana_cw5n1h2txyewy", "SearchUI.exe")
        };
        return packages.Any(package => path.Equals(Path.Combine(windowsDirectory, "SystemApps", package.Item1, package.Item2), StringComparison.OrdinalIgnoreCase));
    }

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
