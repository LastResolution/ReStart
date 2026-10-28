using System.Windows;
using System.Windows.Media;

namespace ReStart.App.Tablet.Gestures;

/// <summary>
/// A few-pixel strip along the top edge's right part, up to the corner: swiping down from it
/// opens Control Center (the iPad gesture starts in the corner). It is only 5 DIP tall, so
/// maximized windows' caption buttons (about 30 DIP tall) stay tappable below it.
/// </summary>
internal sealed class ControlCenterEdge : TabletSurface
{
    private const double Thickness = 5;        // DIP; fingers landing on the top edge register in the first few pixels
    private const double Trigger = 28;         // DIP of downward travel that opens Control Center
    private bool _enabled, _fired;

    internal ControlCenterEdge() : base(transparent: true)
    {
        // Almost clear but still hit-testable (fully transparent layered pixels let input through).
        Content = new System.Windows.Controls.Border { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
        Input.Down += _ => _fired = false;
        Input.Move += s =>
        {
            var down = (s.Screen.Y - Input.Start.Screen.Y) / Scale;
            if (!_fired && down > Trigger) { _fired = true; TabletLog.Write("edge", "swipe down → control center"); TabletNavigation.OpenControlCenter(); }
        };
    }

    internal void Enable() { _enabled = true; Reposition(); Show(); Reposition(); }
    internal void Disable() { _enabled = false; Input.Abort(); Hide(); }

    internal void Reposition()
    {
        if (!_enabled) return;
        var (monitor, _) = TabletNavigation.Screen();
        var scale = NavNative.GetDpiForWindow(Handle) / 96.0;
        var left = monitor.Left + monitor.Width * 0.68;
        Place(new Rect(left, monitor.Top, monitor.Right - left, Math.Round(Thickness * scale)));
    }

    protected override void OnDisplayChanged() { base.OnDisplayChanged(); Reposition(); }

    internal new void Raise() { if (_enabled) base.Raise(); }
}
