using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ReStart.App.Controls;

namespace ReStart.App;

public partial class MainWindow
{
    private static int VerifyTouchPanningLease()
    {
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        var lease = new TouchPanningLease();
        var first = new object(); var other = new object();
        foreach (var mode in Enum.GetValues<PanningMode>())
        {
            var viewer = new ScrollViewer { PanningMode = mode };
            lease.Begin(viewer, first);
            Check(viewer.PanningMode == PanningMode.None, "A draggable touch retained scroll manipulation.");
            lease.Release(other);
            Check(viewer.PanningMode == PanningMode.None, "Another contact released the active panning lease.");
            lease.Begin(viewer, first); lease.Release(first);
            Check(viewer.PanningMode == mode, "A repeated touch replaced the saved panning mode.");
        }
        var one = new ScrollViewer { PanningMode = PanningMode.Both };
        var two = new ScrollViewer { PanningMode = PanningMode.VerticalOnly };
        lease.Begin(one, first); lease.Begin(two, other);
        Check(one.PanningMode == PanningMode.Both && two.PanningMode == PanningMode.None, "A new touch stranded the previous viewer.");
        lease.Release(); lease.Release();
        Check(two.PanningMode == PanningMode.VerticalOnly, "Cancelling a touch did not restore its viewer.");
        return checks;
    }

    private int VerifyTouchDragRoutes()
    {
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        var button = _selectionViews.Values.First().Button;
        var group = Document.Groups.First(g => g.Tiles.Any(t => t.Id == _selectionViews.Keys.First()));
        var initial = ContentScroll.PanningMode;
        var locked = Document.Preferences.LayoutLocked;
        var lockedGroups = Document.Preferences.LockedGroupIds.ToList();
        var contact = new FixtureDragContact(int.MaxValue - 1);
        TouchEventArgs Routed(UIElement element, RoutedEvent routedEvent)
        {
            var args = new TouchEventArgs(contact, 0) { RoutedEvent = routedEvent };
            element.RaiseEvent(args);
            return args;
        }
        try
        {
            Document.Preferences.LayoutLocked = false; Document.Preferences.LockedGroupIds.Clear();
            Check(!Routed(button, PreviewTouchDownEvent).Handled && ContentScroll.PanningMode == PanningMode.None,
                "A draggable tile did not scope panning without consuming touch promotion.");
            Routed(button, PreviewTouchUpEvent);
            Check(ContentScroll.PanningMode == initial, "Tile release stranded touch panning.");
            Routed(button, PreviewTouchDownEvent); Routed(button, LostTouchCaptureEvent);
            Check(ContentScroll.PanningMode == initial, "A cancelled contact stranded touch panning.");
            Document.Preferences.LayoutLocked = true;
            Routed(button, PreviewTouchDownEvent);
            Check(ContentScroll.PanningMode == initial, "A locked layout stole touch scrolling.");
            Document.Preferences.LayoutLocked = false; Document.Preferences.LockedGroupIds.Add(group.Id);
            Routed(button, PreviewTouchDownEvent);
            Check(ContentScroll.PanningMode == initial, "A locked group stole touch scrolling.");
            Document.Preferences.LockedGroupIds.Clear();
            Routed(GroupsPanel, PreviewTouchDownEvent);
            Check(ContentScroll.PanningMode == initial, "Blank board space stole touch scrolling.");
            Routed(button, PreviewTouchDownEvent); Hide();
            Check(ContentScroll.PanningMode == initial, "Hiding the fixture stranded touch panning.");
            Present();
        }
        finally
        {
            _touchPanning.Release();
            Document.Preferences.LayoutLocked = locked; Document.Preferences.LockedGroupIds = lockedGroups;
        }
        return checks;
    }

    // These are routed-event tests only; this device never activates or reports
    // physical input and never creates an OLE drag or an OS pointer event.
    private sealed class FixtureDragContact(int id) : TouchDevice(id)
    {
        public override TouchPoint GetTouchPoint(IInputElement? relativeTo) => new(this, new(), new(0, 0, 1, 1), TouchAction.Down);
        public override TouchPointCollection GetIntermediateTouchPoints(IInputElement? relativeTo) => [GetTouchPoint(relativeTo)];
    }
}
