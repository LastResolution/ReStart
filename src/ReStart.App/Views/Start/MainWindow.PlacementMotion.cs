using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private readonly PlacementTransitions _tileTransitions = new(), _groupTransitions = new();
    private readonly Dictionary<FrameworkElement, Point> _arrangedGroups = [];
    private bool _freePlacementGesture, _reorderGesture;
    private bool FreePlacementActive => Document.Preferences.FreeTilePlacement || _dragging && _freePlacementGesture;

    private void SetFreePlacementGesture(bool enabled)
    {
        if (_freePlacementGesture == enabled) return;
        _freePlacementGesture = enabled; _folderHover.Reset(); StopFolderDwell(); ClearDropPreview();
    }

    private void SetReorderGesture(bool enabled)
    {
        if (_reorderGesture == enabled) return;
        _reorderGesture = enabled; _folderHover.Reset(); StopFolderDwell(); ClearDropPreview();
    }

    /// <summary>Gets an arranged origin without transient press, preview or placement animation transforms.</summary>
    /// <param name="element">Descendant visual with a committed layout slot.</param>
    /// <param name="ancestor">Ancestor whose local DIP coordinates are returned.</param>
    /// <returns>Untransformed layout origin; intended for model hit testing rather than rendering.</returns>
    private static Point LayoutOrigin(Visual element, Visual ancestor)
    {
        var offset = new Vector();
        for (var current = element; current != ancestor;)
        {
            offset += VisualTreeHelper.GetOffset(current);
            current = VisualTreeHelper.GetParent(current) as Visual ?? throw new InvalidOperationException("The layout visual is detached from its expected ancestor.");
        }
        return new(offset.X, offset.Y);
    }

    private Point PanelPoint(Point board, TilePanel panel)
    {
        var origin = LayoutOrigin(panel, GroupsPanel);
        return new(board.X - origin.X, board.Y - origin.Y);
    }

    private void InstallPlacementMotion()
    {
        GroupsPanel.PlacementChanged += (_, _) =>
        {
            foreach (var view in GroupsPanel.Children.OfType<StackPanel>())
            {
                var origin = LayoutOrigin(view, GroupsPanel);
                if (_arrangedGroups.TryGetValue(view, out var previous) && (previous - origin).LengthSquared > .0001)
                    _groupTransitions.Rebase(view, previous - origin, _dragging && MotionEnabled);
                _arrangedGroups[view] = origin;
            }
        };
    }

    private Dictionary<string, Point>? CapturePlacementMotion()
    {
        if (!MotionEnabled || !IsVisible || _motion != null || !GroupsPanel.IsVisible) return null;
        return MotionElements().ToDictionary(pair => pair.Key, pair => pair.Value.TranslatePoint(new(), GroupsPanel));
    }

    private IEnumerable<KeyValuePair<string, FrameworkElement>> MotionElements()
    {
        foreach (var (id, view) in _selectionViews) yield return new("tile:" + id, view.Button);
        foreach (var stack in GroupsPanel.Children.OfType<StackPanel>())
            if (stack.Tag is TileGroup group && stack.Children.OfType<GroupHeader>().FirstOrDefault() is { } header)
                yield return new("group:" + group.Id, header);
    }

    private void RestorePlacementMotion(Dictionary<string, Point>? previous)
    {
        if (previous == null || !MotionEnabled) return;
        GroupsPanel.UpdateLayout();
        foreach (var (id, view) in MotionElements())
            if (previous.TryGetValue(id, out var origin))
                _tileTransitions.Rebase(view, origin - LayoutOrigin(view, GroupsPanel), true);
    }

    private void ClearPlacementMotion()
    {
        _tileTransitions.Reset(); _groupTransitions.Reset(); _arrangedGroups.Clear(); _projectedGroups = null;
    }
}
