using System.Buffers.Binary;

namespace ReStart.Core;

/// <summary>Describes one in-memory observation of Windows app-launch usage; no document names or notification contents are included.</summary>
/// <param name="Launches">Observed launch count, bounded to one million; it is not Windows Start's private ranking score.</param>
/// <param name="LastLaunched">Validated UTC launch instant used to break frequency ties.</param>
public sealed record WindowsAppUsage(int Launches, DateTimeOffset LastLaunched);

/// <summary>Decodes only the known 72-byte Windows 7-and-later UserAssist record layout.</summary>
public static class UserAssistFormat
{
    /// <summary>Reads a bounded usage observation without retaining the source bytes.</summary>
    /// <param name="bytes">One registry binary value. Unknown layouts, zero counts and invalid timestamps are rejected.</param>
    /// <param name="now">Current instant; observations more than five minutes in the future are rejected.</param>
    /// <returns>A new usage observation, or null for missing, unsupported or invalid records.</returns>
    public static WindowsAppUsage? Read(ReadOnlySpan<byte> bytes, DateTimeOffset now)
    {
        if (bytes.Length != 72) return null;
        var launches = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        var stamp = BinaryPrimitives.ReadInt64LittleEndian(bytes[60..]);
        if (launches == 0 || stamp <= 0) return null;
        try
        {
            var last = DateTimeOffset.FromFileTime(stamp).ToUniversalTime();
            return last.Year < 2000 || last > now.AddMinutes(5) ? null : new((int)Math.Min(launches, 1_000_000), last);
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>Applies the reversible ASCII ROT13 transformation used for UserAssist registry value names.</summary>
    /// <param name="name">A known catalog identity or absolute app path; non-ASCII characters and punctuation are preserved.</param>
    /// <returns>A new transformed string; applying the operation twice restores the input.</returns>
    public static string TransformName(string name) => string.Concat(name.Select(c => c switch
    {
        >= 'A' and <= 'Z' => (char)('A' + (c - 'A' + 13) % 26),
        >= 'a' and <= 'z' => (char)('a' + (c - 'a' + 13) % 26),
        _ => c
    }));
}
