using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private readonly LayoutHistory _layoutHistory = new();
    private readonly HashSet<string> _selectedTileIds = [];
    private readonly Dictionary<string, (Button Button, FrameworkElement Mark)> _selectionViews = [];
    private bool _historyReady, _layoutEditing;
    private IReadOnlyList<SelectedTilePosition>? _selectionDragPositions;
    private sealed record SelectionPlanKey(List<TileGroup> Groups, IReadOnlyList<SelectedTilePosition> Selection, string Anchor,
        string? Group, string? Folder, double X, double Y, int Columns, int TileColumns, double Cell);
    private (SelectionPlanKey Key, List<TileGroup>? Groups)? _selectionPlanCache;

    private void ToggleLayoutEditing(object sender, RoutedEventArgs e)
    {
        var entering = !_layoutEditing;
        SetLayoutEditing(entering);
        if (!entering) ResetToPinnedView();
    }

    private void SetLayoutEditing(bool editing)
    {
        if (_layoutEditing == editing || editing && (!_ready || !CanEditLayout)) return;
        _editingGroupHeader?.EndEdit(true);
        if (editing) ResetToPinnedView();
        _layoutEditing = editing;
        _selectionPlanCache = null;
        _selectedTileIds.Clear();
        EditBar.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        EditBar.ToolTip = L.T(K.KeyboardLayoutHelp);
        ShowGroupBoundaries(editing);
        RefreshTileSelection();
        if (editing) DoneEditingButton.Focus();
        else if (IsVisible && IsActive) FocusSearchInput();
    }

    private void InstallTileSelection(Button button, Grid content, Tile tile)
    {
        var mark = new Border
        {
            Width = 22,
            Height = 22,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new(-2),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
            Child = new TextBlock
            {
                Text = "\uE73E",
                FontFamily = new("Segoe MDL2 Assets"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        mark.SetResourceReference(BackgroundProperty, "TextBrush");
        mark.SetResourceReference(TextBlock.ForegroundProperty, "PanelBrush");
        content.Children.Add(mark);
        _selectionViews[tile.Id] = (button, mark);
    }

    private void ToggleTileSelection(string id)
    {
        if (!_layoutEditing || !CanEditTile(id) || FindTile(id) is not Tile tile) return;
        if (!_selectedTileIds.Remove(id))
        {
            // A folder and its children must never participate in the same move.
            foreach (var child in tile.Children) _selectedTileIds.Remove(child.Id);
            foreach (var parent in Document.Groups.SelectMany(g => g.Tiles).Where(t => t.Children.Any(c => c.Id == id))) _selectedTileIds.Remove(parent.Id);
            _selectedTileIds.Add(id);
        }
        RefreshTileSelection();
    }

    private void SelectTileForEditing(string id)
    {
        if (!CanEditTile(id)) { SetStatus(L.T(K.LayoutLockedStatus)); return; }
        var parentId = Document.Groups.SelectMany(g => g.Tiles).FirstOrDefault(t => t.Children.Any(c => c.Id == id))?.Id;
        SetLayoutEditing(true);
        if (parentId != null && _openFolder?.Id != parentId && FindTile(parentId) is Tile parent) OpenFolder(parent);
        if (!_selectedTileIds.Contains(id)) ToggleTileSelection(id);
    }

    private void RefreshTileSelection()
    {
        _selectedTileIds.RemoveWhere(id => FindTile(id) == null || !CanEditTile(id) || _layoutEditing && !_selectionViews.ContainsKey(id));
        foreach (var (id, view) in _selectionViews)
        {
            var selected = _layoutEditing && _selectedTileIds.Contains(id);
            view.Mark.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetItemStatus(view.Button, selected ? L.T(K.TileSelected) : "");
            AutomationProperties.SetHelpText(view.Button, _layoutEditing ? L.T(K.LayoutEditingHint) : "");
        }
        UpdateEditingChrome();
    }

    private void UpdateEditingChrome()
    {
        if (EditModeButton == null) return;
        EditModeButton.IsEnabled = _ready && CanEditLayout;
        ExpandedEditModeButton.IsEnabled = EditModeButton.IsEnabled;
        var label = L.T(_layoutEditing ? K.FinishLayoutEditing : K.EditLayout);
        EditRailLabel.Label = label;
        AutomationProperties.SetName(EditModeButton, label); AutomationProperties.SetName(ExpandedEditModeButton, label);
        if (_layoutEditing)
        {
            EditModeButton.SetResourceReference(BackgroundProperty, "TileBrush");
            ExpandedEditModeButton.SetResourceReference(BackgroundProperty, "TileBrush");
        }
        else { EditModeButton.Background = Brushes.Transparent; ExpandedEditModeButton.Background = Brushes.Transparent; }
        UndoButton.IsEnabled = _historyReady && _layoutHistory.CanUndo && CanEditLayout;
        RedoButton.IsEnabled = _historyReady && _layoutHistory.CanRedo && CanEditLayout;
        SelectionSummary.Text = L.F(K.SelectedTileCount, _selectedTileIds.Count) + " · " + L.T(K.LayoutEditingHint);
        ClearSelectionButton.IsEnabled = MoveSelectionButton.IsEnabled = UnpinSelectionButton.IsEnabled = _selectedTileIds.Count > 0;
    }

    private void ClearTileSelectionClick(object sender, RoutedEventArgs e) { _selectedTileIds.Clear(); RefreshTileSelection(); }
    private void SelectAllTilesClick(object sender, RoutedEventArgs e)
    {
        _selectedTileIds.Clear();
        foreach (var root in Document.Groups.SelectMany(g => g.Tiles).Where(t => CanEditTile(t.Id))) _selectedTileIds.Add(root.Id);
        RefreshTileSelection();
    }

    private void UnpinSelectedTilesClick(object sender, RoutedEventArgs e)
    {
        if (!_layoutEditing || _selectedTileIds.Count == 0 || _selectedTileIds.Any(id => !CanEditTile(id))) return;
        foreach (var id in _selectedTileIds.ToArray()) RemoveTile(id);
        CommitLayout();
    }

    private void UndoLayoutClick(object sender, RoutedEventArgs e) => RestoreLayoutHistory(false);
    private void RedoLayoutClick(object sender, RoutedEventArgs e) => RestoreLayoutHistory(true);

    private void RestoreLayoutHistory(bool redo)
    {
        if (!_historyReady || _dragging || !CanEditLayout || _editingGroupHeader?.IsEditing == true) return;
        var folderId = _openFolder?.Id;
        var before = Document.Groups;
        if (!(redo ? _layoutHistory.Redo(Document) : _layoutHistory.Undo(Document))) return;
        if (!LayoutLocks.AllowsChange(before, Document.Groups, Document.Preferences, GroupsPanel.Columns))
        {
            if (redo) _layoutHistory.Undo(Document); else _layoutHistory.Redo(Document);
            SetStatus(L.T(K.LayoutLockedStatus)); return;
        }
        if (!TrySaveLayout(recordHistory: false))
        {
            // Failed persistence also rolls back the history cursor, keeping the
            // same operation available for a subsequent retry.
            if (redo) _layoutHistory.Undo(Document); else _layoutHistory.Redo(Document);
        }
        _openFolder = folderId != null && FindTile(folderId) is { IsFolder: true } folder ? folder : null;
        RenderGroups();
        UpdateEditingChrome();
    }

    private void AddLayoutEditingMenu(ContextMenu menu)
    {
        menu.Items.Add(new Separator());
        var locked = new MenuItem { Header = L.T(K.LockLayout), IsCheckable = true, InputGestureText = "Ctrl+Shift+L" };
        locked.Click += (_, _) => SetLayoutLocked(locked.IsChecked);
        menu.Items.Add(locked);
        var edit = new MenuItem { Header = L.T(_layoutEditing ? K.FinishLayoutEditing : K.EditLayout), InputGestureText = "Ctrl+E" };
        edit.Click += ToggleLayoutEditing;
        var undo = new MenuItem { Header = L.T(K.UndoLayout), InputGestureText = "Ctrl+Z" };
        undo.Click += UndoLayoutClick;
        var redo = new MenuItem { Header = L.T(K.RedoLayout), InputGestureText = "Ctrl+Y" };
        redo.Click += RedoLayoutClick;
        menu.Items.Add(edit); menu.Items.Add(undo); menu.Items.Add(redo);
        menu.Opened += (_, _) =>
        {
            locked.IsChecked = Document.Preferences.LayoutLocked;
            locked.IsEnabled = !_store.IsReadOnly;
            edit.Header = L.T(_layoutEditing ? K.FinishLayoutEditing : K.EditLayout);
            edit.IsEnabled = _ready && CanEditLayout;
            undo.IsEnabled = _historyReady && _layoutHistory.CanUndo && CanEditLayout;
            redo.IsEnabled = _historyReady && _layoutHistory.CanRedo && CanEditLayout;
        };
    }

    private bool HandleLayoutEditingKey(KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        var text = Ancestor<TextBox>(e.OriginalSource as DependencyObject);
        var editingText = text != null && (text != SearchBox || TopBar.Height > 0);
        if (!editingText && e.Key == Key.L && modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) SetLayoutLocked(!Document.Preferences.LayoutLocked);
        else if (e.Key == Key.E && modifiers == ModifierKeys.Control)
        {
            SetLayoutEditing(!_layoutEditing);
            if (_layoutEditing) FocusEditingTile(null);
        }
        else if (!editingText && e.Key == Key.Z && modifiers == ModifierKeys.Control) RestoreLayoutHistory(false);
        else if (!editingText && (e.Key == Key.Y && modifiers == ModifierKeys.Control || e.Key == Key.Z && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))) RestoreLayoutHistory(true);
        else if (_layoutEditing && !editingText)
        {
            if (HandlePlacementKeyboard(e.Key == Key.System ? e.SystemKey : e.Key, modifiers)) { }
            else if (e.Key == Key.A && modifiers == ModifierKeys.Control) SelectAllTilesClick(this, new());
            else if (e.Key == Key.Escape)
            { if (_selectedTileIds.Count > 0) ClearTileSelectionClick(this, new()); else SetLayoutEditing(false); }
            else if (e.Key == Key.Delete && modifiers == ModifierKeys.None) UnpinSelectedTilesClick(this, new());
            else if (e.Key == Key.F && modifiers == ModifierKeys.Control) { SetLayoutEditing(false); return false; }
            else return false;
        }
        else return false;
        e.Handled = true;
        return true;
    }

    private IReadOnlyList<SelectedTilePosition> CaptureTileSelection() => _selectionViews
        .Where(p => _selectedTileIds.Contains(p.Key) && p.Value.Button.IsVisible)
        .Select(p => { var point = p.Value.Button.TranslatePoint(new(0, 0), GroupsPanel); return new SelectedTilePosition(p.Key, point.X / Cell, point.Y / Cell); }).ToArray();

    private List<TileGroup>? PlanSelectionDrop(string anchorId, Point point)
    {
        if (!_layoutEditing || _selectionDragPositions is not { Count: > 0 } selection || selection.Any(s => !CanEditTile(s.Id))) return null;
        var target = FindTile(anchorId) is Tile anchor ? TileDropTarget(anchor, point) : GroupAt(point);
        if (target is { } lockedTarget && !CanEditGroup(lockedTarget.Group)) return null;
        if (target is { } destination && (!destination.Group.IsStandalone || destination.Panel.FolderOwner != null))
        {
            var local = destination.Panel.ToModelPoint(GroupsPanel.TranslatePoint(point, destination.Panel));
            var closedFolder = destination.Panel.FolderOwner == null && FindTile(anchorId) is Tile source && selection.All(s => FindTile(s.Id)?.IsFolder == false)
                ? FolderHoverTarget(source, destination.Panel, GroupsPanel.TranslatePoint(point, destination.Panel), anchorId, existingFoldersOnly: true) : null;
            if (destination.Panel.FolderOwner != null || selection.Any(s => FindTile(s.Id)?.IsFolder == true)) { _folderHover.Reset(); StopFolderDwell(); }
            return CachedSelectionPlan(selection, anchorId, destination.Group.IsStandalone ? null : destination.Group.Id,
                destination.Panel.FolderOwner?.Id ?? closedFolder?.Id, closedFolder == null ? Math.Round((local.X - _dragOffset.X) / Cell, MidpointRounding.AwayFromZero) : 0,
                closedFolder == null ? Math.Round((local.Y - _dragOffset.Y) / Cell, MidpointRounding.AwayFromZero) : 0);
        }
        if (target is { } independent && FindTile(anchorId) is Tile lead)
        {
            var closed = FolderHoverTarget(lead, independent.Panel, GroupsPanel.TranslatePoint(point, independent.Panel), anchorId);
            if (closed != null)
            {
                var grouped = closed.IsFolder ? CachedSelectionPlan(selection, anchorId, null, closed.Id, 0, 0)
                    : TileGrouping.Plan(Document, selection.Select(s => FindTile(s.Id)!).ToArray(), closed.Id, GroupsPanel.Columns, Cell);
                return grouped != null && PlacementAllowed(grouped) ? grouped : null;
            }
        }
        else { _folderHover.Reset(); StopFolderDwell(); }
        var (_, row, x) = BoardAnchor(point, true);
        return CachedSelectionPlan(selection, anchorId, null, null, x, row / 2d);
    }

    private List<TileGroup>? CachedSelectionPlan(IReadOnlyList<SelectedTilePosition> selection, string anchor, string? group, string? folder, double x, double y)
    {
        var key = new SelectionPlanKey(Document.Groups, selection, anchor, group, folder, x, y, GroupsPanel.Columns, Document.Preferences.Columns, Cell);
        if (_selectionPlanCache is { } previous && previous.Key == key) return previous.Groups;
        var groups = TileBatchMove.Plan(Document, selection, anchor, group, folder, x, y, GroupsPanel.Columns, Cell);
        if (groups != null && !PlacementAllowed(groups)) groups = null;
        _selectionPlanCache = (key, groups);
        return groups;
    }

    private void HandleSelectionDrop(DragEventArgs e, string anchorId, Point point, bool commit)
    {
        var plan = PlanSelectionDrop(anchorId, point);
        e.Effects = plan == null ? DragDropEffects.None : DragDropEffects.Move;
        if (plan == null) { ClearDropPreview(); if (commit) SetStatus(L.T(K.SelectionDoesNotFit)); return; }
        if (commit) { CommitSelectionPlan(plan); return; }
        ShowSelectionPreview(plan, point, anchorId);
        ShowPendingFolderPreview();
    }

    private void CommitSelectionPlan(List<TileGroup> plan) { Document.Groups = plan; CommitLayout(); }

    private void ShowSelectionPreview(List<TileGroup> plan, Point? point = null, string? anchorId = null)
    {
        var bounds = SelectionPreviewBounds(plan);
        if (bounds.Count == 0) return;
        ShowBoardPreview(bounds[0]);
        _dropAdorner!.SelectionBounds = bounds;
        var destination = point is Point pointer && anchorId != null && FindTile(anchorId) is Tile tile ? TileDropTarget(tile, pointer) : null;
        _dropAdorner.Caption = L.F(K.MoveTileCount, _selectedTileIds.Count) + (point == null ? "" : " · " + (destination?.Panel.FolderOwner != null
            ? L.T(K.DropIntoFolder) : destination is { Group.IsStandalone: false } target
                ? L.F(K.DropIntoGroup, target.Group.Name.Length == 0 ? L.T(K.UnnamedGroup) : target.Group.Name) : L.T(K.DropIndependent)));
        _dropAdorner.InvalidateVisual();
    }

    private List<Rect> SelectionPreviewBounds(List<TileGroup> groups)
    {
        var bounds = new List<Rect>();
        foreach (var g in GroupLayout.Pack(groups, GroupsPanel.Columns, Document.Preferences.Columns, Cell, expandedFolderId: _openFolder?.Id, preserveColumns: true))
        {
            var tiles = TileLayout.Pack(g.Group.Tiles, Document.Preferences.Columns);
            var expansion = FolderExpansion.Create(tiles, Document.Preferences.Columns, _openFolder?.Id);
            var compact = g.Group.IsStandalone && expansion == null;
            var header = g.Group.IsStandalone ? 0 : GroupLayout.HeaderHeight;
            foreach (var t in tiles.Where(t => _selectedTileIds.Contains(t.Tile.Id)))
                bounds.Add(new(g.Left + (compact ? 0 : t.Column * Cell), g.Top + header + (expansion?.TileY(t, Cell) ?? t.Row * Cell), t.Width * Cell - 4, t.Height * Cell - 4));
            if (expansion != null)
                foreach (var t in expansion.Children.Where(t => _selectedTileIds.Contains(t.Tile.Id)))
                    bounds.Add(new(g.Left + t.Column * Cell, g.Top + header + expansion.Row * Cell + GroupLayout.HeaderHeight + Controls.InlineFolderView.ContentTopInset + t.Row * Cell, t.Width * Cell - 4, t.Height * Cell - 4));
        }
        return bounds;
    }

    private void MoveSelectedTilesClick(object sender, RoutedEventArgs e)
    {
        var menu = CreateContextMenu();
        foreach (var group in Document.Groups.Where(g => !g.IsStandalone && CanEditGroup(g)))
            Add(menu, group.Name.Length == 0 ? L.T(K.UnnamedGroup) : group.Name, () => MoveSelectionToGroup(group));
        Add(menu, L.T(K.NewGroup), () => MoveSelectionToGroup(null));
        OpenMenu(menu, MoveSelectionButton);
    }

    private void MoveSelectionToGroup(TileGroup? destination)
    {
        var selected = Document.Groups.SelectMany(g => g.Tiles).SelectMany(t => new[] { t }.Concat(t.Children))
            .Where(t => _selectedTileIds.Contains(t.Id)).ToArray();
        if (!_layoutEditing || selected.Length == 0 || selected.Any(t => !CanEditTile(t.Id)) || destination != null && !CanEditGroup(destination)) return;
        // Menu moves collect tiles into a compact block. Dragging instead keeps
        // the selection's visible relative positions, including intentional gaps.
        var copies = LayoutHistory.CopyGroups([new() { Tiles = [.. selected] }])[0].Tiles;
        foreach (var tile in copies) { tile.Column = null; tile.Row = null; }
        var selection = TileLayout.Pack(copies, Document.Preferences.Columns).Select(p => new SelectedTilePosition(p.Tile.Id, p.Column, p.Row)).ToArray();
        var working = new LayoutDocument { Preferences = Document.Preferences, Groups = LayoutHistory.CopyGroups(Document.Groups) };
        var targetId = destination?.Id;
        if (targetId == null) { var created = new TileGroup { Name = "" }; working.Groups.Add(created); targetId = created.Id; }
        var plan = TileBatchMove.Plan(working, selection, selection[0].Id, targetId, null, 0, 0, GroupsPanel.Columns, Cell);
        if (plan == null || !PlacementAllowed(plan)) { SetStatus(L.T(K.SelectionDoesNotFit)); return; }
        Document.Groups = plan; CommitLayout();
    }
}
