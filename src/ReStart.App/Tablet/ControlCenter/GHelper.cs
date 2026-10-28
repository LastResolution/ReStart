using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ReStart.App.Services;

namespace ReStart.App.Tablet.ControlCenter;

/// <summary>
/// Talks to G-Helper (github.com/seerge/g-helper), which owns the ASUS power/fan modes, the
/// panel refresh and the side button. G-Helper has no IPC: a second GHelper.exe kills the
/// running one and its config is never re-read from disk. Its documented global hotkeys are
/// the supported external interface, and they run G-Helper's own code paths, so its UI,
/// config and per-power-source memory stay in sync. The config file is only read, never written.
/// </summary>
internal static class GHelper
{
    /// <summary>G-Helper's mode ids (Modes.cs): 2 Silent, 0 Balanced, 1 Turbo.</summary>
    internal const int Silent = 2, Balanced = 0, Turbo = 1;
    private const ushort Control = 0x11, Shift = 0x10, Alt = 0x12, Win = 0x5B;
    private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GHelper", "config.json");

    /// <summary>Whether G-Helper is running in this session with its hotkeys enabled.</summary>
    internal static bool IsAvailable
    {
        get
        {
            var processes = Process.GetProcessesByName("GHelper");
            try
            {
                using var self = Process.GetCurrentProcess();
                return processes.Any(p => p.SessionId == self.SessionId) && Config() is { } config && Int(config, "skip_hotkeys") != 1;
            }
            finally { foreach (var p in processes) p.Dispose(); }
        }
    }

    /// <summary>Current performance mode id from G-Helper's config, or null.</summary>
    internal static int? PerformanceMode => Config() is { } config ? Int(config, "performance_mode") : null;

    /// <summary>Switches the performance mode with G-Helper's Ctrl+Shift+Alt+F16/F17/F18 hotkeys (or the user's rebinding).</summary>
    internal static bool SetPerformanceMode(int mode)
    {
        if (Config() is not { } config) return false;
        // InputDispatcher: keybind_profile_0..2 -> SetPerformanceMode(0..2); defaults F17, F18, F16.
        var key = mode switch { Balanced => Int(config, "keybind_profile_0") ?? 0x80, Turbo => Int(config, "keybind_profile_1") ?? 0x81, Silent => Int(config, "keybind_profile_2") ?? 0x7F, _ => 0 };
        return key != 0 && Send(Modifiers(config, "modifier_keybind_alt", [Control, Shift, Alt]), (ushort)key);
    }

    /// <summary>Toggles the panel between 60 Hz and its maximum (Ctrl+Shift+Alt+F13). G-Helper then records the manual choice.</summary>
    internal static bool ToggleRefreshRate() => Config() is { } config && Send(Modifiers(config, "modifier_keybind_alt", [Control, Shift, Alt]), 0x7C);

    /// <summary>Opens or hides the G-Helper window (Ctrl+Shift+F12): the same SettingsToggle as its "ghelper" key action.</summary>
    internal static bool ToggleWindow() => Config() is { } config && Send(Modifiers(config, "modifier_keybind", [Control, Shift]), (ushort)(Int(config, "keybind_app") ?? 0x7B));

    private static JsonElement? Config()
    {
        // G-Helper replaces the file atomically (File.Replace), so a plain read sees a whole version.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(ConfigPath));
                return document.RootElement.Clone();
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch (JsonException) { return null; }
            catch (IOException) { Thread.Sleep(30); }
            catch (UnauthorizedAccessException) { return null; }
        }
        return null;
    }

    // AppConfig.Get parses the value's text, so numbers stored as strings count too.
    private static int? Int(JsonElement config, string name) =>
        config.TryGetProperty(name, out var value) && int.TryParse(value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText(), out var number) ? number : null;

    // Same parsing as InputDispatcher.GetModifierKeys: "control-shift-alt" style, case-insensitive.
    private static ushort[] Modifiers(JsonElement config, string name, ushort[] fallback)
    {
        if (!config.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())) return fallback;
        var parts = new HashSet<string>(value.GetString()!.Split('-'), StringComparer.OrdinalIgnoreCase);
        var keys = new List<ushort>();
        if (parts.Contains("control")) keys.Add(Control);
        if (parts.Contains("shift")) keys.Add(Shift);
        if (parts.Contains("alt")) keys.Add(Alt);
        if (parts.Contains("win")) keys.Add(Win);
        return keys.ToArray();
    }

    private static bool Send(ushort[] modifiers, ushort key)
    {
        var inputs = new List<Native.Input>();
        foreach (var modifier in modifiers) inputs.Add(Key(modifier, false));
        inputs.Add(Key(key, false)); inputs.Add(Key(key, true));
        for (var i = modifiers.Length - 1; i >= 0; i--) inputs.Add(Key(modifiers[i], true));
        var sent = Native.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Native.Input>());
        TabletLog.Write("cc", $"g-helper hotkey {string.Join('+', modifiers.Select(m => $"{m:X2}"))}+{key:X2}: {sent}/{inputs.Count}");
        return sent == inputs.Count;
    }

    private static Native.Input Key(ushort vk, bool up) => new()
    {
        Type = 1, // INPUT_KEYBOARD
        Union = new() { Keyboard = new() { Vk = vk, Scan = (ushort)MapVirtualKey(vk, 0), Flags = up ? 0x2u : 0u } },
    };

    [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint code, uint mapType);
}
