using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace ReStart.App.Services;

internal readonly record struct TaskbarIdentity(nint Window, int ProcessId, long Started);
internal sealed record StartButtonTarget(nint Taskbar, Rect Bounds, long ReadAt);

/// <summary>Owns accessibility roots on the discovery thread for exactly one taskbar process lifetime.</summary>
internal sealed class StartButtonDiscovery(Func<nint, AutomationElement>? createRoot = null)
{
    private readonly Dictionary<TaskbarIdentity, AutomationElement> _roots = [];
    private readonly Func<nint, AutomationElement> _createRoot = createRoot ?? AutomationElement.FromHandle;
    private readonly System.Windows.Automation.Condition _condition = new OrCondition(
        new PropertyCondition(AutomationElement.AutomationIdProperty, "StartButton"),
        new PropertyCondition(AutomationElement.ClassNameProperty, "Start"));

    internal static IReadOnlyList<TaskbarIdentity> EnumerateTaskbars()
    {
        var taskbars = new List<TaskbarIdentity>();
        Native.EnumWindows((window, _) =>
        {
            var name = new System.Text.StringBuilder(128);
            Native.GetClassName(window, name, name.Capacity);
            if (name.ToString() is not ("Shell_TrayWnd" or "Shell_SecondaryTrayWnd")) return true;
            try
            {
                // Reject unrelated windows before invoking accessibility providers.
                Native.GetWindowThreadProcessId(window, out var processId);
                if (processId == 0 || processId > int.MaxValue) return true;
                using var owner = Process.GetProcessById((int)processId);
                if (owner.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                    taskbars.Add(new(window, owner.Id, owner.StartTime.ToUniversalTime().Ticks));
            }
            catch (Exception error) when (Expected(error)) { }
            return true;
        }, 0);
        return taskbars;
    }

    internal StartButtonTarget[] Read(IReadOnlyList<TaskbarIdentity> taskbars)
    {
        var current = taskbars.ToHashSet();
        foreach (var old in _roots.Keys.Where(key => !current.Contains(key)).ToArray()) _roots.Remove(old);
        var targets = new List<StartButtonTarget>();
        foreach (var taskbar in current)
        {
            try
            {
                Native.GetWindowThreadProcessId(taskbar.Window, out var processId);
                if (processId != taskbar.ProcessId) { _roots.Remove(taskbar); continue; }
                if (!_roots.TryGetValue(taskbar, out var root))
                {
                    // FromHandle creates managed HWND providers backed by native UIA
                    // resources. Recreating these on every poll accumulates providers
                    // even while Start is closed. Retain one root per live taskbar.
                    root = _createRoot(taskbar.Window);
                    _roots.Add(taskbar, root);
                }
                if (root.Current.ProcessId != taskbar.ProcessId) { _roots.Remove(taskbar); continue; }
                var observed = new List<StartButtonTarget>();
                var readAt = Environment.TickCount64;
                var buttons = root.FindAll(TreeScope.Descendants, _condition);
                foreach (AutomationElement button in buttons)
                {
                    var info = button.Current;
                    var bounds = info.BoundingRectangle;
                    if (info.ProcessId != taskbar.ProcessId || info.ControlType != ControlType.Button || info.IsOffscreen
                        || bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) continue;
                    observed.Add(new(taskbar.Window, bounds, readAt));
                }
                // Cache providers, never geometry: centered/aligned Start buttons
                // and changed monitor positions still get fresh hit-test bounds.
                targets.AddRange(observed);
            }
            catch (Exception error) when (Expected(error)) { _roots.Remove(taskbar); }
        }
        return targets.ToArray();
    }

    internal void Clear() => _roots.Clear();

    private static bool Expected(Exception error) => error is ElementNotAvailableException or COMException
        or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception;
}
