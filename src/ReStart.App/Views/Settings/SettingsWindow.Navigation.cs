using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App;

internal sealed partial class SettingsWindow
{
    private void BuildNavigationSettings(Panel panel)
    {
        Check(panel, L.T(K.SettingsRailHover), _draft.ExpandNavigationOnHover, value => _draft.ExpandNavigationOnHover = value);
        Heading(panel, L.T(K.SettingsSidebar));
        var folders = _draft.NavigationFolders ?? ["documents", "pictures"];
        _draft.NavigationFolders = folders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var shortcut in MainWindow.FolderShortcuts)
            Check(panel, L.T(shortcut.Label), folders.Contains(shortcut.Id, StringComparer.OrdinalIgnoreCase), value =>
            {
                _draft.NavigationFolders.RemoveAll(id => id.Equals(shortcut.Id, StringComparison.OrdinalIgnoreCase));
                if (value) _draft.NavigationFolders.Add(shortcut.Id);
            });
        Heading(panel, L.T(K.SearchTitle));
        Check(panel, L.T(K.SettingsSearchSettings), _draft.SearchSettings, value => _draft.SearchSettings = value);
        Check(panel, L.T(K.SearchFilesSetting), _draft.SearchFiles, value => _draft.SearchFiles = value);
        Check(panel, L.T(K.SettingsJumpLists), _draft.ShowJumpLists, value => _draft.ShowJumpLists = value);
        Heading(panel, L.T(K.SettingsLayout));
        Check(panel, L.T(K.LockLayout), _draft.LayoutLocked, value => _draft.LayoutLocked = value);
        Note(panel, L.T(K.LayoutLockHelp));
        Check(panel, L.T(K.SettingsTileHover), _draft.TileHoverHighlight, value => _draft.TileHoverHighlight = value);
        Check(panel, L.T(K.SettingsGroupHover), _draft.ShowGroupBoundariesOnHover, value => _draft.ShowGroupBoundariesOnHover = value);
        CheckBox? overlapGroups = null;
        Check(panel, L.T(K.SettingsFreePlacement), _draft.FreeTilePlacement, value =>
        { _draft.FreeTilePlacement = value; if (overlapGroups != null) overlapGroups.IsEnabled = value; });
        Note(panel, L.T(K.SettingsFreePlacementHelp));
        overlapGroups = Check(panel, L.T(K.SettingsDropGroups), _draft.GroupTilesOnDrop, value => _draft.GroupTilesOnDrop = value);
        overlapGroups.IsEnabled = _draft.FreeTilePlacement;
        Check(panel, L.T(K.SettingsDropFolders), _draft.CreateFoldersOnDrop, value => _draft.CreateFoldersOnDrop = value);
        Check(panel, L.T(K.SettingsDragScroll), _draft.DragEdgeScrolling, value => _draft.DragEdgeScrolling = value);
        Check(panel, L.T(K.SettingsDropHints), _draft.ShowDropHints, value => _draft.ShowDropHints = value);
        Heading(panel, L.T(K.NavAllApps));
        Check(panel, L.T(K.SettingsShowRecentApps), _draft.ShowRecentlyAdded, value => _draft.ShowRecentlyAdded = value);
        Check(panel, L.T(K.SettingsShowMostUsed), _draft.ShowMostUsed, value => _draft.ShowMostUsed = value);
        Note(panel, L.T(K.AppHistoryHelp));
        DataButton(panel, L.T(K.ClearAppHistory), _main.ClearAppHistory);
    }
}
