using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Accepts an Explorer/command-line pin and acknowledges only after the layout has been written successfully.</summary>
    internal async Task<PinReply> PinPathAsync(string path)
    {
        await InitializeAsync();
        try
        {
            var app = FilePinning.FromPath(path);
            app = _apps.FirstOrDefault(a => !a.IsShellApp && string.Equals(a.LaunchTarget, app.LaunchTarget, StringComparison.OrdinalIgnoreCase)) ?? app;
            var before = System.Text.Json.JsonSerializer.Serialize(Document, LayoutStore.JsonOptions);
            Tile tile;
            try { tile = PinApplication(app); _store.Save(Document); }
            catch
            {
                Document = System.Text.Json.JsonSerializer.Deserialize<LayoutDocument>(before, LayoutStore.JsonOptions)!;
                throw;
            }
            await _catalog.LoadIconsAsync([app]);
            Present(); RenderGroups(); UpdateLayout();
            var button = VisualChildren<Button>(GroupsPanel).FirstOrDefault(b => b.Tag == tile);
            button?.BringIntoView();
            SetStatus(L.F(K.StatusPinned, app.Name));
            return new(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Security.SecurityException)
        { Present(); SetStatus(L.T(K.ErrorPinFailed) + ex.Message); return new(false, L.T(K.ErrorPinFailed) + ex.Message); }
    }

    private Tile PinApplication(AppEntry app) => AppPinning.Pin(Document, app, availableColumns: GroupsPanel.Columns,
        cell: Cell, viewportWidth: ContentScroll.ViewportWidth, preferredTop: ContentScroll.VerticalOffset);

    private async void ChoosePinPath(bool directory)
    {
        string? path = null;
        WithDialog(() =>
        {
            if (directory)
            {
                var dialog = new OpenFolderDialog { Title = L.T(K.PinFolder) };
                if (dialog.ShowDialog(this) == true) path = dialog.FolderName;
            }
            else
            {
                var dialog = new OpenFileDialog { Title = L.T(K.PinFile), Filter = L.T(K.AllFilesFilter) };
                if (dialog.ShowDialog(this) == true) path = dialog.FileName;
            }
        });
        if (path != null) await PinPathAsync(path);
    }
}
