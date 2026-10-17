using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ReStart.App.Services;

namespace ReStart.App;

internal sealed partial class SettingsWindow
{
    internal WrapPanel LibraryTiles { get; } = new();
    internal int LibraryRefreshCount { get; private set; }
    internal string? LibraryRefreshError { get; private set; }

    private void BuildTileLibrary(Panel content)
    {
        Heading(content, L.T(K.TileLibrary));
        Note(content, L.T(K.TileLibraryHelp));
        Note(content, _main.ScriptPackages.LibraryRoot);
        var buttons = new WrapPanel();
        DataButton(buttons, L.T(K.OpenTileLibrary), () =>
        {
            try { Directory.CreateDirectory(_main.ScriptPackages.LibraryRoot); ShellLauncher.Launch(_main.ScriptPackages.LibraryRoot); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException) { _feedback.Text = ex.Message; }
        });
        var closed = false; var refreshing = false; var pending = false; string? signature = null;
        async void Refresh()
        {
            if (closed) return;
            // Coalesce new requests into a follow-up scan. Invalidating every
            // in-flight result can otherwise starve updates during a long copy.
            if (refreshing) { pending = true; return; }
            refreshing = true;
            try
            {
                do
                {
                    pending = false;
                    var entries = await Task.Run(_main.ScriptPackages.DiscoverLibrary);
                    if (closed) return;
                    LibraryRefreshCount++;
                    LibraryRefreshError = null;
                    var currentSignature = string.Join("\0", entries.Select(entry => entry.ManifestPath + "\n" + entry.Error + "\n" +
                        System.Text.Json.JsonSerializer.Serialize(entry.Manifest, ReStart.Core.ScriptTileProtocol.Json)));
                    if (signature == currentSignature) continue;
                    signature = currentSignature;
                    LibraryTiles.Children.Clear();
                    if (entries.Count == 0) Note(LibraryTiles, L.T(K.TileLibraryEmpty));
                    foreach (var entry in entries)
                    {
                        var title = entry.Manifest?.LocalizedTitles.GetValueOrDefault(L.Language, entry.Manifest.Title) ?? Path.GetFileName(Path.GetDirectoryName(entry.ManifestPath));
                        var button = new Button
                        {
                            Content = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
                            Width = 152,
                            Height = 70,
                            Margin = new(0, 0, 8, 8),
                            Tag = entry.ManifestPath,
                            IsEnabled = entry.Manifest != null,
                            ToolTip = entry.Error ?? L.F(K.AddBundledTile, title),
                            Background = Resource("TileBrush")
                        };
                        button.Click += (_, _) =>
                        {
                            var tile = _main.AddScriptPackage(entry.ManifestPath);
                            if (tile == null) return;
                            if (entry.Manifest!.Capabilities.Contains("images.slideshow")) _main.ChooseSlideshowFolder(tile, this);
                            _feedback.Text = L.T(K.StatusScriptAdded);
                        };
                        LibraryTiles.Children.Add(button);
                    }
                } while (pending && !closed);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { if (!closed) { LibraryRefreshError = ex.Message; _feedback.Text = ex.Message; } }
            finally { refreshing = false; }
        }
        DataButton(buttons, L.T(K.RefreshTiles), Refresh);
        content.Children.Add(buttons); content.Children.Add(LibraryTiles);
        var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        debounce.Tick += (_, _) => { debounce.Stop(); Refresh(); };
        // Filesystem notifications can be coalesced or arrive while a package is
        // still being copied. Reconcile while Settings is open, including when
        // the library is below the scroll viewport or on an unselected page.
        var reconcile = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        reconcile.Tick += (_, _) => { if (IsVisible) Refresh(); };
        reconcile.Start();
        LibraryTiles.IsVisibleChanged += (_, _) => { if (LibraryTiles.IsVisible) Refresh(); };
        FileSystemWatcher? watcher = null;
        try
        {
            Directory.CreateDirectory(_main.ScriptPackages.LibraryRoot);
            watcher = new(_main.ScriptPackages.LibraryRoot)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            void Changed(object? sender, FileSystemEventArgs e) => Dispatcher.BeginInvoke(() => { if (!closed) { debounce.Stop(); debounce.Start(); } });
            watcher.Created += Changed; watcher.Changed += Changed; watcher.Deleted += Changed; watcher.Renamed += Changed;
            watcher.Error += (_, _) => Dispatcher.BeginInvoke(() => { if (!closed) Refresh(); });
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { _feedback.Text = ex.Message; }
        Closed += (_, _) => { closed = true; debounce.Stop(); reconcile.Stop(); watcher?.Dispose(); };
        Refresh();
    }
}
