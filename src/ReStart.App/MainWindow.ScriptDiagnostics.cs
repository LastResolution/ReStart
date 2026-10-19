using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private bool _verifyHostMetrics;
    /// <summary>Renders all bundled packages through real workers and exercises local click and image bindings in an isolated preview.</summary>
    internal async Task<BundledTilesSmokeResult> VerifyBundledTilesAsync(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("Bundled tile verification requires preview mode.");
        var directory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var fixture = new MainWindow(new LayoutStore(Path.Combine(directory, "layout.json")), true, () => Task.FromResult(new List<AppEntry>()))
        { Owner = this, Width = 1560, Height = 900, Topmost = false };
        var language = L.Language;
        var stage = "initial rendering";
        try
        {
            fixture.Present(); await fixture.InitializeAsync(); fixture._clock.Stop();
            fixture.Document = new() { Preferences = new() { Language = "en", Columns = 8, ShowWallpaper = false, Animate = false } };
            fixture.ApplyAppearance();
            var tiles = ScriptTilePackages.BundledIds.Select(id => fixture._scriptPackages.Import(ScriptTilePackages.BundledManifestPath(id))).ToArray();
            for (var group = 0; group < 3; group++)
            {
                var contents = tiles.Skip(group * 5).Take(5).ToList();
                for (var i = 0; i < contents.Count; i++) { contents[i].Column = i % 2 * 4; contents[i].Row = i / 2 * 2; }
                fixture.Document.Groups.Add(new() { Name = new[] { "Everyday", "Rhythm", "System & photos" }[group], Column = group, Row = 0, Tiles = contents });
            }
            var images = Path.Combine(directory, "images"); Directory.CreateDirectory(images);
            WriteFixtureImage(Path.Combine(images, "a.png"), Colors.Teal);
            WriteFixtureImage(Path.Combine(images, "b.png"), Colors.Coral);
            var photo = tiles[ScriptTilePackages.BundledIds.ToList().IndexOf("slideshow")]; SlideshowSource.Bind(fixture._scriptPackages.SlideshowBindingPath(photo.ScriptId), images);
            fixture.ApplyAppearance(); fixture.RenderGroups(); fixture.UpdateLayout();
            async Task RenderUntil(Func<bool> ready)
            {
                for (var attempt = 0; attempt < 50; attempt++)
                {
                    foreach (var runtime in fixture._scriptSessions.Values) runtime.NextUpdate = DateTimeOffset.MinValue;
                    await fixture.UpdateScriptTilesAsync(); fixture.UpdateLayout();
                    if (ready()) return;
                    await Task.Delay(100);
                }
                throw new InvalidOperationException("Bundled tiles did not produce the expected frames: " + string.Join(" | ", fixture._scriptViews.Select(v => v.Button.ToolTip)));
            }
            await RenderUntil(() => fixture.ScriptFrameCount == tiles.Length && fixture._scriptFrames[photo.Id].Image == "current" && fixture._scriptFrames[tiles[2].Id].Series.Length > 1);
            Require(fixture._scriptViews.Count == tiles.Length, "Not all bundled tiles are visible to the worker pool.");
            fixture.SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "bundled-wide.png"));
            fixture.SaveTileShowcase(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "tile-showcase.png"));
            var focus = tiles[4];
            fixture._scriptViews.Single(v => v.Tile == focus).Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntil(() => fixture._scriptFrames[focus.Id].State.TryGetProperty("pausedAt", out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Number);
            Require(fixture.IsVisible, "Clicking a script tile launched an external app or closed the launcher.");
            var pausedAt = fixture._scriptFrames[focus.Id].State.GetProperty("pausedAt").GetInt64();
            fixture.Hide(); fixture.Present(); fixture._clock.Stop();
            await RenderUntil(() => fixture._scriptFrames[focus.Id].State.GetProperty("pausedAt").ValueKind == System.Text.Json.JsonValueKind.Number);
            Require(fixture._scriptFrames[focus.Id].State.GetProperty("pausedAt").GetInt64() == pausedAt, "Hiding the board lost local tile state.");
            var oldPhoto = fixture._slideshowSources[photo.Id].Source.Refresh(DateTimeOffset.Now, 15).Index;
            fixture._scriptViews.Single(v => v.Tile == photo).Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntil(() => fixture._slideshowSources[photo.Id].Source.Refresh(DateTimeOffset.Now, 15).Index != oldPhoto);
            Require(fixture.IsVisible, "Clicking a slideshow tile opened another app.");
            using (File.Open(Path.Combine(images, "a.png"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            foreach (var size in new[] { TileSize.Small, TileSize.Medium, TileSize.Large })
            {
                foreach (var tile in tiles) tile.Size = size;
                fixture.RenderGroups(); fixture.UpdateLayout(); await RenderUntil(() => fixture.ScriptFrameCount == tiles.Length);
                foreach (var view in fixture._scriptViews)
                {
                    var plot = (Grid)view.Body.Parent;
                    var art = plot.Children.OfType<Controls.ScriptTileArt>().Single();
                    var frame = fixture._scriptFrames[view.Tile.Id];
                    Require(size != TileSize.Small || art.Visibility == Visibility.Collapsed, "Small tiles retain chart decoration or metadata.");
                    Require(frame.Layout != "stacked" || plot.RowDefinitions.Count == 2 && Grid.GetRow(art) == 1,
                        "A large tile did not place its chart below the text.");
                }
                fixture.SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"bundled-{size}.png"));
                if (size == TileSize.Small)
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "small-geometry.json"), System.Text.Json.JsonSerializer.Serialize(fixture._scriptViews.Select(v => new
                    {
                        frame = fixture._scriptFrames[v.Tile.Id].Text,
                        tileHeight = v.Button.ActualHeight,
                        gridHeight = ((Grid)v.Button.Content).ActualHeight,
                        plotHeight = ((Grid)v.Body.Parent).ActualHeight,
                        bodyHeight = v.Body.ActualHeight,
                        bodyDesired = v.Body.DesiredSize.Height,
                        texts = v.Body.Children.OfType<TextBlock>().Select(t => new { t.Text, t.ActualHeight, t.ActualWidth, desired = t.DesiredSize.Height, t.FontSize })
                    })));
            }
            var custom = tiles[0]; custom.UseCustomColor = true; custom.Color = "#FFFFFF";
            fixture.RenderGroups(); fixture.UpdateLayout(); await RenderUntil(() => true);
            Require(fixture._scriptViews.Single(v => v.Tile == custom).Button.Background is SolidColorBrush solid && solid.Color == Colors.White,
                "The script frame overwrote a user-selected tile color.");
            fixture._verifyHostMetrics = true;
            await RenderUntil(() => fixture._systemMetrics?.Latest?.CpuPercent != null && fixture._scriptFrames[tiles[2].Id].Text != "—");
            Require(fixture._systemMetrics?.Latest?.CpuPercent != null, "The real host did not supply CPU data to the tile.");
            var previousPool = fixture._scriptWorkerPool;
            stage = "clock change";
            fixture.OnSystemTimeChanged(null, EventArgs.Empty);
            await Task.Delay(100);
            await RenderUntil(() => fixture._scriptWorkerPool != null && fixture._scriptWorkerPool != previousPool);
            Require(fixture._scriptFrames[focus.Id].State.GetProperty("pausedAt").GetInt64() == pausedAt, "A clock change lost successful tile state.");
            var settings = new SettingsWindow(fixture, fixture._store);
            stage = "watched library";
            try
            {
                settings.Show(); settings.SelectSection(L.T(K.SettingsScriptTiles));
                var library = Path.Combine(fixture._scriptPackages.LibraryRoot, "local-clock"); Directory.CreateDirectory(library);
                async Task WaitForLibrary(Func<bool> ready, string message)
                {
                    for (var attempt = 0; attempt < 100 && !ready(); attempt++) await Task.Delay(100);
                    if (ready()) return;
                    settings.UpdateLayout(); SaveFixtureElement(settings, Path.Combine(Path.GetDirectoryName(snapshotPath)!, "tile-library-failure.png"));
                    throw new InvalidOperationException($"{message} Scans: {settings.LibraryRefreshCount}; settings visible: {settings.IsVisible}; library visible: {settings.LibraryTiles.IsVisible}; error: {settings.LibraryRefreshError ?? "none"}; packages: {string.Join(" | ", fixture._scriptPackages.DiscoverLibrary().Select(entry => entry.Error ?? "valid"))}");
                }
                // A package can arrive one file at a time while its Settings page
                // is not selected. It must recover without a manual refresh.
                settings.SelectSection(L.T(K.SettingsStartup));
                File.Copy(ScriptTilePackages.BundledManifestPath("clock"), Path.Combine(library, "manifest.json"));
                await WaitForLibrary(() => settings.LibraryTiles.Children.OfType<Button>().Any(b => !b.IsEnabled), "An incomplete library package was not reported.");
                File.Copy(Path.Combine(Path.GetDirectoryName(ScriptTilePackages.BundledManifestPath("clock"))!, "tile.js"), Path.Combine(library, "tile.js"));
                await WaitForLibrary(() => settings.LibraryTiles.Children.OfType<Button>().Count(b => b.IsEnabled) == 1, "The watched library did not discover the complete package.");
                settings.SelectSection(L.T(K.SettingsScriptTiles));
                var add = settings.LibraryTiles.Children.OfType<Button>().Single(b => b.IsEnabled);
                var scans = settings.LibraryRefreshCount;
                await WaitForLibrary(() => settings.LibraryRefreshCount > scans, "Library reconciliation did not run.");
                Require(ReferenceEquals(add, settings.LibraryTiles.Children.OfType<Button>().Single(b => b.IsEnabled)), "An unchanged library scan replaced the focused button.");
                settings.UpdateLayout(); SaveFixtureElement(settings, Path.Combine(Path.GetDirectoryName(snapshotPath)!, "tile-library.png"));
                var before = GroupLayout.Pack(fixture.Document.Groups, fixture.GroupsPanel.Columns, 8, fixture.Cell, preserveColumns: true).ToArray();
                add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); fixture.UpdateLayout();
                Require(fixture.Document.Groups.Count == 4 && fixture.Document.Groups[^1].IsStandalone, "Library click did not create an independent pin.");
                var after = GroupLayout.Pack(fixture.Document.Groups, fixture.GroupsPanel.Columns, 8, fixture.Cell, preserveColumns: true);
                Require(before.All(p => after.Single(a => a.Group == p.Group) == p), "Adding a library tile changed an existing group rectangle.");
            }
            finally { settings.Close(); }
            var context = new ContextMenu(); var liveMenu = fixture.AddLiveTileMenu(context);
            stage = "context menu library";
            Require(liveMenu.Items.OfType<MenuItem>().Count(item => item.Tag is string id && ScriptTilePackages.BundledIds.Contains(id)) == tiles.Length,
                "The right-click menu does not expose all bundled tiles.");
            var libraryMenu = liveMenu.Items.OfType<MenuItem>().Single(item => Equals(item.Tag, "TileLibrary"));
            await fixture.PopulateScriptLibraryMenuAsync(libraryMenu);
            var libraryItem = libraryMenu.Items.OfType<MenuItem>().Single(item => item.IsEnabled);
            var pinCount = fixture.Document.Groups.Count;
            libraryItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Require(fixture.Document.Groups.Count == pinCount + 1 && fixture.Document.Groups[^1].IsStandalone, "Right-click library addition did not create an independent tile.");
            foreach (var languageTag in TextCatalog.SupportedLanguages)
            {
                stage = "language " + languageTag;
                fixture.Document.Preferences.Language = languageTag; fixture.ApplyAppearance(); fixture.RenderGroups(); fixture.UpdateLayout();
                await RenderUntil(() => fixture._scriptFrames.TryGetValue(tiles[2].Id, out var cpu) && cpu.Kicker == ScriptTilePackages.BundledManifest("cpu").Settings.GetProperty("labels").GetProperty(languageTag).GetProperty("caption").GetString());
                var cpuView = fixture._scriptViews.Single(view => view.Tile == tiles[2]);
                var expectedName = ScriptTilePackages.BundledManifest("cpu").LocalizedTitles[languageTag];
                Require(fixture.TileDisplayName(tiles[2]) == expectedName && System.Windows.Automation.AutomationProperties.GetName(cpuView.Button) == expectedName,
                    "Live tile names or accessibility labels did not follow the UI language.");
            }
            focus.Label = "My timer"; focus.UseLocalizedName = false;
            fixture.Document.Preferences.Language = "ja"; fixture.ApplyAppearance(); fixture.RenderGroups(); fixture.UpdateLayout();
            Require(fixture.TileDisplayName(focus) == "My timer", "Language switching overwrote a custom tile name.");
            var responsive = Enum.GetValues<TileSize>().Select(size => new Tile
            {
                Widget = "script",
                ScriptId = tiles[0].ScriptId,
                AppId = "restart:script:" + tiles[0].ScriptId,
                Label = "Orbit clock",
                UseLocalizedName = true,
                Size = size
            }).ToArray();
            fixture.Document = new()
            {
                Preferences = new() { Language = "en", Columns = 16, ShowWallpaper = false, Animate = false },
                Groups = [new() { Name = "One script, four sizes", Tiles = responsive.ToList() }]
            };
            fixture.ApplyAppearance(); fixture.RenderGroups(); fixture.UpdateLayout();
            await RenderUntil(() => fixture.ScriptFrameCount == 4);
            foreach (var view in fixture._scriptViews)
            {
                var main = view.Body.Children.OfType<TextBlock>().Single(t => Equals(t.Tag, "ScriptValue"));
                if (view.Tile.Size == TileSize.Small)
                    Require(main.ActualHeight >= 29 && main.ActualHeight <= view.Body.ActualHeight + .1,
                        "The small clock clips its second line instead of displaying both time components.");
            }
            fixture.SaveTileShowcase(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "responsive-sizes.png"));
            stage = "position-independent tile geometry";
            var placementChecks = fixture.VerifyScriptGeometry(tiles[8], Path.GetDirectoryName(snapshotPath)!);
            stage = "complete launcher showcase";
            await fixture.SaveLauncherShowcaseAsync(tiles, this, Path.GetDirectoryName(snapshotPath)!);
            return new(tiles.Length, 4, 2, 1, 1, 1, 1, 2, 2, 8, placementChecks);
        }
        catch (InvalidOperationException ex) { throw new InvalidOperationException($"Bundled tile verification ({stage}): {ex.Message}", ex); }
        finally { fixture.Exit(); L.SetLanguage(language); ApplyAppearance(); }
    }

    private static void WriteFixtureImage(string path, Color color)
    {
        var pixels = Enumerable.Range(0, 64 * 64).SelectMany(_ => new[] { color.B, color.G, color.R, (byte)255 }).ToArray();
        var bitmap = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgra32, null, pixels, 64 * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path); encoder.Save(output);
    }

    private void SaveTileShowcase(string path)
    {
        var width = (int)Math.Ceiling(GroupsPanel.Placements.Max(p => p.Left + p.Width)) + 48;
        var height = (int)Math.Ceiling(GroupsPanel.Placements.Max(p => p.Top + p.Height)) + 32;
        var visual = new DrawingVisual();
        using (var draw = visual.RenderOpen())
        {
            draw.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 30, 30)), null, new Rect(0, 0, width, height));
            draw.PushTransform(new TranslateTransform(24, 16));
            draw.DrawRectangle(new VisualBrush(GroupsPanel) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null,
                new Rect(0, 0, GroupsPanel.ActualWidth, GroupsPanel.ActualHeight));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var output = File.Create(path); encoder.Save(output);
    }
}

internal sealed record BundledTilesSmokeResult(int Packages, int Sizes, int LocalClickChecks, int StateRetentionChecks, int ImageBindingChecks, int CustomColorChecks, int NativeMetricsChecks, int LibraryChecks, int MenuChecks, int LanguageChecks, int PlacementChecks);
