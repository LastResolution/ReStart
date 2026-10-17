using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Net.NetworkInformation;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Samples one language-neutral system CPU counter and physical memory outside the UI and script workers.</summary>
[SupportedOSPlatform("windows")]
internal sealed class SystemMetricsSampler : IDisposable
{
    private readonly ManualResetEvent _stop = new(false);
    private readonly AutoResetEvent _changed = new(false);
    private volatile bool _enabled = true;
    private int _disposed;
    private SystemMetricsSnapshot? _latest;
    internal SystemMetricsSnapshot? Latest => Volatile.Read(ref _latest);

    internal SystemMetricsSampler() => new Thread(Watch) { IsBackground = true, Name = "ReStart aggregate system metrics" }.Start();

    internal void SetEnabled(bool enabled)
    {
        if (Volatile.Read(ref _disposed) != 0 || enabled == _enabled) return;
        _enabled = enabled; Volatile.Write(ref _latest, null); _changed.Set();
    }

    private void Watch()
    {
        nint query = 0, counter = 0;
        (long Received, long Sent, long At)? previousNetwork = null;
        Task<SystemDriveMetrics[]>? driveTask = null;
        SystemDriveMetrics[] drives = [];
        var driveSampleAt = long.MinValue / 2;
        try
        {
            if (PdhOpenQuery(null, 0, out query) == 0 && PdhAddEnglishCounter(query, @"\Processor Information(_Total)\% Processor Time", 0, out counter) != 0)
            { PdhCloseQuery(query); query = 0; }
            var warmed = false;
            while (!_stop.WaitOne(0))
            {
                if (_enabled)
                {
                    double? cpu = null;
                    if (query != 0 && PdhCollectQueryData(query) == 0)
                    {
                        if (warmed && PdhGetFormattedCounterValue(counter, 0x200, out _, out var value) == 0 && value.Status is 0 or 1 && double.IsFinite(value.Value))
                            cpu = Math.Clamp(value.Value, 0, 100);
                        warmed = true;
                    }
                    var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
                    var available = GlobalMemoryStatusEx(ref memory) && memory.TotalPhysical > 0;
                    var processors = GetActiveProcessorCount(0xffff);
                    double? receiveRate = null, sendRate = null;
                    try
                    {
                        var network = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)).Select(n => n.GetIPStatistics()).ToArray();
                        var received = network.Sum(n => n.BytesReceived); var sent = network.Sum(n => n.BytesSent); var at = Environment.TickCount64;
                        if (previousNetwork is { } last && at > last.At && received >= last.Received && sent >= last.Sent)
                        { receiveRate = (received - last.Received) * 1000d / (at - last.At); sendRate = (sent - last.Sent) * 1000d / (at - last.At); }
                        previousNetwork = (received, sent, at);
                    }
                    catch (NetworkInformationException) { previousNetwork = null; }
                    if (driveTask?.IsCompleted == true) { if (driveTask.IsCompletedSuccessfully) drives = driveTask.Result; else _ = driveTask.Exception; driveTask = null; }
                    if (driveTask == null && Environment.TickCount64 - driveSampleAt >= 30000)
                    { driveSampleAt = Environment.TickCount64; driveTask = Task.Run(ReadDrives); }
                    var batteryAvailable = GetSystemPowerStatus(out var power) && power.BatteryFlag != 255 && (power.BatteryFlag & 128) == 0;
                    var snapshot = new SystemMetricsSnapshot(DateTimeOffset.UtcNow, cpu, processors > 0 ? (int)processors : Environment.ProcessorCount,
                        available ? memory.TotalPhysical : null, available ? memory.AvailablePhysical : null,
                        available ? Math.Clamp(100d * (1 - (double)memory.AvailablePhysical / memory.TotalPhysical), 0, 100) : null,
                        Environment.TickCount64 / 1000d, drives, receiveRate, sendRate,
                        batteryAvailable && power.BatteryPercent <= 100 ? power.BatteryPercent : null, batteryAvailable ? (power.BatteryFlag & 8) != 0 : null);
                    if (_enabled) Volatile.Write(ref _latest, snapshot);
                }
                else { warmed = false; previousNetwork = null; }
                if (WaitHandle.WaitAny([_stop, _changed], 1000) == 0) return;
            }
        }
        finally { if (query != 0) PdhCloseQuery(query); _stop.Dispose(); _changed.Dispose(); }
    }

    public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) { _enabled = false; _stop.Set(); } }

    private static SystemDriveMetrics[] ReadDrives()
    {
        var result = new List<SystemDriveMetrics>();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).Take(16))
            try
            {
                if (!drive.IsReady || drive.TotalSize <= 0) continue;
                var total = drive.TotalSize; var available = drive.AvailableFreeSpace;
                result.Add(new(drive.Name, total, available, Math.Clamp((total - available) * 100d / total, 0, 100)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return result.ToArray();
    }

    [StructLayout(LayoutKind.Explicit)] private struct CounterValue { [FieldOffset(0)] internal uint Status; [FieldOffset(8)] internal double Value; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        internal uint Length, MemoryLoad;
        internal ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [DllImport("pdh.dll", EntryPoint = "PdhOpenQueryW", CharSet = CharSet.Unicode)] private static extern uint PdhOpenQuery(string? source, nuint data, out nint query);
    [DllImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", CharSet = CharSet.Unicode)] private static extern uint PdhAddEnglishCounter(nint query, string path, nuint data, out nint counter);
    [DllImport("pdh.dll")] private static extern uint PdhCollectQueryData(nint query);
    [DllImport("pdh.dll")] private static extern uint PdhGetFormattedCounterValue(nint counter, uint format, out uint type, out CounterValue value);
    [DllImport("pdh.dll")] private static extern uint PdhCloseQuery(nint query);
    [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);
    [DllImport("kernel32.dll")] private static extern uint GetActiveProcessorCount(ushort group);
    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus
    { internal byte AcLineStatus, BatteryFlag, BatteryPercent, SystemStatusFlag; internal uint BatteryLifeTime, BatteryFullLifeTime; }
    [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemPowerStatus(out PowerStatus status);
}
