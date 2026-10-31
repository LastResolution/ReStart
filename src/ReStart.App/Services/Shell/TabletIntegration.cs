using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using ReStart.Core;
using Drawing = System.Drawing;

namespace ReStart.App.Services;

/// <summary>Serializes opt-in tablet effects and launcher-scoped taskbar work off the UI thread. Desktop Start hooks remain independent.</summary>
internal sealed class TabletIntegration : IDisposable
{
    /// <summary>Supplies every external effect together, so owned verification cannot accidentally fall back to a real shell operation.</summary>
    /// <param name="Store">Private or owned-fixture recovery storage.</param>
    /// <param name="Backend">Real identity-validated backend or inert fixture backend.</param>
    /// <param name="SetScope">Sets only the current launcher monitor on the supplied backend.</param>
    /// <param name="Protect">Returns only after crash recovery is ready; fixtures use an inert lease.</param>
    /// <param name="ReadInput">Fresh anonymous posture/keyboard reader.</param>
    /// <param name="Recover">Attempts startup recovery without touching any live owner.</param>
    internal sealed record Dependencies(ITaskbarRecoveryStore Store, ITaskbarLeaseBackend Backend, Action<TaskbarBounds> SetScope,
        Func<TaskbarLeaseState, IDisposable> Protect, Func<TabletInputSnapshot> ReadInput, Func<bool> Recover);
    private sealed record Request(bool Open, bool Enabled, bool KeyboardPolicy, bool Hide, bool Reveal, TaskbarBounds Monitor, int DeviceRevision);
    private sealed class Release(Action release) : IDisposable { public void Dispose() => release(); }
    private readonly MainWindow _window;
    private readonly HwndSource _source;
    private readonly DispatcherTimer _timer;
    private readonly Dependencies _dependencies;
    private readonly TaskbarLease _lease;
    private readonly TabletSession _tablet;
    private readonly uint _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private Request? _wanted;
    private TaskbarLeaseState? _published;
    private TabletInputSnapshot _input;
    private long _sampledAt;
    private int _sampledRevision = -1, _deviceRevision;
    private bool _busy, _recovered;
    private volatile bool _disposed;
    internal bool IsBusy => _busy;
    internal TaskbarLeaseMode? CurrentMode => _published?.Mode;
    internal bool TabletActive => _tablet.IsActive;

    /// <summary>Attaches short native event hooks and a coalesced background worker; construction does not mutate the shell.</summary>
    /// <param name="window">UI-owned launcher whose saved preferences provide consent and whose current monitor scopes every effect.</param>
    /// <param name="source">Existing launcher presentation source; hooks are removed on disposal.</param>
    internal TabletIntegration(MainWindow window, HwndSource source) : this(window, source, NativeDependencies()) { }

    /// <summary>Initializes the identical UI lifecycle with explicitly supplied external dependencies for owned verification.</summary>
    /// <param name="window">Owned launcher fixture or normal launcher; its preferences are never mutated.</param>
    /// <param name="source">Existing presentation source belonging to the same UI thread.</param>
    /// <param name="dependencies">Complete dependency set; no native backend, data directory or helper is created implicitly.</param>
    internal TabletIntegration(MainWindow window, HwndSource source, Dependencies dependencies)
    {
        _window = window; _source = source;
        _dependencies = dependencies;
        _lease = new(dependencies.Store, dependencies.Backend, dependencies.Protect);
        _tablet = new([() => new Release(() => { if (_lease.Mode == TaskbarLeaseMode.Hide) _lease.Synchronize(null); })]);
        _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(350) };
        _timer.Tick += (_, _) => Refresh();
        _source.AddHook(OnMessage); _window.NativeWorkingArea = WorkingArea;
        _timer.Start(); Refresh();
    }

    private static Dependencies NativeDependencies()
    {
        var store = new TaskbarRecoveryStore(TaskbarWatchdog.Profile);
        var backend = new TaskbarNativeBackend();
        var watchdog = new TaskbarWatchdog(store);
        return new(store, backend, value => backend.Scope = value, watchdog.Protect, TabletInputDevices.Read,
            () => TaskbarRecovery.Run(store, backend, TaskbarWatchdog.IsAlive));
    }

    /// <summary>Invalidates the input sample after a device event; reading and effects remain on the serialized worker.</summary>
    internal void DeviceStateChanged() { if (!_disposed) { _deviceRevision++; Refresh(); } }

    /// <summary>Queues the latest saved choices and monitor; previews never call this method with draft preferences.</summary>
    internal async void Refresh()
    {
        if (_disposed) return;
        var preferences = _window.Document.Preferences;
        var monitor = System.Windows.Forms.Screen.FromHandle(_source.Handle).Bounds;
        var request = new Request(_window.IsPresenting, preferences.TabletFeaturesEnabled, preferences.TabletKeyboardMeansDesktop,
            preferences.TabletHideTaskbar, preferences.ShowTaskbarWhileOpen, new(monitor.X, monitor.Y, monitor.Width, monitor.Height), _deviceRevision);
        if (_wanted != request) Volatile.Write(ref _wanted, request);
        else request = _wanted!;
        if (_busy) return;
        _busy = true;
        try
        {
            var snapshot = await Task.Run(() => Apply(request));
            if (!_disposed && ReferenceEquals(request, _wanted) && !SameArea(_published, snapshot))
            {
                _published = snapshot;
                _window.RefitNativeBounds();
            }
        }
        catch (Exception error) when (TaskbarLease.Expected(error) || error is AggregateException) { }
        finally
        {
            _busy = false;
            if (_disposed) _ = Task.Run(Cleanup);
            else if (!ReferenceEquals(request, _wanted)) Refresh();
        }
    }

    private TaskbarLeaseState? Apply(Request request)
    {
        if (!_recovered)
        {
            _recovered = _dependencies.Recover();
            if (!_recovered) return null;
        }
        if (request.Enabled && (_sampledRevision != request.DeviceRevision || Environment.TickCount64 - _sampledAt >= 2000))
        {
            _input = _dependencies.ReadInput(); _sampledAt = Environment.TickCount64; _sampledRevision = request.DeviceRevision;
        }
        if (_disposed || !ReferenceEquals(request, Volatile.Read(ref _wanted))) return _lease.Snapshot;
        var preferences = new Preferences { TabletFeaturesEnabled = request.Enabled, TabletKeyboardMeansDesktop = request.KeyboardPolicy };
        _tablet.Apply(preferences, _input);
        _dependencies.SetScope(request.Monitor);
        TaskbarLeaseMode? mode = !request.Open ? null : _tablet.IsActive && request.Hide ? TaskbarLeaseMode.Hide
            : request.Reveal ? TaskbarLeaseMode.Reveal : null;
        _lease.Synchronize(mode, () => !_disposed && ReferenceEquals(request, Volatile.Read(ref _wanted)));
        return _lease.Snapshot;
    }

    private Drawing.Rectangle WorkingArea(System.Windows.Forms.Screen screen)
    {
        var bounds = screen.Bounds;
        return TaskbarNativeBackend.AvailableArea(screen.WorkingArea, _published, new(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }

    private static bool SameArea(TaskbarLeaseState? a, TaskbarLeaseState? b) => a?.Mode == b?.Mode
        && (a == null && b == null || a != null && b != null && a.Windows.Select(w => (w.Monitor, w.Target)).SequenceEqual(b.Windows.Select(w => (w.Monitor, w.Target))));

    private nint OnMessage(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message is 0x219 or 0x1A or 0x7E || (uint)message == _taskbarCreated)
            _source.Dispatcher.BeginInvoke(DeviceStateChanged, DispatcherPriority.Background);
        return 0;
    }

    private void Cleanup()
    {
        try { _tablet.Dispose(); }
        finally { _lease.Dispose(); }
    }

    /// <summary>Removes UI hooks immediately and queues restoration. A ready independent watchdog covers process exit during pending work.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _timer.Stop(); _source.RemoveHook(OnMessage); _window.NativeWorkingArea = null;
        if (!_busy) _ = Task.Run(Cleanup);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
}
