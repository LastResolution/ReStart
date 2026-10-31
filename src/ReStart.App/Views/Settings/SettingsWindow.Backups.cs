using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

internal sealed partial class SettingsWindow
{
    private async void ExportBackup()
    {
        if (!ResolvePendingChanges()) return;
        var dialog = new SaveFileDialog { Title = L.T(K.BackupCreate), FileName = "ReStart-backup.zip", DefaultExt = ".zip", Filter = "ZIP|*.zip" };
        if (dialog.ShowDialog(this) != true || !ConfirmationWindow.Ask(this, L.T(K.BackupPrivacy))) return;
        var detached = JsonSerializer.Deserialize<LayoutDocument>(JsonSerializer.Serialize(_main.Document, LayoutStore.JsonOptions), LayoutStore.JsonOptions)!;
        IsEnabled = false;
        try
        {
            await Task.Run(() => ProfileBackup.Export(dialog.FileName, Path.GetDirectoryName(_store.FilePath)!, detached));
            _feedback.Text = L.T(K.BackupCreated);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException) { _feedback.Text = L.F(K.BackupFailed, ex.Message); }
        finally { IsEnabled = true; }
    }

    private async void RestoreBackup()
    {
        if (!ResolvePendingChanges()) return;
        if (_store.IsReadOnly) { _feedback.Text = L.T(K.MigrationReadOnly); return; }
        if (_main.Document.Preferences.LayoutLocked || _main.Document.Preferences.LockedGroupIds.Count > 0) { _feedback.Text = L.T(K.LayoutLockedStatus); return; }
        var dialog = new OpenFileDialog { Title = L.T(K.BackupRestore), Filter = "ZIP|*.zip" };
        if (dialog.ShowDialog(this) != true) return;
        IsEnabled = false;
        try
        {
            using var pending = await Task.Run(() => ProfileBackup.PrepareRestore(dialog.FileName, Path.GetDirectoryName(_store.FilePath)!));
            if (ReleaseVersion.Parse(pending.Layout.SavedWithAppVersion) is { } producer && producer.CompareTo(ReleaseVersion.Parse(AppBuildInfo.Version)) > 0)
                throw new InvalidDataException(L.T(K.ErrorFutureLayout));
            if (!ConfirmationWindow.Ask(this, L.F(K.BackupConfirm, pending.Layout.Groups.Count, pending.Layout.Groups.Sum(g => g.Tiles.Count)))) return;
            var local = _main.Document.Preferences; var restored = pending.Layout.Preferences;
            restored.CaptureWindowsKey = local.CaptureWindowsKey; restored.CaptureStartButton = local.CaptureStartButton;
            restored.StartWithWindows = local.StartWithWindows; restored.ExplorerContextMenu = local.ExplorerContextMenu;
            restored.TabletFeaturesEnabled = local.TabletFeaturesEnabled; restored.TabletKeyboardMeansDesktop = local.TabletKeyboardMeansDesktop;
            restored.TabletHideTaskbar = local.TabletHideTaskbar; restored.ShowTaskbarWhileOpen = local.ShowTaskbarWhileOpen;
            pending.Commit(_store.Save);
            _main.AdoptRestoredProfile(pending.Layout); DiscardAndClose();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException) { _feedback.Text = L.F(K.BackupFailed, ex.Message); }
        finally { if (IsLoaded) IsEnabled = true; }
    }
}
