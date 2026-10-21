using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace ReStart.App.Services;

/// <summary>Reads Start-button accessibility bounds outside the input hook, scoped to actual Explorer taskbars.</summary>
internal sealed class StartButtonLocator : IDisposable
{
    private sealed record Target(nint Taskbar, Rect Bounds, long ReadAt);
    private Target[] _targets = [];
    private readonly ManualResetEvent _stop = new(false);
    private readonly AutoResetEvent _refresh = new(false);
    private readonly Thread _worker;
    private volatile bool _enabled;
    private int _disposed;

    internal StartButtonLocator()
    {
        _worker = new Thread(Watch) { IsBackground = true, Name = "ReStart Start-button discovery" };
        _worker.SetApartmentState(ApartmentState.MTA);
        _worker.Start();
    }

    internal void SetEnabled(bool enabled)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        _enabled = enabled;
        if (!enabled) Volatile.Write(ref _targets, []);
        _refresh.Set();
    }

    /// <summary>Matches only a recent observed rectangle belonging to the taskbar under the pointer; no coordinates are guessed.</summary>
    internal bool Contains(Native.Point point)
    {
        var window = Native.WindowFromPoint(point);
        var root = Native.GetAncestor(window, 2);
        var now = Environment.TickCount64;
        return Volatile.Read(ref _targets).Any(t => t.Taskbar == root && now - t.ReadAt <= 1500 && t.Bounds.Contains(point.X, point.Y));
    }

    private void Watch()
    {
        try
        {
            while (!_stop.WaitOne(0))
            {
                if (_enabled)
                {
                    var targets = new List<Target>();
                    Native.EnumWindows((window, _) =>
                    {
                        var name = new System.Text.StringBuilder(128);
                        Native.GetClassName(window, name, name.Capacity);
                        if (name.ToString() is not ("Shell_TrayWnd" or "Shell_SecondaryTrayWnd")) return true;
                        var readAt = Environment.TickCount64;
                        try
                        {
                            var root = AutomationElement.FromHandle(window);
                            var process = root.Current.ProcessId;
                            using var owner = System.Diagnostics.Process.GetProcessById(process);
                            if (!owner.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase)) return true;
                            var buttons = root.FindAll(TreeScope.Descendants, new OrCondition(
                                new PropertyCondition(AutomationElement.AutomationIdProperty, "StartButton"),
                                new PropertyCondition(AutomationElement.ClassNameProperty, "Start")));
                            foreach (AutomationElement button in buttons)
                            {
                                var current = button.Current;
                                var bounds = current.BoundingRectangle;
                                if (current.ProcessId != process || current.ControlType != ControlType.Button || current.IsOffscreen || bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) continue;
                                targets.Add(new(window, bounds, readAt));
                            }
                        }
                        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception) { }
                        return true;
                    }, 0);
                    if (_enabled) Volatile.Write(ref _targets, targets.ToArray());
                }
                if (WaitHandle.WaitAny([_stop, _refresh], 500) == 0) return;
            }
        }
        finally { _stop.Dispose(); _refresh.Dispose(); }
    }

    public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) != 0) return; _enabled = false; Volatile.Write(ref _targets, []); _stop.Set(); }
}
