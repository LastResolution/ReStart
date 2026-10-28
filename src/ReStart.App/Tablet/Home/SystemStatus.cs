using System.Runtime.InteropServices;
using System.Text.Json;

namespace ReStart.App.Tablet.Home;

/// <summary>Battery, load and performance mode for the Battery and System tiles.</summary>
/// <param name="BatteryPercent">Charge 0–100, or null without a battery.</param>
/// <param name="Charging">Whether the battery is charging.</param>
/// <param name="PluggedIn">Whether AC power is connected (charging or full).</param>
/// <param name="SecondsLeft">Estimated runtime on battery, or null when unknown/plugged in.</param>
/// <param name="SecondsToFull">Estimated time to full while charging, or null.</param>
/// <param name="ChargeRateWatts">Signed battery power (positive = charging), or null.</param>
/// <param name="BatterySaver">Whether Windows energy saver is on.</param>
/// <param name="Cpu">Total CPU load 0–100, or null while warming up.</param>
/// <param name="Gpu">Busiest GPU 3D engine load 0–100, or null.</param>
/// <param name="Memory">Physical memory in use 0–100.</param>
/// <param name="PerformanceMode">G-Helper mode: silent, balanced, turbo, custom; null without G-Helper.</param>
internal sealed record SystemStatusSnapshot(int? BatteryPercent, bool Charging, bool PluggedIn, int? SecondsLeft, int? SecondsToFull,
    double? ChargeRateWatts, bool BatterySaver, double? Cpu, double? Gpu, double? Memory, string? PerformanceMode);

/// <summary>
/// Samples every two seconds on a background thread, only while a tile asked for data in the last
/// ten seconds. GPU load uses the language-neutral PDH "GPU Engine" counters (as Task Manager);
/// the performance mode is read (never written) from G-Helper's config.json.
/// </summary>
internal sealed class SystemStatus : IDisposable
{
    private static readonly string GHelperConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GHelper", "config.json");
    private readonly AutoResetEvent _wake = new(false);
    private volatile bool _disposed;
    private long _wantedUntil;
    private SystemStatusSnapshot? _latest;
    private Thread? _thread;
    private (DateTime Stamp, string? Mode) _ghelper;

    /// <summary>Raised on the sampling thread after a new snapshot.</summary>
    internal event Action? Updated;

    /// <summary>Latest snapshot (null before the first sample); keeps sampling for ten more seconds.</summary>
    internal SystemStatusSnapshot? Snapshot()
    {
        Volatile.Write(ref _wantedUntil, Environment.TickCount64 + 10000);
        if (_thread == null) { _thread = new Thread(Run) { IsBackground = true, Name = "ReStart tablet system status" }; _thread.Start(); }
        else _wake.Set();
        return Volatile.Read(ref _latest);
    }

    private void Run()
    {
        nint query = 0, cpu = 0, gpu = 0;
        try
        {
            if (PdhOpenQuery(null, 0, out query) == 0)
            {
                if (PdhAddEnglishCounter(query, @"\Processor Information(_Total)\% Processor Utility", 0, out cpu) != 0
                    && PdhAddEnglishCounter(query, @"\Processor Information(_Total)\% Processor Time", 0, out cpu) != 0) cpu = 0;
                if (PdhAddEnglishCounter(query, @"\GPU Engine(*engtype_3D)\Utilization Percentage", 0, out gpu) != 0) gpu = 0;
            }
            var warmed = false;
            while (!_disposed)
            {
                if (Environment.TickCount64 > Volatile.Read(ref _wantedUntil)) { warmed = false; _wake.WaitOne(); continue; }
                double? cpuLoad = null, gpuLoad = null;
                if (query != 0 && PdhCollectQueryData(query) == 0)
                {
                    if (warmed)
                    {
                        if (cpu != 0 && PdhGetFormattedCounterValue(cpu, 0x200, out _, out var value) == 0 && value.Status is 0 or 1 && double.IsFinite(value.Value))
                            cpuLoad = Math.Clamp(value.Value, 0, 100);
                        if (gpu != 0) gpuLoad = GpuLoad(gpu);
                    }
                    warmed = true;
                }
                var snapshot = Sample(cpuLoad, gpuLoad);
                Volatile.Write(ref _latest, snapshot);
                Updated?.Invoke();
                _wake.WaitOne(2000);
            }
        }
        catch (Exception ex) { TabletLog.Write("system", "sampler stopped: " + ex.Message); }
        finally { if (query != 0) PdhCloseQuery(query); }
    }

    private SystemStatusSnapshot Sample(double? cpu, double? gpu)
    {
        int? percent = null, secondsLeft = null, secondsToFull = null; bool charging = false, plugged = false, saver = false; double? rate = null;
        if (GetSystemPowerStatus(out var power))
        {
            plugged = power.AcLineStatus == 1;
            saver = power.SystemStatusFlag == 1;
            if (power.BatteryFlag != 255 && (power.BatteryFlag & 128) == 0)
            {
                percent = power.BatteryPercent <= 100 ? power.BatteryPercent : null;
                charging = (power.BatteryFlag & 8) != 0;
                if (!plugged && power.BatteryLifeTime != uint.MaxValue) secondsLeft = (int)Math.Min(int.MaxValue, power.BatteryLifeTime);
            }
        }
        try
        {
            // The aggregate battery report adds the charge rate, which gives time-to-full.
            var report = Windows.Devices.Power.Battery.AggregateBattery.GetReport();
            var mw = report.ChargeRateInMilliwatts;
            if (mw is int milliwatts) rate = milliwatts / 1000d;
            if (charging && mw > 0 && report.FullChargeCapacityInMilliwattHours is int full && report.RemainingCapacityInMilliwattHours is int left && full > left)
                secondsToFull = (int)((full - left) * 3600d / mw.Value);
            if (!plugged && secondsLeft == null && report.ChargeRateInMilliwatts is < 0 && report.RemainingCapacityInMilliwattHours is int remaining)
                secondsLeft = (int)(remaining * 3600d / -report.ChargeRateInMilliwatts.Value);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException) { }
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        double? used = GlobalMemoryStatusEx(ref memory) && memory.TotalPhysical > 0 ? Math.Round(100d * (1 - (double)memory.AvailablePhysical / memory.TotalPhysical), 1) : null;
        return new(percent, charging, plugged, secondsLeft, secondsToFull, rate, saver, cpu, gpu, used, PerformanceMode());
    }

    /// <summary>Task Manager's figure: per-engine utilisation summed over processes, busiest 3D engine wins.</summary>
    private static double? GpuLoad(nint counter)
    {
        uint size = 0;
        if (PdhGetFormattedCounterArray(counter, 0x200, ref size, out var count, 0) != unchecked((int)0x800007D2) || size == 0) return null;
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArray(counter, 0x200, ref size, out count, buffer) != 0) return null;
            var engines = new Dictionary<string, double>(StringComparer.Ordinal);
            var stride = Marshal.SizeOf<CounterItem>();
            for (var i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<CounterItem>(buffer + i * stride);
                if (item.Status is not (0 or 1) || !double.IsFinite(item.Value)) continue;
                // Instance: pid_1234_luid_0x..._phys_0_eng_0_engtype_3D → group by the engine, not the process.
                var name = Marshal.PtrToStringUni(item.Name) ?? "";
                var engine = name.IndexOf("_luid_", StringComparison.Ordinal) is var at and >= 0 ? name[at..] : name;
                engines[engine] = engines.GetValueOrDefault(engine) + item.Value;
            }
            return engines.Count == 0 ? 0 : Math.Clamp(engines.Values.Max(), 0, 100);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private string? PerformanceMode()
    {
        try
        {
            var info = new FileInfo(GHelperConfig);
            if (!info.Exists) return null;
            if (info.LastWriteTimeUtc == _ghelper.Stamp) return _ghelper.Mode;
            string? mode = null;
            using (var stream = new FileStream(GHelperConfig, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var document = JsonDocument.Parse(stream))
                if (document.RootElement.TryGetProperty("performance_mode", out var value) && value.TryGetInt32(out var number))
                    mode = number switch { 0 => "balanced", 1 => "turbo", 2 => "silent", _ => "custom" };
            _ghelper = (info.LastWriteTimeUtc, mode);
            return mode;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return _ghelper.Mode; }
    }

    public void Dispose() { _disposed = true; _wake.Set(); }

    [StructLayout(LayoutKind.Explicit)] private struct CounterValue { [FieldOffset(0)] internal uint Status; [FieldOffset(8)] internal double Value; }
    [StructLayout(LayoutKind.Explicit)] private struct CounterItem { [FieldOffset(0)] internal nint Name; [FieldOffset(8)] internal uint Status; [FieldOffset(16)] internal double Value; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        internal uint Length, MemoryLoad;
        internal ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus { internal byte AcLineStatus, BatteryFlag, BatteryPercent, SystemStatusFlag; internal uint BatteryLifeTime, BatteryFullLifeTime; }
    [DllImport("pdh.dll", EntryPoint = "PdhOpenQueryW", CharSet = CharSet.Unicode)] private static extern uint PdhOpenQuery(string? source, nuint data, out nint query);
    [DllImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", CharSet = CharSet.Unicode)] private static extern uint PdhAddEnglishCounter(nint query, string path, nuint data, out nint counter);
    [DllImport("pdh.dll")] private static extern uint PdhCollectQueryData(nint query);
    [DllImport("pdh.dll")] private static extern uint PdhGetFormattedCounterValue(nint counter, uint format, out uint type, out CounterValue value);
    [DllImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterArrayW", CharSet = CharSet.Unicode)] private static extern int PdhGetFormattedCounterArray(nint counter, uint format, ref uint size, out uint count, nint buffer);
    [DllImport("pdh.dll")] private static extern uint PdhCloseQuery(nint query);
    [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);
    [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemPowerStatus(out PowerStatus status);
}
