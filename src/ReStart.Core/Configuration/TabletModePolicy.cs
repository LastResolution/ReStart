namespace ReStart.Core;

/// <summary>Represents a current posture observation without device identities or historical state.</summary>
public enum DevicePosture
{
    /// <summary>No reliable current observation is available.</summary>
    Unknown,
    /// <summary>Windows reports a slate posture on a supported device.</summary>
    Slate,
    /// <summary>Windows reports a desktop/laptop posture.</summary>
    Desktop
}

/// <summary>Represents current USB/Bluetooth keyboard presence; unknown is distinct from absent.</summary>
public enum KeyboardPresence
{
    /// <summary>Enumeration failed or is unavailable.</summary>
    Unknown,
    /// <summary>A complete current enumeration found no matching keyboard.</summary>
    Absent,
    /// <summary>A currently present USB/Bluetooth keyboard was observed.</summary>
    Present
}

/// <summary>Contains anonymous device observations, not consent or persistent user preferences.</summary>
/// <param name="Posture">Current Windows posture; Unknown does not activate tablet integration.</param>
/// <param name="Keyboard">Current keyboard presence; Unknown never suppresses a known slate posture.</param>
public readonly record struct TabletInputSnapshot(DevicePosture Posture, KeyboardPresence Keyboard);

/// <summary>Resolves tablet mode independently of desktop shortcuts, layout seeding and temporary observations.</summary>
public static class TabletModePolicy
{
    /// <summary>Decides whether explicitly enabled tablet integration should be active without mutating preferences or layouts.</summary>
    /// <param name="preferences">Caller-owned persistent choices; never modified.</param>
    /// <param name="input">Current anonymous posture and keyboard observations.</param>
    /// <param name="temporaryPosture">Optional in-memory test posture; null follows Windows. It cannot grant consent or override an enabled keyboard policy.</param>
    /// <returns>True only with persistent opt-in and a selected slate posture not suppressed by a present keyboard.</returns>
    public static bool IsActive(Preferences preferences, TabletInputSnapshot input, DevicePosture? temporaryPosture = null)
        => preferences.TabletFeaturesEnabled && (temporaryPosture ?? input.Posture) == DevicePosture.Slate
            && !(preferences.TabletKeyboardMeansDesktop && input.Keyboard == KeyboardPresence.Present);
}

/// <summary>Owns reversible tablet effects; transitions never alter desktop preferences or stored layouts.</summary>
/// <param name="effects">Ordered acquisition factories, copied at construction; each returned lease must release only its own effect. Factories must clean up their own partial acquisition before throwing.</param>
public sealed class TabletSession(IEnumerable<Func<IDisposable>> effects) : IDisposable
{
    private readonly Func<IDisposable>[] _factories = effects.ToArray();
    private readonly List<IDisposable> _leases = [];
    private bool _disposed;
    /// <summary>Gets whether all tablet effects were acquired successfully and have not been released.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Applies a current policy decision, acquiring once on entry and releasing in reverse order on exit.</summary>
    /// <param name="preferences">Caller-owned persistent settings, read without mutation.</param>
    /// <param name="input">Current anonymous device observations.</param>
    /// <param name="temporaryPosture">Optional in-memory posture override that never grants consent; null follows the observation.</param>
    /// <returns>Whether active state changed; false for repeated application or a disposed session.</returns>
    /// <exception cref="AggregateException">Cleanup failed; every acquired lease is still offered disposal. An activation failure is included when both acquisition and cleanup fail.</exception>
    /// <exception cref="InvalidOperationException">An effect factory returns null. Other factory exceptions propagate after previously acquired effects are released.</exception>
    public bool Apply(Preferences preferences, TabletInputSnapshot input, DevicePosture? temporaryPosture = null)
    {
        if (_disposed) return false;
        var active = TabletModePolicy.IsActive(preferences, input, temporaryPosture);
        if (active == IsActive) return false;
        if (!active) { IsActive = false; Release(); return true; }
        try
        {
            foreach (var factory in _factories) _leases.Add(factory() ?? throw new InvalidOperationException("Missing tablet effect lease."));
            IsActive = true;
            return true;
        }
        catch (Exception activation)
        {
            try { Release(); }
            catch (AggregateException cleanup) { throw new AggregateException(activation, cleanup); }
            throw;
        }
    }

    private void Release()
    {
        List<Exception> failures = [];
        for (var index = _leases.Count - 1; index >= 0; index--)
            try { _leases[index].Dispose(); }
            catch (Exception error) { failures.Add(error); }
        _leases.Clear();
        if (failures.Count != 0) throw new AggregateException("Tablet cleanup failed.", failures);
    }

    /// <summary>Stops transitions and releases all acquired effects once; repeated calls are harmless.</summary>
    /// <exception cref="AggregateException">One or more effects could not be released; all remaining effects are still offered disposal.</exception>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; IsActive = false;
        Release();
    }
}
