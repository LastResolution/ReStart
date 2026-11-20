using System.Windows.Automation;

namespace ReStart.App.Services;

/// <summary>Reads Start-button accessibility bounds outside the input hook, scoped to actual Explorer taskbars.</summary>
internal sealed class StartButtonLocator : IDisposable
{
    private StartButtonTarget[] _targets = [];
    private readonly object _state = new();
    private readonly ManualResetEvent _stop = new(false);
    private readonly AutoResetEvent _refresh = new(false);
    private readonly Thread _worker;
    private readonly Func<IReadOnlyList<TaskbarIdentity>> _enumerate;
    private readonly Func<nint, AutomationElement>? _createRoot;
    private bool _enabled;
    private int _generation;
    private bool _disposed;

    internal StartButtonLocator(Func<IReadOnlyList<TaskbarIdentity>>? enumerate = null, Func<nint, AutomationElement>? createRoot = null)
    {
        _enumerate = enumerate ?? StartButtonDiscovery.EnumerateTaskbars;
        _createRoot = createRoot;
        _worker = new Thread(Watch) { IsBackground = true, Name = "ReStart Start-button discovery" };
        _worker.SetApartmentState(ApartmentState.MTA);
        _worker.Start();
    }

    internal void SetEnabled(bool enabled)
    {
        lock (_state)
        {
            if (_disposed || _enabled == enabled) return;
            _enabled = enabled;
            _generation++;
            Volatile.Write(ref _targets, []);
            _refresh.Set();
        }
    }

    internal IReadOnlyList<StartButtonTarget> Targets => Volatile.Read(ref _targets);

    /// <summary>Matches only a recent observed rectangle belonging to the taskbar under the pointer; no coordinates are guessed.</summary>
    internal bool Contains(Native.Point point)
    {
        var window = Native.WindowFromPoint(point);
        var root = Native.GetAncestor(window, 2);
        var now = Environment.TickCount64;
        return Targets.Any(t => t.Taskbar == root && now - t.ReadAt <= 1500 && t.Bounds.Contains(point.X, point.Y));
    }

    private void Watch()
    {
        var discovery = new StartButtonDiscovery(_createRoot);
        var generation = -1;
        try
        {
            while (!_stop.WaitOne(0))
            {
                bool enabled;
                int observedGeneration;
                lock (_state) { enabled = _enabled; observedGeneration = _generation; }
                if (generation != observedGeneration)
                {
                    discovery.Clear();
                    generation = observedGeneration;
                }
                if (enabled)
                {
                    var targets = discovery.Read(_enumerate());
                    // A disabled/re-enabled or disposed locator must never publish
                    // the result of a scan started in an earlier settings lifetime.
                    lock (_state)
                        if (!_disposed && _enabled && _generation == generation) Volatile.Write(ref _targets, targets);
                }
                if (WaitHandle.WaitAny([_stop, _refresh], 500) == 0) return;
            }
        }
        finally
        {
            discovery.Clear();
            lock (_state) { _stop.Dispose(); _refresh.Dispose(); }
        }
    }

    public void Dispose()
    {
        lock (_state)
        {
            if (_disposed) return;
            _disposed = true;
            _enabled = false;
            _generation++;
            Volatile.Write(ref _targets, []);
            _stop.Set();
        }
    }
}
