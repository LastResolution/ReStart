using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private DateTimeOffset? _documentationNow;

    internal async Task RenderDocumentationAsync(string directory)
    {
        if (!_preview) throw new InvalidOperationException("Documentation rendering requires preview mode.");
        _documentationHtmlRendering = true;
        await InitializeAsync(); _clock.Stop();
        var missing = PreviewApps().Where(a => !_previewAppIcons.Contains(a.Id)).Select(a => a.Name).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException("Install the showcase apps before rendering: " + string.Join(", ", missing));
        _startupPreparing = true;
        var sampleDate = new DateTime(2026, 10, 18, 9, 41, 0, DateTimeKind.Unspecified);
        _documentationNow = new(sampleDate, TimeZoneInfo.Local.GetUtcOffset(sampleDate));
        try
        {
            var tiles = ScriptTilePackages.BundledIds.Take(12).Select(id => _scriptPackages.Import(ScriptTilePackages.BundledManifestPath(id))).ToArray();
            var images = Path.Combine(directory, "fixture-data", "photos"); Directory.CreateDirectory(images);
            WriteFixtureImage(Path.Combine(images, "a.png"), Colors.Teal);
            WriteFixtureImage(Path.Combine(images, "b.png"), Colors.Coral);
            var photo = tiles[11];
            SlideshowSource.Bind(_scriptPackages.SlideshowBindingPath(photo.ScriptId), images);
            await SaveLauncherShowcaseAsync(tiles, this, directory);
            await SaveNavigationShowcasesAsync(directory);
            SetLayoutEditing(true);
            foreach (var tile in Document.Groups[0].Tiles.Take(2)) ToggleTileSelection(tile.Id);
            SaveScreenshot(Path.Combine(directory, "selection-mode.png"));
            SetLayoutEditing(false);
            Document.Preferences.Theme = ReStart.Core.ThemeMode.Dark; ApplyAppearance(); RenderGroups(); UpdateLayout();
            ShowGroupBoundaries(true); SaveScreenshot(Path.Combine(directory, "group-placement.png")); ShowGroupBoundaries(false);
            _openFolder = Document.Groups.SelectMany(g => g.Tiles).Single(t => t.IsFolder);
            var folderGroup = Document.Groups.Single(g => g.Tiles.Contains(_openFolder));
            var closedOwner = GroupsPanel.Placements.Single(p => p.Group == folderGroup);
            var sideNeighbors = GroupsPanel.Placements.Where(p => p.Left >= closedOwner.Left + closedOwner.Width).ToArray();
            foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
            {
                Document.Preferences.Theme = theme; ApplyAppearance(); RenderGroups();
                Root.Measure(new(Root.Width, Root.Height)); Root.Arrange(new Rect(0, 0, Root.Width, Root.Height)); FitPanels(); UpdateLayout();
                foreach (var runtime in _scriptSessions.Values) runtime.NextUpdate = DateTimeOffset.MinValue;
                await UpdateScriptTilesAsync(); await WaitForDocumentationFramesAsync();
                SaveScreenshot(Path.Combine(directory, "inline-folder-" + theme.ToString().ToLowerInvariant() + ".png"));
            }
            SaveScreenshot(Path.Combine(directory, "inline-folder.png"));
            Require(sideNeighbors.All(p => GroupsPanel.Placements.Single(a => a.Group == p.Group).Top == p.Top), "A content-sized documentation folder displaced an unrelated side group.");
            _openFolder = null; RenderGroups();
            Document.Preferences.ShowRecentlyAdded = false; Document.Preferences.ShowMostUsed = false;
            ShowAllApps(this, new()); SaveScreenshot(Path.Combine(directory, "app-list.png"));
            _apps = _apps.Select(a => a with { FolderPath = a.Id is "notepad" or "calc" or "paint" ? "Accessories" : a.Id is "terminal" or "code" ? "Developer tools" : "" }).ToList();
            var sampleFolder = Directory.GetDirectories(Path.Combine(directory, "showcase-files")).Single();
            var folderEntry = FilePinning.FromPath(sampleFolder); await _catalog.LoadIconsAsync([folderEntry]);
            foreach (var name in new[] { "Accessories", "Developer tools" })
            {
                if (_catalog.Icons.TryGetValue(folderEntry.Id, out var icon)) _catalog.FolderIcons[name] = icon;
                _expandedAppFolders.Add("folder:" + name);
            }
            RenderApps(); SaveScreenshot(Path.Combine(directory, "app-folders.png"));
            await SaveSearchShowcaseAsync(directory);
            Require(!IsVisible, "Documentation rendering unexpectedly displayed a window.");
        }
        finally { _startupPreparing = false; _documentationNow = null; }
    }

    private async Task SaveNavigationShowcasesAsync(string directory)
    {
        foreach (var language in L.SupportedLanguages)
            foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
                foreach (var height in new[] { 480d, 1080d })
                {
                    Root.Height = height;
                    Document.Preferences.Language = language; Document.Preferences.Theme = theme;
                    Document.Preferences.NavigationFolders = FolderShortcuts.Select(s => s.Id).ToList();
                    ApplyAppearance(); RenderGroups(); SetRailOpen(true);
                    Root.Measure(new(Root.Width, height)); Root.Arrange(new Rect(0, 0, Root.Width, height)); FitPanels(); UpdateLayout();
                    Root.Measure(new(Root.Width, height)); Root.Arrange(new Rect(0, 0, Root.Width, height)); UpdateLayout();
                    Require(NavigationGlyphsStayAligned(), "Expanded navigation moved/resized an icon in " + language + "/" + theme + "/" + height);
                    if (language == "en" && height == 1080)
                    {
                        await WaitForDocumentationFramesAsync();
                        SaveScreenshot(Path.Combine(directory, $"sidebar-expanded-{theme}.png"));
                        SetRailOpen(false); SaveScreenshot(Path.Combine(directory, $"sidebar-collapsed-{theme}.png"));
                        var confirmation = new ConfirmationWindow(L.F(K.ConfirmDeleteGroup, "Sample group"));
                        var content = (FrameworkElement)confirmation.Content;
                        content.Measure(new Size(confirmation.Width, confirmation.MaxHeight));
                        content.Arrange(new Rect(new Point(), content.DesiredSize)); content.UpdateLayout();
                        SaveFixtureElement(confirmation, Path.Combine(directory, $"confirmation-{theme}.png"));
                        Require(!confirmation.IsVisible, "Documentation unexpectedly displayed a confirmation window.");
                        confirmation.Close();
                    }
                }
        Root.Height = 1080;
        Document.Preferences.Language = "en"; Document.Preferences.Theme = ReStart.Core.ThemeMode.Dark; Document.Preferences.NavigationFolders = null;
        ApplyAppearance(); SetRailOpen(false); RenderGroups();
        await WaitForDocumentationFramesAsync();
    }

    private async Task WaitForDocumentationFramesAsync()
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            UpdateLayout();
            if (ScriptsReady && _scriptViews.Count > 0 && _scriptViews.All(view =>
                _htmlTiles.ContainsKey(view.Tile.Id) || (_htmlTiles.ContainsKey(view.Tile.Id) || view.Body.Children.OfType<TextBlock>().Any(text => Equals(text.Tag, "ScriptValue") && text.Text.Length > 0 && text.ActualHeight > 0))))
            { await Task.WhenAll(_htmlTiles.Values.Select(view => view.FreezeForDocumentationAsync())); return; }
            foreach (var runtime in _scriptSessions.Values) runtime.NextUpdate = DateTimeOffset.MinValue;
            await UpdateScriptTilesAsync(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(100);
        }
        throw new InvalidOperationException("Documentation contains a live tile without a rendered value.");
    }

    private async Task SaveSearchShowcaseAsync(string directory)
    {
        var previousSize = (Width, Height);
        try
        {
            // Render the owned English fixture at the website's advertised dimensions.
            Width = 1920; Height = 1080;
            Document.Preferences.Theme = ReStart.Core.ThemeMode.Dark; RefreshLayout(); ResetToPinnedView();
            var powerShell = new AppEntry("powershell", "Windows PowerShell", Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"), false);
            var notes = Path.Combine(directory, "Power plan.txt"); File.WriteAllText(notes, "Fictional power plan document.");
            var noteEntry = FilePinning.FromPath(notes);
            _apps = [powerShell];
            await _catalog.LoadIconsAsync([powerShell, noteEntry]);
            _fileSearchFixture = (_, _) => Task.FromResult<(IReadOnlyList<AppEntry>, bool)>((new[] { noteEntry }, true));
            SetSmokeSearch("Power");
            for (var attempt = 0; attempt < 100 && !SearchResultNames.Contains("Power plan.txt"); attempt++) await Task.Delay(25);
            Require(SearchResultNames.Contains("Power plan.txt"), "The offline search showcase did not render its file fixture.");
            SetStatus("");
            foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
            {
                Document.Preferences.Theme = theme; ApplyAppearance(); UpdateLayout();
                await Dispatcher.Yield(DispatcherPriority.Render);
                SaveScreenshot(Path.Combine(directory, "unified-search-" + theme.ToString().ToLowerInvariant() + ".png"));
            }
            SaveScreenshot(Path.Combine(directory, "unified-search.png"));
        }
        finally { Width = previousSize.Width; Height = previousSize.Height; UpdateLayout(); }
    }

    /// <summary>Captures the complete launcher with a fixed, nonpersonal app list and demonstration live data.</summary>
    private async Task SaveLauncherShowcaseAsync(Tile[] liveTiles, MainWindow iconSource, string directory)
    {
        if (!_preview) throw new InvalidOperationException("Showcase rendering requires preview mode.");
        // Keep the complete-launcher composition at two curated six-tile lanes;
        // connected examples have their own offline data showcase.
        liveTiles = liveTiles.Take(12).ToArray();
        _verifyHostMetrics = false;
        Root.Width = 1920; Root.Height = 1080;
        Document = new() { Preferences = new() { Language = "en", Columns = 8, TileScale = 1.1, Theme = ReStart.Core.ThemeMode.Dark, ShowWallpaper = false, Animate = false } };
        ApplyAppearance();
        _apps = PreviewApps();
        foreach (var app in _apps)
            if (iconSource._catalog.Icons.TryGetValue(app.Id, out var icon)) _catalog.Icons[app.Id] = icon;
        Tile App(string id, int column, int row)
        {
            var tile = Tile.FromApp(_apps.Single(app => app.Id == id));
            tile.Column = column; tile.Row = row;
            return tile;
        }
        Tile Bundled(string id, TileSize size, int column, int row)
        {
            var tile = _scriptPackages.Import(ScriptTilePackages.BundledManifestPath(id));
            tile.Size = size; tile.Column = column; tile.Row = row;
            return tile;
        }
        Document.Groups.Add(new()
        {
            Name = "Everyday",
            Column = 0,
            X = 0,
            Row = 0,
            Tiles = [App("edge", 0, 0), App("mail", 2, 0), App("photos", 4, 0),
                Bundled("calendar", TileSize.Large, 0, 2),
                Bundled("orbit", TileSize.Medium, 4, 2), App("store", 4, 4)]
        });
        Document.Groups.Add(new()
        {
            Name = "Create & work",
            Column = 0,
            X = 6.5,
            Row = 0,
            Tiles = [App("notepad", 0, 0), App("calc", 2, 0), App("paint", 4, 0), App("terminal", 0, 2), App("code", 2, 2),
                new() { Label = "Quick tools", Column = 4, Row = 2, Children = [App("notepad", 0, 0), App("calc", 0, 0), App("paint", 0, 0)] }]
        });
        var fixtureDirectory = Path.Combine(directory, "showcase-files"); Directory.CreateDirectory(fixtureDirectory);
        var projects = Path.Combine(fixtureDirectory, "Projects"); Directory.CreateDirectory(projects);
        var notes = Path.Combine(fixtureDirectory, "Notes.txt"); File.WriteAllText(notes, "Demonstration document.");
        var files = new[] { FilePinning.FromPath(projects), FilePinning.FromPath(notes) };
        await _catalog.LoadIconsAsync(files);
        Document.Groups.Add(new()
        {
            Name = "Files & folders",
            Column = 0,
            X = 6.5,
            Row = 11,
            Tiles = files.Select((entry, i) =>
        { var tile = Tile.FromApp(entry); tile.Column = i * 2; tile.Row = 0; return tile; }).ToList()
        });
        for (var group = 0; group < 2; group++)
        {
            var contents = liveTiles.Skip(group * 6).Take(6).ToList();
            for (var i = 0; i < contents.Count; i++)
            {
                contents[i].Size = TileSize.Wide; contents[i].Column = i % 2 * 4; contents[i].Row = i / 2 * 2;
                contents[i].UseCustomColor = false; contents[i].UseLocalizedName = true;
            }
            Document.Groups.Add(new() { Name = group == 0 ? "At a glance" : "Make time", Column = 0, X = 13 + group * 8.5, Row = 0, Tiles = contents });
        }
        foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
        {
            Document.Preferences.Theme = theme;
            ApplyAppearance(); RenderGroups(); RenderApps();
            Root.Measure(new Size(Root.Width, Root.Height)); Root.Arrange(new Rect(0, 0, Root.Width, Root.Height)); UpdateLayout();
            for (var attempt = 0; attempt < 50; attempt++)
            {
                foreach (var runtime in _scriptSessions.Values) runtime.NextUpdate = DateTimeOffset.MinValue;
                await UpdateScriptTilesAsync(); UpdateLayout();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                UpdateLayout();
                if (!_scriptSessions.Values.Any(runtime => runtime.Rendering) && ScriptsReady && _scriptFrames[liveTiles[^1].Id].Image == "current" &&
                    _scriptViews.All(view => (_htmlTiles.ContainsKey(view.Tile.Id) || view.Body.Children.OfType<TextBlock>().Any(text => Equals(text.Tag, "ScriptValue") && text.Text.Length > 0 && text.ActualHeight > 0)))) break;
                await Task.Delay(100);
            }
            Require(ScriptsReady && ScriptFrameCount == _scriptViews.Count, "The complete launcher showcase is missing live tiles.");
            Require(_scriptViews.All(view => (_htmlTiles.ContainsKey(view.Tile.Id) || view.Body.Children.OfType<TextBlock>().Any(text => Equals(text.Tag, "ScriptValue") && text.Text.Length > 0 && text.ActualHeight > 0))),
                "A complete launcher showcase tile has no rendered value.");
            await Task.WhenAll(_htmlTiles.Values.Select(view => view.FreezeForDocumentationAsync()));
            SetStatus("");
            SaveScreenshot(Path.Combine(directory, $"launcher-{theme.ToString().ToLowerInvariant()}.png"));
            if (theme == ReStart.Core.ThemeMode.Dark) SaveMotionShowcase(directory);
        }
    }
}
