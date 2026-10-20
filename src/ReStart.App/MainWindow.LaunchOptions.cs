using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private async Task EditLaunchOptionsAsync(Tile tile)
    {
        var app = new AppEntry(tile.AppId, tile.Label, tile.Target, tile.IsShellApp);
        _dialogDepth++;
        try
        {
            var verbs = await AppShellCommands.ReadAsync(app);
            if (_exit || FindTile(tile.Id) != tile) return;
            var canElevate = verbs.Contains("runas");
            var canConfigure = canElevate || !tile.IsShellApp && Path.GetExtension(tile.Target).ToLowerInvariant() is ".exe" or ".com" or ".cmd" or ".bat" or ".lnk";
            var dialog = new LaunchOptionsWindow(tile, canConfigure, canElevate) { Owner = this };
            if (dialog.ShowDialog() == true) CommitLayout();
        }
        finally { _dialogDepth--; }
    }

    private async Task LaunchTileAsync(Tile tile)
    {
        var app = new AppEntry(tile.AppId, tile.Label, tile.Target, tile.IsShellApp);
        if (!tile.UseLaunchOptions) { Launch(app); return; }
        if (_dismissing || _layoutEditing) return;
        if (_preview && _activationFixture != null) { _activationFixture(app); return; }
        var owner = new WindowInteropHelper(this).Handle;
        Dismiss();
        try
        {
            await AppShellCommands.LaunchConfiguredAsync(app, tile.LaunchArguments, tile.WorkingDirectory, tile.RunAsAdministrator, owner);
            Activity.RecordLaunch(app.Id, Document.Preferences.ShowMostUsed, DateTimeOffset.Now);
            if (Document.Preferences.ShowMostUsed) SaveActivity();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { } // Normal UAC cancellation.
        catch (Exception ex) when (ex is Win32Exception or COMException or InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        { Present(); SetStatus(L.T(K.ErrorLaunch) + ex.Message); }
    }
}
