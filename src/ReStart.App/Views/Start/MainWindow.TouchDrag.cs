using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private readonly TouchPanningLease _touchPanning = new();
    // Only the explicitly requested fictional interactive fixture enables these counters.
    private TouchInputCounters? _touchInput;
    private sealed class TouchInputCounters
    {
        public int Down, Move, Up, PanningSuspensions, MouseMoves, PressedMouseMoves, DragStarts, AcceptedDrops, CancelledDrags, ScrollChanges, FinalPanningMode, BoundaryFeedback;
    }

    private void InstallTouchDrag()
    {
        AddHandler(PreviewTouchDownEvent, new EventHandler<TouchEventArgs>((_, _) =>
        {
            if (_touchInput != null) _touchInput.Down++;
            if (TouchesOver.Skip(1).Any()) _touchPanning.Release();
        }), true);
        AddHandler(PreviewTouchMoveEvent, new EventHandler<TouchEventArgs>((_, _) => { if (_touchInput != null) _touchInput.Move++; }), true);
        AddHandler(PreviewTouchUpEvent, new EventHandler<TouchEventArgs>((_, e) =>
        {
            if (_touchInput != null) _touchInput.Up++;
            _touchPanning.Release(e.TouchDevice);
        }), true);
        AddHandler(LostTouchCaptureEvent, new EventHandler<TouchEventArgs>((_, e) => { if (!_dragging) _touchPanning.Release(e.TouchDevice); }), true);
        AddHandler(ContextMenuOpeningEvent, new ContextMenuEventHandler((_, _) => _touchPanning.Release()), true);
        Deactivated += (_, _) => _touchPanning.Release();
        // Keep scroll inertia, but prevent WPF's default edge feedback from
        // moving the entire launcher window when a finger reaches a boundary.
        ContentScroll.AddHandler(ManipulationBoundaryFeedbackEvent, new EventHandler<ManipulationBoundaryFeedbackEventArgs>((_, e) =>
        {
            if (_touchInput != null) _touchInput.BoundaryFeedback++;
            e.Handled = true;
        }), true);
        ContentScroll.ScrollChanged += (_, e) =>
        {
            if (_touchInput != null && (e.HorizontalChange != 0 || e.VerticalChange != 0)) _touchInput.ScrollChanges++;
        };
    }

    private void WireTouchDrag(Button button, string format, string id)
    {
        // List rows retain touch scrolling; only pinned layout controls use
        // direct touch-to-mouse dragging. Mouse drag wiring remains shared.
        if (format is not ("ReStart.Tile" or "ReStart.Group")) return;
        button.AddHandler(PreviewTouchDownEvent, new EventHandler<TouchEventArgs>((_, e) =>
        {
            if (TouchesOver.Skip(1).Any() || !CanEditLayout || _dragging
                || format == "ReStart.Tile" && !CanEditTile(id)
                || format == "ReStart.Group" && Document.Groups.FirstOrDefault(g => g.Id == id) is { } group && !CanEditGroup(group)) return;
            _touchPanning.Begin(ContentScroll, e.TouchDevice);
            if (_touchInput != null) _touchInput.PanningSuspensions++;
            // Leave the event unhandled: WPF must still promote this contact to
            // the existing Button/drag input path. Blank space keeps native panning.
        }), true);
    }
}
