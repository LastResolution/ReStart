using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using ReStart.App.Services;

namespace ReStart.App.Tablet.ControlCenter;

/// <summary>Battery snapshot for the status line and the battery module.</summary>
/// <param name="HasBattery">False on AC-only hardware.</param>
/// <param name="Percent">Charge 0..100.</param>
/// <param name="OnAc">Plugged in.</param>
/// <param name="Charging">Actively charging.</param>
/// <param name="Remaining">Time to empty on battery, or to full while charging; null when unknown.</param>
/// <param name="Saver">Energy saver on.</param>
internal sealed record BatteryState(bool HasBattery, int Percent, bool OnAc, bool Charging, TimeSpan? Remaining, bool Saver);

/// <summary>Power, lock, display power and the small system shortcuts used by Control Center and the side button.</summary>
internal static class SystemActions
{
    private static readonly Guid ConsoleDisplayState = new("6FE69556-704A-47A0-8F24-C28D936FDA47");
    private static HwndSource? _window;
    private static long _lastSideButton;

    /// <summary>Last console display state reported by Windows: 0 off, 1 on, 2 dimmed.</summary>
    internal static int DisplayState { get; private set; } = 1;

    /// <summary>Creates the hidden window that tracks display power and issues SC_MONITORPOWER.</summary>
    internal static void Initialize()
    {
        if (_window != null) return;
        _window = new HwndSource(new HwndSourceParameters("ReStartTabletPower") { WindowStyle = 0, Width = 0, Height = 0 });
        _window.AddHook(Hook);
        RegisterPowerSettingNotification(_window.Handle, ConsoleDisplayState, 0 /* DEVICE_NOTIFY_WINDOW_HANDLE */);
    }

    private static nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        // WM_POWERBROADCAST / PBT_POWERSETTINGCHANGE: POWERBROADCAST_SETTING { GUID; DWORD length; DWORD data }.
        if (msg == 0x0218 && wParam == 0x8013 && lParam != 0 && Marshal.PtrToStructure<Guid>(lParam) == ConsoleDisplayState)
        {
            DisplayState = Marshal.ReadInt32(lParam, 20);
            TabletLog.Write("power", "display " + DisplayState switch { 0 => "off", 1 => "on", _ => "dimmed" });
        }
        return 0;
    }

    /// <summary>Current battery state.</summary>
    internal static BatteryState Battery()
    {
        if (!GetSystemPowerStatus(out var status)) return new(false, 100, true, false, null, false);
        var hasBattery = status.BatteryFlag != 128 && status.BatteryFlag != 255;
        var percent = status.BatteryLifePercent <= 100 ? status.BatteryLifePercent : 100;
        var onAc = status.ACLineStatus == 1;
        var charging = (status.BatteryFlag & 8) != 0;
        TimeSpan? remaining = !onAc && status.BatteryLifeTime > 0 ? TimeSpan.FromSeconds(status.BatteryLifeTime) : null;
        if (charging)
        {
            try
            {
                var report = Windows.Devices.Power.Battery.AggregateBattery.GetReport();
                if (report.ChargeRateInMilliwatts is > 0 and var rate && report.FullChargeCapacityInMilliwattHours is { } full && report.RemainingCapacityInMilliwattHours is { } now && full > now)
                    remaining = TimeSpan.FromHours((full - now) / (double)rate);
                if (report.FullChargeCapacityInMilliwattHours is > 0 and var capacity && report.RemainingCapacityInMilliwattHours is { } left)
                    percent = (int)Math.Round(Math.Clamp(left * 100.0 / capacity, 0, 100));
            }
            catch (Exception ex) when (ex is COMException or UnauthorizedAccessException) { }
        }
        return new(hasBattery, percent, onAc, charging, remaining, status.SystemStatusFlag == 1);
    }

    /// <summary>Locks the session (Win+L).</summary>
    internal static void Lock() { TabletLog.Write("power", "lock"); Native.LockWorkStation(); }

    /// <summary>Turns the display off; any touch, key or the side button wakes it.</summary>
    internal static void ScreenOff()
    {
        Initialize();
        TabletLog.Write("power", "screen off");
        SendMessage(_window!.Handle, 0x0112 /* WM_SYSCOMMAND */, 0xF170 /* SC_MONITORPOWER */, 2);
    }

    /// <summary>Turns the display back on (side button while the screen is off).</summary>
    internal static void Wake()
    {
        TabletLog.Write("power", "wake");
        // A display-required execution state resets the display idle timer; a tiny relative
        // mouse move is the input Windows reliably treats as "user present". The move fails
        // harmlessly when the secure desktop (lock screen) owns input.
        SetThreadExecutionState(0x2 /* ES_DISPLAY_REQUIRED */);
        var move = new Native.Input[]
        {
            new() { Type = 0, Union = new() { Mouse = new() { X = 1, Flags = 0x1 /* MOUSEEVENTF_MOVE */ } } },
            new() { Type = 0, Union = new() { Mouse = new() { X = -1, Flags = 0x1 } } },
        };
        Native.SendInput((uint)move.Length, move, Marshal.SizeOf<Native.Input>());
    }

    /// <summary>
    /// The Z13 side button (G-Helper key "m4" bound to <c>ReStart.exe --tablet-command side-button</c>).
    /// Tablet: iPad side button - lock and turn the screen off, or wake it when it is off.
    /// Laptop: open/hide G-Helper exactly like G-Helper's own "ghelper" action.
    /// </summary>
    internal static void SideButton(Dispatcher dispatcher, string? argument)
    {
        var now = Environment.TickCount64;
        if (now - _lastSideButton < 600) return; // one press can arrive twice (WMI event + key repeat)
        _lastSideButton = now;
        var tablet = argument switch { "tablet" => true, "laptop" => false, _ => TabletShell.IsTablet };
        TabletLog.Write("power", $"side button ({(tablet ? "tablet" : "laptop")}, display {DisplayState})");
        if (!tablet) { if (!GHelper.ToggleWindow()) TabletLog.Write("power", "g-helper hotkey unavailable"); return; }
        if (DisplayState == 0) { Wake(); return; }
        TabletShell.CloseOverlays();
        Lock();
        // Let the switch to the lock screen finish first, or it lights the panel again.
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(700), DispatcherPriority.Normal, (s, _) => { ((DispatcherTimer)s!).Stop(); ScreenOff(); }, dispatcher);
        timer.Start();
    }

    /// <summary>Shows or hides the touch keyboard (TabTip's ITipInvocation, which the taskbar keyboard button uses).</summary>
    internal static async Task ToggleTouchKeyboardAsync()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var tip = (ITipInvocation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("4ce576fa-83dc-4F88-951c-9d0782b4e376"), true)!)!;
                try { tip.Toggle(GetDesktopWindow()); return; }
                finally { Marshal.ReleaseComObject(tip); }
            }
            catch (COMException) when (attempt == 0)
            {
                // The class is only registered while TabTip.exe runs.
                var tabTip = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), @"microsoft shared\ink\TabTip.exe");
                if (!File.Exists(tabTip)) return;
                Process.Start(new ProcessStartInfo(tabTip) { UseShellExecute = true })?.Dispose();
                await Task.Delay(500);
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException) { TabletLog.Write("cc", "touch keyboard: " + ex.Message); return; }
        }
    }

    /// <summary>Opens the Snipping Tool capture overlay.</summary>
    internal static void Screenshot() => Open("ms-screenclip:");

    /// <summary>Opens Settings.</summary>
    internal static void Settings() => Open("ms-settings:");

    /// <summary>Shell-executes a URI or path; failures are logged, never thrown.</summary>
    internal static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { TabletLog.Write("cc", $"open {target}: {ex.Message}"); }
    }

    [ComImport, Guid("37c994e7-432b-4834-a2f7-dce1f13b834b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITipInvocation { void Toggle(nint window); }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public int BatteryLifeTime, BatteryFullLifeTime; }

    [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
    [DllImport("kernel32.dll")] private static extern uint SetThreadExecutionState(uint flags);
    [DllImport("user32.dll")] private static extern nint RegisterPowerSettingNotification(nint recipient, in Guid setting, int flags);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern nint GetDesktopWindow();
}
