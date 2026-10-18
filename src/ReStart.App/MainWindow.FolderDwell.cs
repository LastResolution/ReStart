using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private sealed record PendingFolderDrop(Tile Source, TilePanel Panel, string SourceId, Tile Target, bool ExistingFoldersOnly);
    private PendingFolderDrop? _pendingFolderDrop;
    private DispatcherTimer? _folderDwellTimer;

    private void WatchFolderDwell(Tile source, TilePanel panel, string sourceId, Tile? target, bool existingFoldersOnly)
    {
        if (target == null) { StopFolderDwell(); return; }
        _pendingFolderDrop = new(source, panel, sourceId, target, existingFoldersOnly);
        if (_folderDwellTimer != null) return;
        _folderDwellTimer = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(50) };
        _folderDwellTimer.Tick += (_, _) =>
        {
            if (!_dragging || _pendingFolderDrop is not { } pending) { StopFolderDwell(); return; }
            var boardPoint = Mouse.GetPosition(GroupsPanel);
            if (TileDropTarget(pending.Source, boardPoint)?.Panel != pending.Panel) { _folderHover.Reset(); StopFolderDwell(); return; }
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
        var armed = _folderHover.Update(pending.SourceId, pending.Target.Id, Environment.TickCount64);
        ShowDropPreview(pending.Panel, pending.Target, placement.Column, placement.Row);
        _dropAdorner!.FolderTarget = armed;
        _dropAdorner.Caption = L.T(armed ? K.DropIntoFolder : K.HoldToCreateFolder);
        _dropAdorner.InvalidateVisual();
    }

    private void StopFolderDwell()
    {
        _pendingFolderDrop = null;
        _folderDwellTimer?.Stop();
        _folderDwellTimer = null;
    }
}
