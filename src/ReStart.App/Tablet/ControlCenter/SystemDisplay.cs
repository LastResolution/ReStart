using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ReStart.App.Tablet.ControlCenter;

/// <summary>Runs COM work on one background STA thread (WMI scripting objects are apartment-bound and slow enough to stutter the UI).</summary>
internal sealed class StaWorker
{
    private readonly BlockingCollection<Action> _queue = new();

    internal StaWorker(string name)
    {
        var thread = new Thread(() =>
        {
            foreach (var work in _queue.GetConsumingEnumerable())
            {
                // A failing COM call must never take the shell down with it.
                try { work(); }
                catch (Exception ex) { TabletLog.Write("cc", $"{name}: {ex.GetType().Name} {ex.Message}"); }
            }
        }) { IsBackground = true, Name = name };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    internal void Post(Action work) => _queue.Add(work);

    internal Task<T> Run<T>(Func<T> work)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() => { try { done.SetResult(work()); } catch (Exception ex) { done.SetException(ex); } });
        return done.Task;
    }
}

/// <summary>
/// Internal panel brightness through WMI (WmiMonitorBrightness), the same path as the
/// Quick Settings slider. Writes coalesce so a fast slider drag never queues stale values.
/// </summary>
internal static class Brightness
{
    private static readonly StaWorker Worker = new("ReStart CC brightness");
    private static dynamic? _services, _methods; // worker thread only
    private static int _pending = -1, _scheduled;

    /// <summary>Current brightness 0..100, or -1 when the panel exposes no WMI brightness.</summary>
    internal static Task<int> ReadAsync() => Worker.Run<int>(() =>
    {
        try { return Convert.ToInt32((object?)First("SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=TRUE", item => (object)item.Properties_.Item("CurrentBrightness").Value)); }
        catch (Exception ex) when (ex is COMException or InvalidCastException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        { TabletLog.Write("cc", $"brightness read failed: 0x{ex.HResult:X8} {ex.Message}"); Reset(); return -1; }
    });

    /// <summary>Requests a brightness (0..100); only the latest pending value is applied.</summary>
    internal static void Set(int percent)
    {
        Volatile.Write(ref _pending, Math.Clamp(percent, 0, 100));
        if (Interlocked.Exchange(ref _scheduled, 1) == 0) Worker.Post(Apply);
    }

    private static void Apply()
    {
        Volatile.Write(ref _scheduled, 0);
        var value = Interlocked.Exchange(ref _pending, -1);
        if (value < 0) return;
        try
        {
            _methods ??= First("SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=TRUE", item => item, release: false);
            object input = _methods!.Methods_.Item("WmiSetBrightness").InParameters.SpawnInstance_();
            try
            {
                ((dynamic)input).Properties_.Item("Timeout").Value = 0;
                ((dynamic)input).Properties_.Item("Brightness").Value = value;
                object? output = _methods.ExecMethod_("WmiSetBrightness", input); // null for this void method
                if (output != null) Marshal.FinalReleaseComObject(output);
            }
            finally { Marshal.FinalReleaseComObject(input); }
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        { TabletLog.Write("cc", $"brightness set failed: 0x{ex.HResult:X8} {ex.Message}"); Reset(); }
    }

    // Only the scripting API's static members are used through `dynamic`: WMI hands out
    // per-object DISPIDs for class properties/methods (item.CurrentBrightness), but the C#
    // COM binder caches DISPIDs per type library, so a second object would get a stale id
    // (E_FAIL). Queries run to completion and objects are released right away.
    private static dynamic? First(string query, Func<dynamic, dynamic> read, bool release = true)
    {
        object set = Services().ExecQuery(query, "WQL", 0);
        try
        {
            object item = ((dynamic)set).ItemIndex(0);
            try { return read(item); }
            finally { if (release) Marshal.FinalReleaseComObject(item); }
        }
        finally { Marshal.FinalReleaseComObject(set); }
    }

    private static dynamic Services()
    {
        if (_services != null) return _services;
        object locator = Activator.CreateInstance(Type.GetTypeFromProgID("WbemScripting.SWbemLocator", true)!)!;
        try { return _services = ((dynamic)locator).ConnectServer(".", @"root\wmi"); }
        finally { Marshal.FinalReleaseComObject(locator); }
    }

    private static void Reset()
    {
        if (_methods is object methods) Marshal.FinalReleaseComObject(methods);
        if (_services is object services) Marshal.FinalReleaseComObject(services);
        _methods = null; _services = null;
    }
}

/// <summary>Refresh rate of the built-in panel (60/180 Hz on the Z13).</summary>
internal static class RefreshRate
{
    /// <summary>GDI name (\\.\DISPLAYn) of the internal panel, or of the primary screen when no path reports an embedded connector.</summary>
    internal static string? InternalDisplay()
    {
        try
        {
            if (GetDisplayConfigBufferSizes(2 /* QDC_ONLY_ACTIVE_PATHS */, out var pathCount, out var modeCount) == 0)
            {
                var paths = new PathInfo[pathCount];
                var modes = new ModeInfo[modeCount];
                if (QueryDisplayConfig(2, ref pathCount, paths, ref modeCount, modes, 0) == 0)
                    for (var i = 0; i < pathCount; i++)
                    {
                        var tech = paths[i].TargetOutputTechnology;
                        if (tech != 0x80000000 /* INTERNAL */ && tech != 11 /* DISPLAYPORT_EMBEDDED */ && tech != 13 /* UDI_EMBEDDED */) continue;
                        var name = new SourceDeviceName { Type = 1, Size = Marshal.SizeOf<SourceDeviceName>(), AdapterLow = paths[i].SourceAdapterLow, AdapterHigh = paths[i].SourceAdapterHigh, Id = paths[i].SourceId };
                        if (DisplayConfigGetDeviceInfo(ref name) == 0) return name.GdiName;
                    }
            }
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException) { }
        return System.Windows.Forms.Screen.PrimaryScreen?.DeviceName;
    }

    /// <summary>Current refresh rate and the highest rate offered at the current resolution; (0, 0) when unknown.</summary>
    internal static (int Current, int Max, bool Has60) Read()
    {
        var display = InternalDisplay();
        if (display == null) return (0, 0, false);
        var mode = NewMode();
        if (!EnumDisplaySettings(display, -1 /* ENUM_CURRENT_SETTINGS */, ref mode)) return (0, 0, false);
        int max = 0; var has60 = false;
        var probe = NewMode();
        for (var i = 0; EnumDisplaySettings(display, i, ref probe); i++)
        {
            if (probe.PelsWidth != mode.PelsWidth || probe.PelsHeight != mode.PelsHeight || probe.BitsPerPel != mode.BitsPerPel) continue;
            max = Math.Max(max, probe.DisplayFrequency);
            has60 |= probe.DisplayFrequency is 59 or 60;
        }
        return (mode.DisplayFrequency, max, has60);
    }

    /// <summary>Switches the internal panel to <paramref name="hz"/>, keeping the current resolution and orientation.</summary>
    internal static bool Set(int hz)
    {
        var display = InternalDisplay();
        if (display == null) return false;
        var mode = NewMode();
        if (!EnumDisplaySettings(display, -1, ref mode)) return false;
        mode.DisplayFrequency = hz;
        mode.Fields |= 0x400000; // DM_DISPLAYFREQUENCY
        var result = ChangeDisplaySettingsEx(display, ref mode, 0, 0x1 /* CDS_UPDATEREGISTRY */, 0);
        if (result != 0) TabletLog.Write("cc", $"refresh {hz} Hz failed: {result}");
        return result == 0;
    }

    private static DevMode NewMode() => new() { Size = (short)Marshal.SizeOf<DevMode>(), DeviceName = "", FormName = "" };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        public short SpecVersion, DriverVersion, Size, DriverExtra;
        public int Fields, PositionX, PositionY, DisplayOrientation, DisplayFixedOutput;
        public short Color, Duplex, YResolution, TTOption, Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string FormName;
        public short LogPixels;
        public int BitsPerPel, PelsWidth, PelsHeight, DisplayFlags, DisplayFrequency;
        public int IcmMethod, IcmIntent, MediaType, DitherType, Reserved1, Reserved2, PanningWidth, PanningHeight;
    }

    // DISPLAYCONFIG_PATH_INFO flattened (72 bytes); only the fields read here are named.
    [StructLayout(LayoutKind.Sequential)]
    private struct PathInfo
    {
        public uint SourceAdapterLow; public int SourceAdapterHigh; public uint SourceId, SourceModeIndex, SourceStatus;
        public uint TargetAdapterLow; public int TargetAdapterHigh; public uint TargetId, TargetModeIndex, TargetOutputTechnology, TargetRotation, TargetScaling;
        public uint RefreshNumerator, RefreshDenominator, ScanLineOrdering; public int TargetAvailable; public uint TargetStatus, Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)] private struct ModeInfo { public uint InfoType; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SourceDeviceName
    {
        public int Type, Size; public uint AdapterLow; public int AdapterHigh; public uint Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string GdiName;
    }

    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out int paths, out int modes);
    [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref int pathCount, [Out] PathInfo[] paths, ref int modeCount, [Out] ModeInfo[] modes, nint topology);
    [DllImport("user32.dll")] private static extern int DisplayConfigGetDeviceInfo(ref SourceDeviceName request);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplaySettings(string device, int mode, ref DevMode devMode);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int ChangeDisplaySettingsEx(string device, ref DevMode devMode, nint window, uint flags, nint parameter);
}

/// <summary>System rotation lock (the Quick Settings "Rotation lock" toggle).</summary>
internal static class RotationLock
{
    private const int ArDisabled = 0x1, ArNoSensor = 0x10, ArNotSupported = 0x20, ArInactive = 0x4 | 0x8 | 0x40 | 0x80; // remote, multimon, docked, laptop

    /// <summary>(available, locked, applies-now). Unavailable without an orientation sensor or setter.</summary>
    internal static (bool Available, bool Locked, bool Active) Read()
    {
        if (!GetAutoRotationState(out var state) || (state & (ArNoSensor | ArNotSupported)) != 0 || !CanSet()) return (false, false, false);
        return (true, (state & ArDisabled) != 0, (state & ArInactive) == 0);
    }

    /// <summary>Locks (true) or unlocks auto-rotation.</summary>
    internal static bool SetLocked(bool locked)
    {
        try { return NtUserSetAutoRotation(locked ? 0 : 1) != 0; }
        catch (EntryPointNotFoundException) { return false; }
    }

    private static bool? _canSet;
    // user32's undocumented SetAutoRotation (ordinal 2507) is a bare jump to this named win32u
    // export; binding by name avoids trusting an ordinal across builds.
    private static bool CanSet() => _canSet ??= NativeLibrary.TryLoad("win32u.dll", out var module) && NativeLibrary.TryGetExport(module, "NtUserSetAutoRotation", out _);

    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetAutoRotationState(out int state);
    [DllImport("win32u.dll")] private static extern int NtUserSetAutoRotation(int enable);
}

/// <summary>Windows light/dark mode (apps + system), as the Personalization > Colors page sets it.</summary>
internal static class SystemTheme
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Whether the system (shell) uses the dark theme.</summary>
    internal static bool IsDark => !ThemeWindow.SystemIsLight;

    /// <summary>Switches apps and shell to dark or light and notifies running windows.</summary>
    internal static void SetDark(bool dark)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(Key))
        {
            key.SetValue("AppsUseLightTheme", dark ? 0 : 1, RegistryValueKind.DWord);
            key.SetValue("SystemUsesLightTheme", dark ? 0 : 1, RegistryValueKind.DWord);
        }
        // Broadcast off the UI thread: SendMessageTimeout walks every top-level window.
        _ = Task.Run(() =>
        {
            var text = Marshal.StringToHGlobalUni("ImmersiveColorSet");
            try { SendMessageTimeout(0xFFFF, 0x001A /* WM_SETTINGCHANGE */, 0, text, 0x2 /* SMTO_ABORTIFHUNG */, 200, out _); }
            finally { Marshal.FreeHGlobal(text); }
        });
    }

    [DllImport("user32.dll")] private static extern nint SendMessageTimeout(nint window, uint message, nuint wp, nint lp, uint flags, uint timeout, out nuint result);
}
