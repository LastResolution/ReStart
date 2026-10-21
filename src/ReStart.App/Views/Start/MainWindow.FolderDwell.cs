using System.Windows;
using System.Windows.Threading;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private sealed record PendingFolderDrop(Tile Source, TilePanel Panel, string SourceId, Tile Target, bool ExistingFoldersOnly, Point ViewportPoint);
    private PendingFolderDrop? _pendingFolderDrop;
    private DispatcherTimer? _folderDwellTimer;

    private void WatchFolderDwell(Tile source, TilePanel panel, Point point, string sourceId, Tile? target, bool existingFoldersOnly)
    {
        if (target == null) { StopFolderDwell(); return; }
        _pendingFolderDrop = new(source, panel, sourceId, target, existingFoldersOnly, panel.TranslatePoint(point, ContentScroll));
        if (_folderDwellTimer != null) return;
        _folderDwellTimer = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(50) };
        _folderDwellTimer.Tick += (_, _) =>
        {
            if (!_dragging || _pendingFolderDrop is not { } pending) { StopFolderDwell(); return; }
            // OLE owns pointer tracking during DoDragDrop. WPF's mouse device
            // can still report the grab position, so retain the DragEventArgs
            // position in viewport coordinates and reproject after scrolling.
            var boardPoint = ContentScroll.TranslatePoint(pending.ViewportPoint, GroupsPanel);
            if (!pending.Panel.IsLoaded || TileDropTarget(pending.Source, boardPoint)?.Panel != pending.Panel)
            { _folderHover.Reset(); StopFolderDwell(); ClearDropPreview(); return; }
            FolderHoverTarget(pending.Source, pending.Panel, GroupsPanel.TranslatePoint(boardPoint, pending.Panel), pending.SourceId, existingFoldersOnly: pending.ExistingFoldersOnly);
            ShowPendingFolderPreview();
        };
        _folderDwellTimer.Start();
    }

    private void ShowPendingFolderPreview()
    {
        if (_pendingFolderDrop is not { } pending) return;
        var placement = pending.Panel.Placements.FirstOrDefault(p => p.Tile == pending.Target);
        if (placement.Tile == null) return;
        var armed = _folderHover.Update(pending.SourceId, pending.Target.Id, Environment.TickCount64, HoverDelay(pending.Panel, pending.Target));
        var createsGroup = CreatesGroup(pending.Panel, pending.Target);
        if (armed && createsGroup)
        {
            var sources = _selectionDragPositions is { } selected
                ? selected.Select(s => FindTile(s.Id)!).ToArray() : new[] { pending.Source };
            if (TileGrouping.Plan(Document, sources, pending.Target.Id, GroupsPanel.Columns, Cell) is { } plan)
                ShowGroupCreationPreview(plan, ((TileGroup)pending.Panel.Tag).Id);
            return;
        }
        var fits = true;
        if (armed && _selectionDragPositions is { } selection)
            fits = CachedSelectionPlan(selection, pending.SourceId, pending.Panel.Tag is TileGroup { IsStandalone: false } group ? group.Id : null,
                pending.Target.Id, 0, 0) != null;
        ShowDropPreview(pending.Panel, pending.Target, placement.Column, placement.Row);
        _dropAdorner!.FolderTarget = armed && fits;
        _dropAdorner.HoverProgress = _folderHover.Progress(Environment.TickCount64);
        _dropAdorner.Caption = L.T(!fits ? K.SelectionDoesNotFit : createsGroup ? K.HoldToCreateGroup : armed ? K.DropIntoFolder : K.HoldToCreateFolder);
        if (pending.Target.IsFolder) _dropAdorner.Caption += " · " + TileDisplayName(pending.Target);
        _dropAdorner.InvalidateVisual();
    }

    private void StopFolderDwell()
    {
        _pendingFolderDrop = null;
        _folderDwellTimer?.Stop();
        _folderDwellTimer = null;
    }
}
