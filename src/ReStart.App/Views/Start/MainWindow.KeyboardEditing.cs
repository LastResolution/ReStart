using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private void FocusEditingTile(string? id)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!_layoutEditing || !IsActive) return;
            var button = id != null && _selectionViews.TryGetValue(id, out var target) && target.Button.IsVisible ? target.Button
                : _selectionViews.Values.FirstOrDefault(v => v.Button.IsVisible).Button;
            button?.BringIntoView(); button?.Focus();
        }, DispatcherPriority.Loaded);
    }

    private bool HandlePlacementKeyboard(Key key, ModifierKeys modifiers)
    {
        var focused = Ancestor<Button>(Keyboard.FocusedElement as DependencyObject)?.Tag as Tile;
        var arrow = key is Key.Left or Key.Right or Key.Up or Key.Down;
        if (arrow && modifiers == ModifierKeys.None)
        {
            if (focused == null || !_selectionViews.TryGetValue(focused.Id, out var current)) { FocusEditingTile(null); return true; }
            Point Center(Button button) => button.TranslatePoint(new(button.ActualWidth / 2, button.ActualHeight / 2), GroupsPanel);
            var origin = Center(current.Button);
            var next = _selectionViews.Where(p => p.Key != focused.Id && p.Value.Button.IsVisible).Select(p => (p.Key, Point: Center(p.Value.Button)))
                .Select(p => (p.Key, X: p.Point.X - origin.X, Y: p.Point.Y - origin.Y))
                .Where(p => key switch { Key.Left => p.X < -1, Key.Right => p.X > 1, Key.Up => p.Y < -1, _ => p.Y > 1 })
                .OrderBy(p => key is Key.Left or Key.Right ? Math.Abs(p.X) + 2 * Math.Abs(p.Y) : Math.Abs(p.Y) + 2 * Math.Abs(p.X)).FirstOrDefault();
            if (next.Key != null) FocusEditingTile(next.Key);
            return true;
        }
        if (focused == null) return false;
        var ids = _selectedTileIds.Contains(focused.Id) ? _selectedTileIds.ToArray() : [focused.Id];
        List<TileGroup>? plan;
        if (arrow && modifiers is ModifierKeys.Control or (ModifierKeys.Control | ModifierKeys.Shift))
        {
            var dx = key == Key.Left ? -1 : key == Key.Right ? 1 : 0;
            var dy = key == Key.Up ? -1 : key == Key.Down ? 1 : 0;
            plan = modifiers.HasFlag(ModifierKeys.Shift) ? KeyboardLayoutEditor.MoveGroup(Document, focused.Id, dx, dy, GroupsPanel.Columns)
                : KeyboardLayoutEditor.Nudge(Document, ids, dx, dy, GroupsPanel.Columns);
        }
        else if (modifiers == ModifierKeys.Alt && key is >= Key.D1 and <= Key.D4)
            plan = KeyboardLayoutEditor.Resize(Document, ids, new[] { TileSize.Small, TileSize.Medium, TileSize.Wide, TileSize.Large }[key - Key.D1], GroupsPanel.Columns);
        else if (modifiers == ModifierKeys.Control && key == Key.G)
        {
            if (_selectedTileIds.Count == 0) ToggleTileSelection(focused.Id);
            MoveSelectionToGroup(null); FocusEditingTile(focused.Id); return true;
        }
        else return false;
        if (plan == null) SetStatus(L.T(CanEditTile(focused.Id) ? K.SelectionDoesNotFit : K.LayoutLockedStatus));
        else { Document.Groups = plan; CommitLayout(); FocusEditingTile(focused.Id); }
        return true;
    }
}
