using System.Windows;
using System.Windows.Controls;
using ReStart.App.Services;

namespace ReStart.App;

public partial class MainWindow
{
    private MenuItem AddLiveTileMenu(ItemsControl menu)
    {
        var add = new MenuItem { Header = L.T(K.AddLiveTile) };
        Add(add, L.T(K.StudioTitle), CreateScriptTile);
        add.Items.Add(new Separator());
        foreach (var id in ScriptTilePackages.BundledIds)
            try
            {
                var manifest = ScriptTilePackages.BundledManifest(id);
                var item = new MenuItem { Header = manifest.LocalizedTitles.GetValueOrDefault(L.Language, manifest.Title), Tag = id };
                item.Click += (_, e) => { AddLivePackage(ScriptTilePackages.BundledManifestPath(id), manifest.Capabilities); e.Handled = true; };
                add.Items.Add(item);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException) { }
        add.Items.Add(new Separator());
        var library = new MenuItem { Header = L.T(K.TileLibrary), Tag = "TileLibrary" };
        library.Items.Add(new MenuItem { Header = L.T(K.StatusLoading), IsEnabled = false });
        library.SubmenuOpened += async (_, e) => { if (e.Source == library) await PopulateScriptLibraryMenuAsync(library); };
        add.Items.Add(library);
        Add(add, L.T(K.OpenTileLibrary), () =>
        {
            try { Directory.CreateDirectory(_scriptPackages.LibraryRoot); LaunchPath(_scriptPackages.LibraryRoot); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception) { SetStatus(ex.Message); }
        });
        menu.Items.Add(add); return add;
    }

    private async Task PopulateScriptLibraryMenuAsync(MenuItem library)
    {
        try
        {
            var entries = await Task.Run(_scriptPackages.DiscoverLibrary);
            if (_exit) return;
            library.Items.Clear();
            foreach (var entry in entries)
            {
                var item = new MenuItem
                {
                    Header = entry.Manifest?.LocalizedTitles.GetValueOrDefault(L.Language, entry.Manifest.Title) ?? Path.GetFileName(Path.GetDirectoryName(entry.ManifestPath)),
                    Tag = entry.ManifestPath,
                    IsEnabled = entry.Manifest != null,
                    ToolTip = entry.Error
                };
                item.Click += (_, e) => { AddLivePackage(entry.ManifestPath, entry.Manifest!.Capabilities); e.Handled = true; };
                library.Items.Add(item);
            }
            if (entries.Count == 0) library.Items.Add(new MenuItem { Header = new TextBlock { Text = L.T(K.TileLibraryEmpty), MaxWidth = 320, TextWrapping = TextWrapping.Wrap }, IsEnabled = false });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { SetStatus(ex.Message); }
    }

    private void AddLivePackage(string path, string[] capabilities)
    {
        var tile = AddScriptPackage(path);
        if (tile != null && capabilities.Contains("images.slideshow")) ChooseSlideshowFolder(tile, this);
        if (tile != null && capabilities.Contains("data.sources")) ConfigureTileData(tile, this);
    }
}
