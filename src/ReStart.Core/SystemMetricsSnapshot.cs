namespace ReStart.Core;

/// <summary>Contains only aggregate, read-only host metrics. Null values denote unavailable or warming-up counters.</summary>
/// <param name="SampledAt">UTC sample timestamp.</param>
/// <param name="CpuPercent">System-wide processor-time percentage, from zero to one hundred.</param>
/// <param name="LogicalProcessors">Number of active logical processors across processor groups.</param>
/// <param name="TotalMemoryBytes">Total physical memory, when available.</param>
/// <param name="AvailableMemoryBytes">Available physical memory, when available.</param>
/// <param name="MemoryPercent">Used physical memory as a percentage.</param>
/// <param name="UptimeSeconds">Seconds since system startup.</param>
/// <param name="Drives">Capacity snapshots of ready fixed drives; labels and file contents are omitted.</param>
/// <param name="NetworkReceiveBytesPerSecond">Aggregate receive rate across active non-loopback, non-tunnel interfaces.</param>
/// <param name="NetworkSendBytesPerSecond">Aggregate send rate across active non-loopback, non-tunnel interfaces.</param>
/// <param name="BatteryPercent">Battery charge percentage, or null if no battery is reported.</param>
/// <param name="IsCharging">Whether the reported battery is charging.</param>
public sealed record SystemMetricsSnapshot(DateTimeOffset SampledAt, double? CpuPercent, int LogicalProcessors,
    ulong? TotalMemoryBytes, ulong? AvailableMemoryBytes, double? MemoryPercent, double UptimeSeconds,
    IReadOnlyList<SystemDriveMetrics>? Drives = null, double? NetworkReceiveBytesPerSecond = null, double? NetworkSendBytesPerSecond = null,
    double? BatteryPercent = null, bool? IsCharging = null);

/// <summary>Contains capacity data for one fixed local drive without volume labels or filenames.</summary>
/// <param name="Root">Drive root such as C:\.</param>
/// <param name="TotalBytes">Total capacity.</param>
/// <param name="AvailableBytes">Available space.</param>
/// <param name="UsedPercent">Used capacity percentage.</param>
public sealed record SystemDriveMetrics(string Root, long TotalBytes, long AvailableBytes, double UsedPercent);
