using System.Runtime.Versioning;

namespace ReStart.Core;

/// <summary>A process-lifetime lease independent of executable path and version.</summary>
[SupportedOSPlatform("windows")]
public sealed class SingleInstanceGate : IDisposable
{
    private readonly Mutex _lease;
    private readonly EventWaitHandle _activation;
    public bool IsPrimary { get; }
    public WaitHandle Activation => _activation;

    public SingleInstanceGate(string identity, bool preview = false)
    {
        var suffix = identity + (preview ? ".Preview" : "");
        // Open the event first: early duplicate signals must survive until the primary listens.
        _activation = new(false, EventResetMode.AutoReset, "Local\\ReStart.Activate." + suffix);
        // Named-object lifetime is the lease; no thread-affine mutex ownership is needed.
        _lease = new(false, "Local\\ReStart." + suffix, out var created);
        IsPrimary = created;
    }

    public void NotifyExisting() => _activation.Set();
    public void Dispose() { _lease.Dispose(); _activation.Dispose(); }
}
