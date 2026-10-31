using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ReStart.App.Services;

/// <summary>Positions the launcher using physical monitor pixels across per-monitor DPI transitions.</summary>
internal static class MonitorPlacement
{
    private static readonly ConditionalWeakTable<HwndSource, Placement> Placements = new();

    /// <summary>Fits a launcher to the physical working area and retains that fit after WPF DPI processing.</summary>
    /// <param name="window">Existing top-level native window handle owned by the calling UI thread.</param>
    /// <param name="after">Native insertion-order handle; ignored when <paramref name="flags"/> includes SWP_NOZORDER.</param>
    /// <param name="area">Target monitor working area in physical screen pixels, including negative screen coordinates.</param>
    /// <param name="flags">SetWindowPos flags controlling activation, visibility and stacking; must allow movement and resizing.</param>
    /// <param name="resolveArea">Optional UI-thread working-area resolver used after DPI transitions; null follows the ordinary Windows working area.</param>
    /// <returns>True if the immediate native operation succeeds; false on native failure.</returns>
    /// <remarks>A DPI transition schedules one coalesced correction after WPF processes its suggested scaled size. The window must have a live WPF presentation source.</remarks>
    /// <exception cref="ArgumentException">The handle does not belong to a live WPF presentation source.</exception>
    internal static bool FitWorkingArea(nint window, nint after, Rectangle area, uint flags, Func<System.Windows.Forms.Screen, Rectangle>? resolveArea = null)
    {
        var source = HwndSource.FromHwnd(window) ?? throw new ArgumentException("The window must have a WPF presentation source.", nameof(window));
        var placement = Placements.GetValue(source, static value => new Placement(value));
        placement.Area = area;
        placement.ResolveArea = resolveArea;
        return Native.SetWindowPos(window, after, area.X, area.Y, area.Width, area.Height, flags);
    }

    /// <summary>Stops fitting a source before changing it to an ordinary resizable verification window.</summary>
    /// <param name="window">Existing WPF native window handle; an unregistered or disposed source is ignored.</param>
    internal static void Release(nint window)
    {
        if (HwndSource.FromHwnd(window) is { } source && Placements.TryGetValue(source, out var placement))
        {
            placement.Release(); Placements.Remove(source);
        }
    }

    private sealed class Placement
    {
        private readonly HwndSource _source;
        private bool _pending, _released;
        internal Rectangle Area;
        internal Func<System.Windows.Forms.Screen, Rectangle>? ResolveArea;

        internal Placement(HwndSource source) { _source = source; source.AddHook(OnMessage); }
        internal void Release() { _released = true; _source.RemoveHook(OnMessage); }

        private nint OnMessage(nint window, int message, nint wParam, nint lParam, ref bool handled)
        {
            // Let WPF update its visual DPI and logical geometry normally. Its
            // suggested native size preserves logical size, whereas this launcher
            // must preserve the target monitor's physical working-area bounds.
            if (message == 0x02E0 && !_pending)
            {
                _pending = true;
                _source.Dispatcher.BeginInvoke(() =>
                {
                    _pending = false;
                    if (!_released && !_source.IsDisposed)
                    {
                        var screen = System.Windows.Forms.Screen.FromRectangle(Area);
                        Area = ResolveArea?.Invoke(screen) ?? screen.WorkingArea;
                        Native.SetWindowPos(window, 0, Area.X, Area.Y, Area.Width, Area.Height, 0x0014);
                    }
                }, DispatcherPriority.Loaded);
            }
            return 0;
        }
    }
}
