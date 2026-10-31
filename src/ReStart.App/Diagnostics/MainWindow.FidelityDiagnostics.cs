using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using ReStart.App.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    internal async Task<FidelitySmokeResult> VerifyFidelityAsync(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("Fidelity verification requires preview mode.");
        var directory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "fixture-" + Guid.NewGuid().ToString("N"));
        var apps = new List<AppEntry> { new("alpha", "Alpha Editor", ""), new("beta", "Beta Editor", "") };
        var launched = new List<AppEntry>();
        var fixture = new MainWindow(new LayoutStore(Path.Combine(directory, "layout.json")), true, () => Task.FromResult(apps), launched.Add)
        { Owner = this, Width = 1280, Height = 800, Topmost = false };
        var language = L.Language; var checks = 0;
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        async Task Until(Func<bool> condition, int timeoutMs = 2500, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string description = "")
        {
            var deadline = Environment.TickCount64 + timeoutMs;
            while (!condition() && Environment.TickCount64 < deadline) await Task.Delay(25);
            Check(condition(), "An asynchronous fidelity operation failed: " + description);
        }
        try
        {
            fixture.Present(); await fixture.InitializeAsync(); fixture._clock.Stop();
            checks += await VerifyTabletIntegrationAsync(fixture);
            checks += VerifyTouchPanningLease();
            checks += fixture.VerifyTouchDragRoutes();
            fixture.Document.Preferences.Animate = false;
            foreach (var lang in TextCatalog.SupportedLanguages)
            {
                L.SetLanguage(lang);
                var settings = SettingsSearch.Find(L.T(K.WindowsDisplay));
                Check(settings.Any(a => a.LaunchTarget == "ms-settings:display"), "Localized settings search did not resolve Display.");
                fixture.Document.Preferences.NavigationFolders = FolderShortcuts.Select(s => s.Id).ToList();
                fixture.RefreshNavigationShortcuts(); fixture.UpdateLayout();
                fixture.SetRailOpen(true); fixture.UpdateLayout();
                Check(fixture.CollapsedFolderShortcuts.Children.Count == 7 && fixture.ExpandedFolderShortcuts.Children.Count == 7,
                    "Navigation shortcut preferences do not match both sidebar variants.");
                Check(fixture.ExpandedFolderShortcuts.Children.OfType<Button>().All(b => AutomationProperties.GetName(b).Length > 0
                    && b.Content is RailLabel), "A navigation shortcut lost its accessible label or fixed icon column.");
                Check(fixture.NavigationGlyphsStayAligned(), "Scrollable navigation shortcuts changed geometry on expansion.");
            }
            L.SetLanguage("en");
            fixture.Document.Preferences.Language = "en";
            var localizedTerminal = new AppEntry("terminal-package", "\u30bf\u30fc\u30df\u30ca\u30eb", "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App");
            var localizedPhotos = new AppEntry("photos-package", "\u30d5\u30a9\u30c8", "Microsoft.Windows.Photos_8wekyb3d8bbwe!App");
            Check(PreviewCatalog.Match(new("terminal", "Windows Terminal", "wt.exe", false), [localizedTerminal]) == localizedTerminal
                && PreviewCatalog.Match(new("photos", "Photos", "ms-photos:", false), [localizedPhotos]) == localizedPhotos,
                "English preview labels could not resolve installed icons in a localized catalog.");
            fixture._fileSearchFixture = async (query, _) =>
            {
                await Task.Delay(query == "obsolete" ? 650 : 30);
                return (new AppEntry[] { new("restart:file:" + query, query + " document", "", false) }, true);
            };
            fixture.SetSmokeSearch("Editor");
            Check(fixture.SearchResultNames.SequenceEqual(new[] { "Alpha Editor", "Beta Editor" }), "File search delayed immediate app results.");
            await Until(() => fixture.SearchResultNames.Contains("Editor document"));
            Check(((Button)fixture.SearchResults.SelectedItem).Tag is AppEntry { Id: "alpha" }, "Async file results displaced the selected app.");
            fixture.SetSmokeSearch("obsolete"); await Task.Delay(230); fixture.SetSmokeSearch("current");
            await Until(() => fixture.SearchResultNames.Contains("current document")); await Task.Delay(700);
            Check(!fixture.SearchResultNames.Contains("obsolete document"), "A stale file query overwrote the current search.");
            fixture._fileSearchFixture = (_, _) => Task.FromResult<(IReadOnlyList<AppEntry>, bool)>(([], false));
            fixture.SetSmokeSearch("unavailable");
            await Until(() => fixture.SearchResultHint.Text == L.T(K.SearchIndexUnavailable));
            fixture.SetSmokeSearch("Display");
            Check(fixture.SearchResultNames.Contains(L.T(K.WindowsDisplay)), "Settings did not appear in search.");
            ((Button)fixture.SearchResults.SelectedItem).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(launched.Last().LaunchTarget == "ms-settings:display", "Search invoked a different settings target.");
            fixture.Document.Preferences.SearchSettings = false; fixture.RefreshSearchResults();
            Check(fixture.SearchResults.Items.OfType<Button>().All(b => b.Tag is not AppEntry a || !a.Id.StartsWith("restart:setting:", StringComparison.Ordinal)),
                "Disabling settings search left settings results visible.");
            fixture.Document.Preferences.SearchSettings = true;
            fixture.ResetToPinnedView();
            await Task.Delay(250);
            Check(fixture._fileSearchResults.Count == 0 && fixture._fileSearchQuery.Length == 0, "Returning home retained file search results.");

            fixture.Document.Preferences.ShowMostUsed = true;
            fixture.Activity.RecordLaunch("beta", true, DateTimeOffset.Now); fixture.RenderApps();
            Check(VisualChildren<TextBlock>(fixture.AppsPanel).Any(t => t.Text == L.T(K.MostUsed)), "Most used was not rendered from local launch counts.");
            fixture.ClearAppHistory();
            Check(!VisualChildren<TextBlock>(fixture.AppsPanel).Any(t => t.Text == L.T(K.MostUsed)), "Cleared app history still appears.");
            fixture._windowsUsageFixture = async (_, token) =>
            {
                await Task.Delay(40, token);
                return new(true, new Dictionary<string, WindowsAppUsage> { ["beta"] = new(12, DateTimeOffset.UtcNow) });
            };
            fixture.Document.Preferences.UseWindowsAppHistory = true; fixture.RenderApps();
            await Until(() => fixture._windowsUsage.ContainsKey("beta"));
            Check(fixture.AppsPanel.Children.OfType<Button>().First(b => b.Uid == "history").Tag is AppEntry { Id: "beta" },
                "Windows usage did not reach the Most used surface.");
            fixture.Activity.HideFrequent("beta"); fixture.RenderApps();
            Check(!fixture.AppsPanel.Children.OfType<Button>().Any(b => b.Uid == "history" && b.Tag is AppEntry { Id: "beta" }),
                "An imported usage observation restored an excluded app.");
            fixture.Document.Preferences.UseWindowsAppHistory = false; fixture.RenderApps();
            Check(fixture._windowsUsage.Count == 0 && fixture._windowsUsageCancellation == null, "Disabling Windows usage retained history or its reader.");
            fixture._windowsUsageFixture = null;
            fixture.SetRailOpen(true); Check(fixture.ExpandedRail.Visibility == Visibility.Visible && !fixture._railPinnedOpen, "Hover expansion was treated as a pinned-open sidebar.");
            fixture.SetRailOpen(true, true); Check(fixture._railPinnedOpen, "Explicit sidebar expansion did not stay open.");
            fixture.SetRailOpen(false);

            var recent = new MenuItem(); var frequent = new MenuItem();
            fixture.PopulateJumpItems(recent, frequent, apps[0], [new("Recent sample", "sample.txt", false), new("Frequent sample", "other.txt", true)]);
            Check(recent.Items.Count == 1 && frequent.Items.Count == 1, "Jump list categories were mixed.");
            ((MenuItem)recent.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(launched.Last().Id == "alpha" && launched.Last().LaunchTarget == "sample.txt", "A jump destination lost its owning app identity.");
            fixture.PopulateJumpItems(recent, frequent, apps[0], []);
            Check(recent.Visibility == Visibility.Collapsed && frequent.Visibility == Visibility.Collapsed, "An empty jump list left empty menu categories.");
            Check((await AppJumpLists.ReadAsync(new("nonexistent", "Fixture", "ReStart.Nonexistent.Fidelity"))).Count == 0,
                "An unknown app inherited another app's recent documents.");
            var probe = Environment.GetEnvironmentVariable("RESTART_TEST_SHELL_PROBE");
            Check(!string.IsNullOrEmpty(probe) && File.Exists(probe), "The native document argument probe was not supplied.");
            var document = Path.Combine(directory, "Document \u4f8b_%[1].txt"); File.WriteAllText(document, "Fictional document argument fixture.");
            await AppShellCommands.OpenDocumentAsync(new("probe", "Shell probe", probe!, false), document, new System.Windows.Interop.WindowInteropHelper(fixture).Handle);
            await Until(() => File.Exists(document + ".opened"));
            Check(JsonSerializer.Deserialize<string[]>(File.ReadAllText(document + ".opened"))?.SequenceEqual(new[] { document }) == true,
                "The app's native open verb corrupted or split the Unicode document path.");
            var configuredDocument = Path.Combine(directory, "Configured \u4f8b document.txt"); File.WriteAllText(configuredDocument, "Fictional configured launch.");
            var configuredArguments = "--sample \"argument one\" \"" + configuredDocument + "\"";
            await AppShellCommands.LaunchConfiguredAsync(new("probe", "Shell probe", probe!, false), configuredArguments, directory, false,
                new System.Windows.Interop.WindowInteropHelper(fixture).Handle);
            await Until(() => File.Exists(configuredDocument + ".cwd"));
            Check(JsonSerializer.Deserialize<string[]>(File.ReadAllText(configuredDocument + ".opened"))?.SequenceEqual(new[] { "--sample", "argument one", configuredDocument }) == true,
                "Configured launch arguments were split or quoted incorrectly.");
            Check(Path.GetFullPath(File.ReadAllText(configuredDocument + ".cwd")).Equals(Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase),
                "The configured working directory was not passed to the application.");
            var shortcut = Path.Combine(directory, "Probe shortcut.lnk");
            var shortcutDocument = Path.Combine(directory, "Shortcut document.txt");
            File.WriteAllText(shortcutDocument, "Fictional shortcut launch.");
            var shortcutArguments = "--shortcut \"" + shortcutDocument + "\"";
            await AppShellCommands.OnSta(() =>
            {
                object? shell = null, link = null;
                try
                {
                    shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
                    link = ((dynamic)shell!).CreateShortcut(shortcut);
                    ((dynamic)link).TargetPath = probe!;
                    ((dynamic)link).Arguments = shortcutArguments;
                    ((dynamic)link).WorkingDirectory = directory;
                    ((dynamic)link).Save();
                    Check(((dynamic)link).Arguments == shortcutArguments, "The shortcut did not retain its stored arguments.");
                    return true;
                }
                finally
                {
                    if (link != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);
                    if (shell != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
                }
            });
            // Launch only our own no-window probe; real apps and UAC are never invoked by this fixture.
            ShellLauncher.Launch(shortcut);
            await Until(() => File.Exists(shortcutDocument + ".cwd"), timeoutMs: 10000);
            Check(JsonSerializer.Deserialize<string[]>(File.ReadAllText(shortcutDocument + ".opened"))?.SequenceEqual(new[] { "--shortcut", shortcutDocument }) == true,
                "A shortcut's existing arguments were lost.");
            Check((await AppShellCommands.ReadAsync(new("probe", "Shell probe", probe!, false))).Contains("runas"),
                "The desktop app did not expose administrator execution through its canonical verb.");

            var group = new TileGroup { Name = "Sample group", Tiles = [new() { Label = "Sample tile" }] };
            fixture.Document.Groups = [group]; fixture.RenderGroups(); fixture.UpdateLayout();
            Check(VisualChildren<TileSurfaceButton>(fixture.GroupsPanel).Single().Style == fixture.FindResource("TileSurfaceAction"), "Tile hover uses ordinary button borders.");
            var before = JsonSerializer.Serialize(fixture.Document.Groups);
            fixture._dragging = true; fixture._activeDragFormat = "ReStart.Group"; fixture._activeDragId = group.Id;
            fixture.PreserveDragExtent();
            var point = new Point(fixture.ContentScroll.ViewportWidth / 2, fixture.ContentScroll.ViewportHeight - 2);
            for (var i = 0; i < 10; i++) fixture.ScrollDragAt(point, .032);
            Check(fixture.ContentScroll.VerticalOffset > 0, "Stationary edge scrolling did not advance.");
            Check(JsonSerializer.Serialize(fixture.Document.Groups) == before, "Scroll previews mutated persisted layout coordinates.");
            fixture._dragging = false; fixture._activeDragId = null; fixture.ClearDropPreview();
            fixture.Document.Preferences.DragEdgeScrolling = false;
            var stoppedOffset = fixture.ContentScroll.VerticalOffset;
            Check(!fixture.ScrollDragAt(point, .1) && fixture.ContentScroll.VerticalOffset == stoppedOffset, "Disabled edge scrolling still moved the viewport.");
            fixture.Document.Preferences.DragEdgeScrolling = true;
            fixture.GroupsPanel.DragMinimumHeight = 0; fixture.GroupsPanel.DragMinimumWidth = 0;
            fixture.ContentScroll.ScrollToTop(); fixture.ContentScroll.ScrollToLeftEnd();
            fixture.Document.Preferences.Language = "en";
            var toggles = new Dictionary<K, Func<Preferences, bool>>
            {
                [K.SettingsRailHover] = p => p.ExpandNavigationOnHover,
                [K.SettingsSearchSettings] = p => p.SearchSettings,
                [K.SearchFilesSetting] = p => p.SearchFiles,
                [K.SettingsWindowsSearchSyntax] = p => p.SearchWindowsSyntax,
                [K.SettingsWindowsSearchHandoff] = p => p.ShowWindowsSearchHandoff,
                [K.SettingsLegacyUwpTiles] = p => p.LegacyUwpLiveTiles,
                [K.SettingsJumpLists] = p => p.ShowJumpLists,
                [K.SettingsAppTasks] = p => p.ShowAppTasks,
                [K.SettingsPinnedJumpLists] = p => p.ShowPinnedJumpLists,
                [K.SettingsTileHover] = p => p.TileHoverHighlight,
                [K.SettingsGroupHover] = p => p.ShowGroupBoundariesOnHover,
                [K.SettingsFreePlacement] = p => p.FreeTilePlacement,
                [K.SettingsDropGroups] = p => p.GroupTilesOnDrop,
                [K.SettingsDropFolders] = p => p.CreateFoldersOnDrop,
                [K.SettingsDragScroll] = p => p.DragEdgeScrolling,
                [K.SettingsDropHints] = p => p.ShowDropHints,
                [K.SettingsDragShortcutHints] = p => p.ShowDragShortcutHints,
                [K.SettingsShowRecentApps] = p => p.ShowRecentlyAdded,
                [K.SettingsShowMostUsed] = p => p.ShowMostUsed,
                [K.SettingsWindowsAppHistory] = p => p.UseWindowsAppHistory
            };
            foreach (var enabled in new[] { false, true })
            {
                Exception? editorError = null;
                var editor = new SettingsWindow(fixture, fixture._store) { Owner = fixture };
                editor.Loaded += (_, _) => editor.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        editor.SelectSection(L.T(K.SettingsStartExperience));
                        editor.UpdateLayout();
                        foreach (var key in toggles.Keys)
                            VisualChildren<CheckBox>(editor).Single(c => AutomationProperties.GetName(c) == L.T(key)).IsChecked = enabled;
                        if (enabled)
                        {
                            editor.UpdateLayout(); SaveFixtureElement(editor, Path.Combine(directory, "start-behavior.png"));
                            var scroll = VisualChildren<ScrollViewer>(editor).Single(v => v.Content is StackPanel p && VisualChildren<CheckBox>(p).Count() >= toggles.Count);
                            scroll.ScrollToVerticalOffset(470); editor.UpdateLayout();
                            SaveFixtureElement(editor, Path.Combine(directory, "start-behavior-layout.png"));
                        }
                        VisualChildren<Button>(editor).Single(b => b.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch (Exception ex) { editorError = ex; editor.DiscardAndClose(); }
                });
                editor.ShowDialog();
                if (editorError != null) throw new InvalidOperationException("Start preference editor failed: " + editorError.Message, editorError);
                var saved = fixture._store.Load()!.Preferences;
                Check(toggles.Values.All(read => read(saved) == enabled), "A Start feature toggle did not survive save and reload.");
                fixture.RefreshLayout();
                Check(VisualChildren<TileSurfaceButton>(fixture.GroupsPanel).All(b => b.HoverHighlightEnabled == enabled), "Saved hover preference was not applied to tiles.");
                Check(!enabled || fixture.Document.Preferences.NavigationFolders?.Count == 7, "Navigation shortcuts were lost while saving feature switches.");
            }
            fixture.Document.Preferences.ShowMostUsed = false;
            var launchTile = group.Tiles[0]; launchTile.Target = probe!; launchTile.IsShellApp = false;
            Exception? launchEditorError = null;
            var launchEditor = new LaunchOptionsWindow(launchTile, true, true) { Owner = fixture };
            launchEditor.Loaded += (_, _) => launchEditor.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var enabled = VisualChildren<CheckBox>(launchEditor).Single(c => AutomationProperties.GetName(c) == L.T(K.UseLaunchOptions));
                    enabled.IsChecked = true;
                    var args = VisualChildren<TextBox>(launchEditor).Single(c => AutomationProperties.GetName(c) == L.T(K.LaunchArguments));
                    var cwd = VisualChildren<TextBox>(launchEditor).Single(c => AutomationProperties.GetName(c) == L.T(K.WorkingDirectory));
                    args.Text = configuredArguments; cwd.Text = Path.Combine(directory, "Missing directory");
                    var save = VisualChildren<Button>(launchEditor).Single(b => b.IsDefault);
                    save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(launchEditor.IsVisible && !launchTile.UseLaunchOptions, "Invalid launch options were committed before validation.");
                    cwd.Text = directory;
                    VisualChildren<CheckBox>(launchEditor).Single(c => AutomationProperties.GetName(c) == L.T(K.RunAsAdministrator)).IsChecked = true;
                    save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch (Exception ex) { launchEditorError = ex; launchEditor.Close(); }
            });
            launchEditor.ShowDialog();
            if (launchEditorError != null) throw new InvalidOperationException("Launch options editor failed: " + launchEditorError.Message, launchEditorError);
            fixture.CommitLayout();
            var savedLaunch = fixture._store.Load()!.Groups.SelectMany(g => g.Tiles).Single(t => t.Id == launchTile.Id);
            Check(savedLaunch.UseLaunchOptions && savedLaunch.LaunchArguments == configuredArguments && savedLaunch.WorkingDirectory == directory && savedLaunch.RunAsAdministrator,
                "Per-tile launch options did not survive the native editor and layout persistence.");
            launchTile.UseLaunchOptions = false; launchTile.RunAsAdministrator = false;
            foreach (var tag in L.SupportedLanguages)
                foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
                {
                    fixture.Document.Preferences.Language = tag; fixture.Document.Preferences.Theme = theme; fixture.ApplyAppearance();
                    var demoLaunch = new LaunchOptionsWindow(new Tile
                    {
                        Label = "Sample Editor",
                        UseLaunchOptions = true,
                        LaunchArguments = "--new-window \"sample project.txt\"",
                        WorkingDirectory = @"%USERPROFILE%\Documents"
                    }, true, true)
                    { Owner = fixture };
                    Exception? themeError = null;
                    demoLaunch.Loaded += (_, _) => demoLaunch.Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            demoLaunch.UpdateLayout();
                            Check(demoLaunch.Background is SolidColorBrush background && background.Color == ((SolidColorBrush)fixture.FindResource("PanelBrush")).Color
                                && demoLaunch.Foreground is SolidColorBrush foreground && foreground.Color == ((SolidColorBrush)fixture.FindResource("TextBrush")).Color,
                                "The launch editor did not inherit the selected window palette.");
                            Check(VisualChildren<CheckBox>(demoLaunch).All(c => c.Content is string text && text.Length > 0), "A localized launch option has no visible label.");
                            if (tag == "en") SaveFixtureElement(demoLaunch, Path.Combine(directory, theme == ReStart.Core.ThemeMode.Dark ? "launch-options.png" : "launch-options-light.png"));
                        }
                        catch (Exception ex) { themeError = ex; }
                        finally { demoLaunch.Close(); }
                    });
                    demoLaunch.ShowDialog();
                    if (themeError != null) throw new InvalidOperationException("Launch editor appearance failed.", themeError);
                }
            fixture.Document.Preferences.Language = "en";
            foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
            {
                fixture.ResetToPinnedView();
                fixture.Document.Preferences.Theme = theme; fixture.RefreshLayout(); fixture.SetRailOpen(true, true);
                fixture.SaveScreenshot(Path.Combine(directory, "navigation-" + theme + ".png"));
                fixture.SetRailOpen(false); fixture.SetSmokeSearch("Display");
                fixture.SaveScreenshot(Path.Combine(directory, "settings-search-" + theme + ".png"));
            }
            await fixture.SaveSearchShowcaseAsync(directory);
            checks += await VerifySessionMenusAsync(fixture, directory);
            checks += await VerifyLegacyTileRenderingAsync(fixture, directory);
            checks += await VerifyWindowsSearchAsync(fixture, launched, directory);
            checks += await IndexedFileSearch.VerifyOwnedFieldsAsync();
            var indexed = await new IndexedFileSearch().FindAsync("ReStart_nonexistent_%_[fidelity]", CancellationToken.None);
            Check(indexed.Files.Count == 0, "The native index query ignored literal wildcard characters.");
            var contentIndex = await new IndexedFileSearch().FindAsync("ReStart_nonexistent_'[fidelity]", CancellationToken.None, true);
            Check(!indexed.Available || contentIndex.Available, "The native file-content query failed on an available Windows index.");
            Check(contentIndex.Files.Count == 0, "The native content query interpreted a literal fixture as SQL or full-text operators.");
            var advanced = await new IndexedFileSearch().FindAsync("System.FileName:ReStartOwnedAbsent_fidelity_41f0d1", CancellationToken.None, false, true);
            Check(!indexed.Available || advanced.Available, "A native advanced query failed on an available local Windows index.");
            Check(advanced.Files.Count == 0, "The native AQS query escaped its fixed local projection.");
            // Reuse the bounded empty filename pattern to isolate wrapper lifetime;
            // broad leading-wildcard/content queries may legitimately reach the provider deadline.
            var repeated = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
            {
                var reader = new IndexedFileSearch();
                var result = await reader.FindAsync("ReStart_nonexistent_%_[fidelity]", CancellationToken.None);
                return (Count: result.Files.Count, result.Available, reader.LastFailureCode);
            }));
            Check(repeated.All(result => result.Count == 0 && result.Available == indexed.Available),
                "Overlapping index readers changed availability or results: " + string.Join(", ", repeated.Select(result => $"{result.Available}/{result.Count}/{result.LastFailureCode:X8}")));
            using (var cancelledIndex = new CancellationTokenSource())
            {
                cancelledIndex.Cancel(); var cancelled = false;
                try { await new IndexedFileSearch().FindAsync("ReStart_nonexistent_cancelled", cancelledIndex.Token, true); }
                catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled, "A cancelled index query performed work.");
            }
            Check((await new IndexedFileSearch().FindAsync("ReStart_nonexistent_%_[fidelity]", CancellationToken.None)).Available == indexed.Available,
                "A cancelled index reader retained the shared query gate.");
            return new(checks, indexed.Available);
        }
        finally { fixture.Exit(); L.SetLanguage(language); ApplyAppearance(); Activate(); }
    }
}

internal sealed record FidelitySmokeResult(int Checks, bool NativeIndexAvailable);
