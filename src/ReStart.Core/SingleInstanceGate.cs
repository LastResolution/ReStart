using System.Runtime.Versioning;

namespace ReStart.Core;

/// <summary>A process-lifetime lease independent of executable path and version.</summary>
[SupportedOSPlatform("windows")]
public sealed class SingleInstanceGate : IDisposable
{
    private readonly Mutex _lease;
    private readonly EventWaitHandle _activation;
    /// <summary>Gets whether this instance created the process-lifetime lease.</summary>
    public bool IsPrimary { get; }
    /// <summary>Gets the activation wait handle owned by this gate; callers must not dispose it separately.</summary>
    public WaitHandle Activation => _activation;

    /// <summary>Opens a user/session identity lease and its activation event; preview uses an independent namespace.</summary>
    public SingleInstanceGate(string identity, bool preview = false)
    {
        var suffix = identity + (preview ? ".Preview" : "");
        // Open the event first: early duplicate signals must survive until the primary listens.
        _activation = new(false, EventResetMode.AutoReset, "Local\\ReStart.Activate." + suffix);
        // Named-object lifetime is the lease; no thread-affine mutex ownership is needed.
        _lease = new(false, "Local\\ReStart." + suffix, out var created);
        IsPrimary = created;
    }

    /// <summary>Signals the primary to present its existing window. Background duplicates should not call this.</summary>
    public void NotifyExisting() => _activation.Set();
    /// <summary>Closes the lease and event. No thread-affine mutex release is required.</summary>
    public void Dispose() { _lease.Dispose(); _activation.Dispose(); }
}
