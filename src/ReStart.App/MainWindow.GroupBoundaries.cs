using System.Windows;
using System.Windows.Documents;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private GroupBoundariesAdorner? _groupBoundaries;

    private void InstallGroupBoundaries()
    {
        GroupsPanel.Loaded += (_, _) =>
        {
            if (_groupBoundaries != null || AdornerLayer.GetAdornerLayer(GroupsPanel) is not { } layer) return;
            _groupBoundaries = new(GroupsPanel) { IsHitTestVisible = false };
            layer.Add(_groupBoundaries);
        };
        GroupsPanel.MouseMove += (_, e) =>
        {
            if (_groupBoundaries == null) return;
            var point = e.GetPosition(GroupsPanel);
            var group = GroupsPanel.Placements.FirstOrDefault(p => !p.Group.IsStandalone && GroupLayout.OccupiedAreas(p.Group, GroupsPanel.TileColumns, Cell, _openFolder?.Id)
                .Any(a => new Rect(p.Left + a.Left, p.Top + a.Top, a.Width, a.Height).Contains(point))).Group;
            if (_groupBoundaries.HoveredGroup == group) return;
            _groupBoundaries.HoveredGroup = group; _groupBoundaries.InvalidateVisual();
        };
        GroupsPanel.MouseLeave += (_, _) =>
        {
            if (_groupBoundaries == null) return;
            _groupBoundaries.HoveredGroup = null; _groupBoundaries.InvalidateVisual();
        };
        GroupsPanel.PlacementChanged += (_, _) => _groupBoundaries?.InvalidateVisual();
    }

    private void ShowGroupBoundaries(bool show)
    {
        if (_groupBoundaries == null) return;
        _groupBoundaries.ShowAll = show; _groupBoundaries.InvalidateVisual();
    }
}
