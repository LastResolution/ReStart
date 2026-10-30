using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Opens real WPF menus on isolated app-list/search/tile/folder surfaces; external actions are captured, never executed.</summary>
    private async Task<(int Checks, int LanguageThemeCases, int NativeReads, bool NativeHistoryRoundTrip)> VerifyAppMenuSurfacesAsync(MainWindow fixture, string directory, string snapshotPath)
    {
        var checks = 0; var themes = 0; var reads = 0;
        void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); checks++; }
        static MenuItem? Find(ItemsControl menu, string tag) => menu.Items.OfType<MenuItem>().FirstOrDefault(i => Equals(i.Tag, tag));
        static IEnumerable<MenuItem> Commands(ItemsControl menu) => menu.Items.OfType<MenuItem>().SelectMany(i => new[] { i }.Concat(Commands(i)));
        static bool Visible(MenuItem? item) => item is { Visibility: Visibility.Visible };
        var file = Path.Combine(directory, "Sample 日本語, & # (one).txt"); File.WriteAllText(file, "owned menu fixture");
        var folderPath = Path.Combine(directory, "Folder 日本語, & #"); Directory.CreateDirectory(folderPath);
        var linkPath = Path.Combine(directory, "Command sample.lnk");
        object? script = null, link = null;
        try
        {
            script = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            link = ((dynamic)script!).CreateShortcut(linkPath);
            ((dynamic)link).TargetPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            ((dynamic)link).Arguments = "/k fixture-only"; ((dynamic)link).Save();
        }
        finally { if (link != null) Marshal.FinalReleaseComObject(link); if (script != null) Marshal.FinalReleaseComObject(script); }
        var local = FilePinning.FromPath(file);
        var directoryApp = FilePinning.FromPath(folderPath);
        var desktop = new AppEntry("desktop-menu-fixture", "Command sample", linkPath, false) { ShortcutPath = linkPath };
        var packaged = new AppEntry("package-fixture!app", "Packaged sample", "package-fixture!app");
        var native = new Dictionary<string, IReadOnlySet<string>>();
        var nativeHistory = await AppJumpLists.VerifyOwnedHistoryAsync(directory); reads += nativeHistory.Checks;
        foreach (var app in new[] { local, directoryApp, FilePinning.FromPath(linkPath), desktop })
        {
            var verbs = await AppShellCommands.ReadAsync(app); reads++;
            Check(verbs.Contains("opencontaining") && verbs.Contains("properties"), "A real local item lacks location/properties commands.");
            native[app.Id] = verbs;
        }
        Check(native[directoryApp.Id].Contains(AppShellCommands.FolderLocation) && !native[local.Id].Contains(AppShellCommands.FolderLocation), "Directory location labels were not classified on the Shell worker.");
        var actualCatalog = await new AppCatalog().LoadAsync();
        var actualDesktop = actualCatalog.FirstOrDefault(a => a.IsShellApp && a.SearchAliases.Contains("cmd.exe", StringComparer.OrdinalIgnoreCase));
        if (actualDesktop != null)
        {
            var verbs = await AppShellCommands.ReadAsync(actualDesktop); reads++;
            Check(verbs.Contains("opencontaining") && verbs.Contains("runas"), "AppsFolder's actual desktop commands were lost during canonical-name normalization.");
        }
        var actualPackage = actualCatalog.FirstOrDefault(a => a.IsShellApp && a.Id.StartsWith("Microsoft.WindowsNotepad_", StringComparison.OrdinalIgnoreCase))
            ?? actualCatalog.FirstOrDefault(a => a.IsShellApp && a.Id.StartsWith("Microsoft.WindowsCalculator_", StringComparison.OrdinalIgnoreCase));
        if (actualPackage != null && AppPackageCommands.Read(actualPackage) is { } package)
        {
            var verbs = await AppShellCommands.ReadAsync(actualPackage); reads++;
            Check((verbs.Contains("appsettings") || verbs.Contains("manageapps")) && verbs.Contains("review") == package.StoreSigned, "An installed package's management/Store capabilities do not match its registration.");
        }
        var extended = await AppShellCommands.ReadAsync(FilePinning.FromPath(linkPath), true); reads++;
        Check(extended.Contains("runasuser"), "Shift's extended shortcut menu lost Run as different user.");
        Check((await AppShellCommands.ReadAsync(new("missing", "Missing", Path.Combine(directory, "missing.exe"), false))).Count == 0, "A missing local item exposes actions."); reads++;
        Check(AppPackageCommands.Target(new("Package_sample", true), "manageapps") == "ms-settings:appsfeatures"
            && AppPackageCommands.Target(new("Package_sample", true), "appsettings") == null
            && AppPackageCommands.Target(new("Package_sample", true), "review") == "ms-windows-store://review/?PFN=Package_sample"
            && AppPackageCommands.Target(new("Package_sample", false), "review") == null, "Package links are malformed or invent Store reviews for non-Store packages.");
        var invoked = new List<(string App, string Verb)>();
        fixture._appCommandFixture = (app, verb) => { invoked.Add((app.Id, verb)); return Task.CompletedTask; };
        fixture._jumpDestinationsFixture = _ => Task.FromResult<IReadOnlyList<JumpDestination>>([]);
        IReadOnlySet<string> Capabilities(AppEntry app) => app.Id == packaged.Id
            ? new HashSet<string>(["open", "runas", "manageapps", "review", "uninstall"], StringComparer.OrdinalIgnoreCase) : native[app.Id];
        fixture._shellCommandsFixture = (app, _) => Task.FromResult(Capabilities(app));
        var language = L.Language;
        try
        {
            foreach (var tag in L.SupportedLanguages)
                foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
                {
                    fixture.Document.Preferences.Language = tag; fixture.Document.Preferences.Theme = theme;
                    fixture.Document.Preferences.LayoutLocked = false; fixture.ApplyAppearance();
                    foreach (var app in new[] { desktop, directoryApp, local, packaged })
                        foreach (var search in new[] { false, true })
                        {
                            var button = fixture.AppButton(app, search); fixture.AppsPanel.Children.Add(button);
                            var menu = button.ContextMenu!; menu.PlacementTarget = button;
                            menu.IsOpen = true; await Task.Delay(10);
                            var more = Find(menu, "shell-more")!;
                            var commands = search ? menu : (ItemsControl)more;
                            Check(Visible(Find(menu, "open")) && Visible(Find(menu, "pin")), "An app surface lacks Open or pin actions.");
                            Check(search ? !Visible(more) : Visible(more), "Search actions are nested in More, or app-list actions escaped More.");
                            if (tag == "en" && app == desktop)
                            {
                                SaveMenuSnapshot(menu, Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"menu-{(search ? "search" : "apps")}-{theme}.png"));
                                if (!search)
                                {
                                    more.IsSubmenuOpen = true; await Task.Delay(10);
                                    if (more.Template.FindName("PART_Popup", more) is Popup popup && popup.Child is FrameworkElement content)
                                        SaveMenuSnapshot(content, Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"menu-more-{theme}.png"));
                                    more.IsSubmenuOpen = false;
                                }
                            }
                            if (app != packaged)
                            {
                                var location = Find(commands, "opencontaining");
                                Check(Visible(location) && location!.IsEnabled && Equals(location.Header, L.T(app == directoryApp ? K.OpenFolderLocation : K.OpenFileLocation)), "An opened menu lacks the correctly translated location action.");
                                var before = invoked.Count; location!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                                Check(invoked.Count == before + 1 && invoked[^1] == (app.Id, "opencontaining"), "A location click invoked the wrong app or command.");
                                Check(Visible(Find(commands, "properties")), "A local item's Properties command disappeared.");
                                Check(Visible(Find(commands, "share")) == (app == local && FileShareSession.IsSupported),
                                    "Share is missing for an accessible file or exposed for a folder/application shortcut.");
                                var propertyCount = invoked.Count;
                                Find(commands, "properties")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                                Check(invoked.Count == propertyCount + 1 && invoked[^1] == (app.Id, "properties"), "Properties did not retain the clicked file or shortcut identity.");
                                if (Find(commands, "share") is { } share)
                                {
                                    var shareCount = invoked.Count; share.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                                    Check(invoked.Count == shareCount + 1 && invoked[^1] == (app.Id, "share"), "Share did not retain the clicked file identity.");
                                }
                                Check(search == Visible(Find(commands, "copyaspath")), "Copy full path is missing from search or incorrectly exposed in the Start More menu.");
                            }
                            else
                                Check(Visible(Find(commands, "manageapps")) && !Visible(Find(commands, "appsettings")) && Visible(Find(commands, "review")) && Visible(Find(menu, "uninstall"))
                                    && !Visible(Find(commands, "opencontaining")), "Packaged actions or unavailable filesystem actions are incorrectly represented.");
                            Check(!Commands(menu).Any(i => i.Header?.ToString() is "MenuOpen" or "OpenFileLocation" or "OpenFolderLocation"), "A menu rendered resource identifiers instead of translations.");
                            menu.IsOpen = false; fixture.AppsPanel.Children.Remove(button);
                        }
                    var tile = Tile.FromApp(local);
                    var child = Tile.FromApp(directoryApp);
                    var tileFolder = new Tile { Label = "Tile folder", Children = [child] };
                    foreach (var standalone in new[] { false, true })
                    {
                        fixture._openFolder = null;
                        fixture.Document.Groups = [new() { IsStandalone = standalone, Tiles = [tile, tileFolder] }];
                        fixture.RenderGroups(); fixture.UpdateLayout();
                        var button = VisualChildren<Button>(fixture.GroupsPanel).FirstOrDefault(b => ReferenceEquals(b.Tag, tile))
                            ?? throw new InvalidOperationException("The menu fixture's root tile did not render.");
                        var menu = button.ContextMenu!; menu.PlacementTarget = button; menu.IsOpen = true; await Task.Delay(10);
                        Check(Visible(Find(Find(menu, "shell-more")!, "opencontaining")), "A pinned standalone/group tile lacks More > Open file location.");
                        menu.IsOpen = false;
                        fixture.OpenFolder(tileFolder); fixture.UpdateLayout();
                        button = VisualChildren<Button>(fixture.GroupsPanel).FirstOrDefault(b => ReferenceEquals(b.Tag, child))
                            ?? throw new InvalidOperationException("The menu fixture's expanded folder child did not render.");
                        menu = button.ContextMenu!; menu.PlacementTarget = button; menu.IsOpen = true; await Task.Delay(10);
                        Check(Visible(Find(Find(menu, "shell-more")!, "opencontaining")), "An expanded tile-folder child lacks location actions.");
                        menu.IsOpen = false;
                        Check(!Commands(fixture.TileMenu(tileFolder)).Any(i => i.Tag is "shell-more"), "A virtual tile folder exposes a filesystem location.");
                        fixture.Document.Preferences.LayoutLocked = true;
                        menu = fixture.TileMenu(tile);
                        Check(menu.Items.OfType<MenuItem>().First(i => Equals(i.Header, L.T(K.UnpinTile))).IsEnabled == false
                            && Find(menu, "shell-more") != null, "Layout locking hides Shell actions or leaves unpin enabled.");
                        fixture.Document.Preferences.LayoutLocked = false;
                    }
                    var live = new Tile { Widget = "clock", Live = true };
                    var liveMenu = fixture.TileMenu(live);
                    Check(Find(liveMenu, "live-more") is { } liveMore && Find(liveMore, "toggle-live") != null
                        && Find(liveMenu, "shell-more") == null, "Live controls are misplaced or expose file actions for a widget.");
                    foreach (var (verb, label) in new[] { ("runasuser", K.RunAsDifferentUser), ("taskbarpin", K.PinToTaskbar), ("taskbarunpin", K.UnpinFromTaskbar), ("share", K.ShareItem), ("appsettings", K.AppSettings) })
                    {
                        var more = new MenuItem(); var uninstall = new MenuItem();
                        fixture.PopulateAppShellItems(more, uninstall, desktop, new HashSet<string>([verb]));
                        Check(more.Items.OfType<MenuItem>().Single() is { } command && Equals(command.Tag, verb) && Equals(command.Header, L.T(label)),
                            "An optional Shell action was mislabeled or mixed with unavailable commands.");
                    }
                    await VerifyHistoryMenusAsync(fixture, local, directoryApp, theme, snapshotPath, Check);
                    themes++;
                }
            fixture.Document.Preferences.Language = "en"; fixture.ApplyAppearance();
            await VerifyMenuRequestLifetimeAsync(fixture, desktop, native[desktop.Id], Check);
            return (checks, themes, reads, nativeHistory.RoundTrip);
        }
        finally
        {
            fixture._shellCommandsFixture = null; fixture._jumpDestinationsFixture = null; fixture._appCommandFixture = null;
            fixture._removeJumpDestinationFixture = null; fixture._menuSettingsFixture = null;
            L.SetLanguage(language);
        }
    }

    private static void SaveMenuSnapshot(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0) throw new InvalidOperationException("The opened menu has no rendered bounds.");
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }

    private static async Task VerifyMenuRequestLifetimeAsync(MainWindow fixture, AppEntry app, IReadOnlySet<string> commands, Action<bool, string> check)
    {
        var jumps = new TaskCompletionSource<IReadOnlyList<JumpDestination>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture._jumpDestinationsFixture = _ => jumps.Task;
        fixture._shellCommandsFixture = (_, _) => Task.FromResult(commands);
        var button = fixture.AppButton(app, true); fixture.AppsPanel.Children.Add(button);
        var menu = button.ContextMenu!; menu.PlacementTarget = button; menu.IsOpen = true; await Task.Delay(20);
        check(menu.Items.OfType<MenuItem>().Any(i => i.Tag is "opencontaining"), "A pending Jump List blocks the app's Shell commands.");
        await CloseMenuAsync(menu); jumps.SetResult([]);
        var old = new TaskCompletionSource<IReadOnlySet<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture._shellCommandsFixture = (_, _) => old.Task;
        menu.IsOpen = true; await Task.Delay(10); await CloseMenuAsync(menu);
        fixture._shellCommandsFixture = (_, _) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(["runas"]));
        menu.IsOpen = true; await Task.Delay(10); old.SetResult(commands); await Task.Delay(20);
        check(menu.Items.OfType<MenuItem>().Any(i => i.Tag is "runas") && !menu.Items.OfType<MenuItem>().Any(i => i.Tag is "opencontaining"),
            "A stale Shell request replaced a reopened menu's capabilities: " + string.Join(", ", menu.Items.OfType<MenuItem>().Select(i => $"{i.Tag}:{i.Visibility}")));
        await CloseMenuAsync(menu);
        fixture._jumpDestinationsFixture = _ => Task.FromException<IReadOnlyList<JumpDestination>>(new IOException("Owned history fixture failure."));
        fixture._shellCommandsFixture = (_, _) => Task.FromResult(commands);
        menu.IsOpen = true; await Task.Delay(10);
        check(menu.Items.OfType<MenuItem>().Any(i => i.Tag is "opencontaining"), "A failed history provider hides the app's commands.");
        await CloseMenuAsync(menu);
        fixture._shellCommandsFixture = (_, _) => Task.FromException<IReadOnlySet<string>>(new TimeoutException("Owned capability fixture timeout."));
        menu.IsOpen = true; await Task.Delay(10);
        check(menu.Items.OfType<MenuItem>().Any(i => i.Tag is "open") && !menu.Items.OfType<MenuItem>().Any(i => i.Tag is "opencontaining")
            && menu.Items.OfType<MenuItem>().Single(i => i.Tag is "shell-more").Visibility == Visibility.Collapsed,
            "A failed capability request leaves a loading placeholder or stale commands instead of the basic menu.");
        await CloseMenuAsync(menu);
        var reopened = new TaskCompletionSource<IReadOnlySet<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture._shellCommandsFixture = (_, _) => reopened.Task;
        menu.IsOpen = true; await Task.Delay(10);
        // A prior popup animation can deliver Closed after a newer open.
        menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent));
        reopened.SetResult(commands); await Task.Delay(20);
        check(menu.Items.OfType<MenuItem>().Any(i => i.Tag is "opencontaining"), "A late Closed notification invalidated a newer menu request.");
        await CloseMenuAsync(menu); fixture.AppsPanel.Children.Remove(button);
        fixture.ResultsPanel.Visibility = Visibility.Visible;
        fixture.AppsPanel.Visibility = fixture.GroupsPanel.Visibility = Visibility.Collapsed;
        fixture.SearchResults.Items.Clear(); fixture.SearchResults.Items.Add(button); fixture.UpdateLayout();
        var row = fixture.SearchResults.ItemContainerGenerator.ContainerFromItem(button) as ListBoxItem;
        check(row != null && ReferenceEquals(row.ContextMenu, button.ContextMenu), "The focused search row does not expose its item's context menu for keyboard requests.");
        fixture.SearchResults.Items.Clear();
    }

    private static async Task CloseMenuAsync(ContextMenu menu)
    {
        if (!menu.IsOpen) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RoutedEventHandler closed = (_, _) => completion.TrySetResult();
        menu.Closed += closed;
        try
        {
            menu.IsOpen = false;
            if (await Task.WhenAny(completion.Task, Task.Delay(2000)) != completion.Task) throw new InvalidOperationException("The native menu did not finish closing.");
        }
        finally { menu.Closed -= closed; }
    }
}
