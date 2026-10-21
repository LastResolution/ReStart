using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private bool CanEditLayout => !_store.IsReadOnly && !Document.Preferences.LayoutLocked;
    private bool CanEditGroup(TileGroup group) => CanEditLayout && !Document.Preferences.LockedGroupIds.Contains(group.Id);
    private bool CanEditTile(string id) => CanEditLayout && !Document.Groups.Any(g => !CanEditGroup(g) && g.Tiles.Any(t => t.Id == id || t.Children.Any(c => c.Id == id)));
    private bool PlacementAllowed(List<TileGroup> plan) => !_store.IsReadOnly && LayoutLocks.AllowsChange(Document.Groups, plan, Document.Preferences, GroupsPanel.Columns);

    private void SetLayoutLocked(bool locked)
    {
        var before = Document.Preferences.LayoutLocked;
        Document.Preferences.LayoutLocked = locked;
        if (locked) SetLayoutEditing(false);
        if (!TrySaveLayout()) { Document.Preferences.LayoutLocked = before; UpdateEditingChrome(); return; }
        RenderGroups(); SetStatus(L.T(locked ? K.LayoutLockedStatus : K.LayoutUnlockedStatus));
    }

    private void AddGroupLockMenu(ContextMenu menu, TileGroup group)
    {
        var item = new MenuItem { Header = L.T(K.LockGroup), IsCheckable = true, IsChecked = Document.Preferences.LockedGroupIds.Contains(group.Id), IsEnabled = !_store.IsReadOnly };
        item.Click += (_, _) =>
        {
            var before = Document.Preferences.LockedGroupIds.ToList();
            if (!Document.Preferences.LockedGroupIds.Remove(group.Id)) Document.Preferences.LockedGroupIds.Add(group.Id);
            _selectedTileIds.RemoveWhere(id => !CanEditTile(id));
            if (TrySaveLayout()) RenderGroups();
            else Document.Preferences.LockedGroupIds = before;
        };
        menu.Items.Add(item);
    }
}
