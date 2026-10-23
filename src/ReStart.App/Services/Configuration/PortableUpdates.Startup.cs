using System.Diagnostics;

namespace ReStart.App.Services;

internal static partial class PortableUpdates
{
    /// <summary>Waits for startup confirmation without losing a marker written immediately before process exit.</summary>
    /// <param name="isAcknowledged">Non-null probe of the owned workspace's startup marker; callback failures propagate.</param>
    /// <param name="hasExited">Non-null replacement-process exit probe, evaluated after an unsuccessful marker read.</param>
    /// <param name="timeout">Maximum polling time; zero or a negative value performs only the final marker check.</param>
    /// <returns>True when startup was acknowledged, including just before observed exit or the final deadline check.</returns>
    internal static async Task<bool> WaitForAcknowledgementAsync(Func<bool> isAcknowledged, Func<bool> hasExited, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            if (isAcknowledged()) return true;
            // The child can write the marker and exit between these probes.
            // Observing exit therefore requires a final marker read.
            if (hasExited()) return isAcknowledged();
            var remaining = timeout - watch.Elapsed;
            if (remaining <= TimeSpan.Zero) break;
            await Task.Delay(remaining < TimeSpan.FromMilliseconds(100) ? remaining : TimeSpan.FromMilliseconds(100));
        }
        return isAcknowledged();
    }
}
