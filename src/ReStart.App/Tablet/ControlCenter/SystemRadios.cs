using System.Runtime.InteropServices;
using Windows.Devices.Radios;
using Windows.Networking.Connectivity;

namespace ReStart.App.Tablet.ControlCenter;

/// <summary>
/// Wi-Fi and Bluetooth radios (Windows.Devices.Radios, as Quick Settings uses) plus airplane
/// mode through the shell's Radio Management API. <see cref="Changed"/> fires on a worker thread.
/// </summary>
internal sealed class SystemRadios
{
    private Radio? _wifi, _bluetooth;
    private Task? _start;

    /// <summary>Raised when a radio, airplane mode or the connection changes (any thread).</summary>
    internal event Action? Changed;

    /// <summary>Wi-Fi radio state, or null without a Wi-Fi radio.</summary>
    internal bool? WifiOn => _wifi == null ? null : _wifi.State == RadioState.On;
    /// <summary>Bluetooth radio state, or null without Bluetooth.</summary>
    internal bool? BluetoothOn => _bluetooth == null ? null : _bluetooth.State == RadioState.On;

    /// <summary>Finds the radios once; later calls return the same task.</summary>
    internal Task StartAsync() => _start ??= StartCoreAsync();

    private async Task StartCoreAsync()
    {
        try
        {
            if (await Radio.RequestAccessAsync() != RadioAccessStatus.Allowed) { TabletLog.Write("cc", "radio access denied"); return; }
            foreach (var radio in await Radio.GetRadiosAsync())
            {
                if (radio.Kind == RadioKind.WiFi && _wifi == null) _wifi = radio;
                else if (radio.Kind == RadioKind.Bluetooth && _bluetooth == null) _bluetooth = radio;
                else continue;
                radio.StateChanged += (_, _) => Changed?.Invoke();
            }
            NetworkInformation.NetworkStatusChanged += _ => Changed?.Invoke();
        }
        catch (Exception ex) { TabletLog.Write("cc", "radios failed: " + ex.Message); }
    }

    /// <summary>Turns Wi-Fi on or off. Returns false when Windows refused.</summary>
    internal async Task<bool> SetWifiAsync(bool on) => _wifi != null && await _wifi.SetStateAsync(on ? RadioState.On : RadioState.Off) == RadioAccessStatus.Allowed;

    /// <summary>Turns Bluetooth on or off. Returns false when Windows refused.</summary>
    internal async Task<bool> SetBluetoothAsync(bool on) => _bluetooth != null && await _bluetooth.SetStateAsync(on ? RadioState.On : RadioState.Off) == RadioAccessStatus.Allowed;

    /// <summary>
    /// Name of the connected Wi-Fi network. Since Windows 11 24H2 the SSID needs location
    /// access, so this falls back to the connection profile name, then to null.
    /// </summary>
    internal static string? WifiNetwork()
    {
        try
        {
            foreach (var profile in NetworkInformation.GetConnectionProfiles())
            {
                if (!profile.IsWlanConnectionProfile || profile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.None) continue;
                string? ssid = null;
                try { ssid = profile.WlanConnectionProfileDetails?.GetConnectedSsid(); }
                catch (Exception ex) when (ex is UnauthorizedAccessException or COMException) { }
                return string.IsNullOrWhiteSpace(ssid) ? (string.IsNullOrWhiteSpace(profile.ProfileName) ? "Connected" : profile.ProfileName) : ssid;
            }
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException) { }
        return null;
    }

    /// <summary>Airplane mode state, or null when the Radio Management API is unavailable.</summary>
    internal static bool? AirplaneMode
    {
        get
        {
            var manager = RadioManager();
            if (manager == null) return null;
            try { return manager.GetSystemRadioState(out var enabled, out _, out _) == 0 ? enabled == 0 : null; }
            finally { Marshal.ReleaseComObject(manager); }
        }
    }

    /// <summary>Turns airplane mode on or off (all radios, like the Quick Settings tile).</summary>
    internal static bool SetAirplaneMode(bool on)
    {
        var manager = RadioManager();
        if (manager == null) return false;
        try { return manager.SetSystemRadioState(on ? 0 : 1) == 0; }
        finally { Marshal.ReleaseComObject(manager); }
    }

    private static IRadioManager? RadioManager()
    {
        try { return Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("581333F6-28DB-41BE-BC7A-FF201F12F3F6"), true)!) as IRadioManager; }
        catch (COMException ex) { TabletLog.Write("cc", "radio manager: " + ex.Message); return null; }
        catch (InvalidCastException ex) { TabletLog.Write("cc", "radio manager: " + ex.Message); return null; }
    }

    // Undocumented but long-stable shell interface behind the airplane-mode toggle (RMapi).
    [ComImport, Guid("DB3AFBFB-08E6-46C6-AA70-BF9A34C30AB7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IRadioManager
    {
        [PreserveSig] int IsRMSupported(out uint state);
        [PreserveSig] int GetUIRadioInstances([MarshalAs(UnmanagedType.IUnknown)] out object instances);
        [PreserveSig] int GetSystemRadioState(out int enabled, out int unknown, out int reason);
        [PreserveSig] int SetSystemRadioState(int enabled);
    }
}
