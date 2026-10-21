using System.Windows;
using System.Windows.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Verifies app-level menu state in an isolated document and reads shell capabilities without executing a command.</summary>
    internal async Task<AppMenuSmokeResult> VerifyAppMenusAsync(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("App-menu verification requires preview mode.");
        var directory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "fixture-" + Guid.NewGuid().ToString("N"));
        var app = new AppEntry("fixture-menu", "Menu sample", "", false);
        var fixture = new MainWindow(new LayoutStore(Path.Combine(directory, "layout.json")), true, () => Task.FromResult(new List<AppEntry> { app }))
        { Owner = this, Width = 1000, Height = 700, Topmost = false };
        try
        {
            fixture.Present();
            await fixture.InitializeAsync();
            fixture.Document.Groups.Clear();
            var menu = fixture.CreateAppMenu(app);
            var pin = menu.Items.OfType<MenuItem>().First();
            Require((string)pin.Header == L.T(K.PinToStart), "An unpinned app does not offer Pin to Start.");
            pin.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Require(AppPinning.IsPinned(fixture.Document, app) && fixture.Document.Groups.Count == 1, "Pin to Start did not create one app pin.");
            Require(ReferenceEquals(fixture.TileForAppDrag(app.Id), AppPinning.Find(fixture.Document, app)) && fixture.Document.Groups.Count == 1,
                "Dragging an already pinned app creates a duplicate instead of moving its existing tile.");
            menu = fixture.CreateAppMenu(app);
            pin = menu.Items.OfType<MenuItem>().First();
            Require((string)pin.Header == L.T(K.UnpinTile), "A pinned app still offers duplicate pinning.");
            pin.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Require(!AppPinning.IsPinned(fixture.Document, app) && fixture.Document.Groups.Count == 0, "Unpin did not remove the pin and newly empty group.");

            var group = new TileGroup { Name = "Before" };
            fixture.Document.Groups.Add(group);
            menu = fixture.CreateAppMenu(app);
            group.Name = "After";
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            var extras = menu.Items.OfType<MenuItem>().Single(i => i.Header is "ReStart");
            var groups = extras.Items.OfType<MenuItem>().First();
            Require(groups.Items.OfType<MenuItem>().Single().Header is "After", "The app menu retained an obsolete group name.");
            var temporary = AppPinning.Pin(fixture.Document, app, group);
            fixture.RemoveTile(temporary.Id);
            Require(!fixture.Document.Groups.Contains(group), "Unpinning a group's last tile leaves an empty group behind.");
            var more = menu.Items.OfType<MenuItem>().Single(i => (string)i.Header == L.T(K.MenuMore));
            var uninstall = menu.Items.OfType<MenuItem>().Single(i => (string)i.Header == L.T(K.UninstallApp));
            fixture.PopulateAppShellItems(more, uninstall, app, new HashSet<string>(["runas", "opencontaining", "uninstall"], StringComparer.OrdinalIgnoreCase));
            Require(more.Items.Count == 2 && more.Items.OfType<MenuItem>().All(i => !i.IsEnabled)
                && uninstall.Visibility == Visibility.Visible && !uninstall.IsEnabled, "Preview shell actions were not safely represented as disabled.");
            fixture.PopulateAppShellItems(more, uninstall, app, new HashSet<string>());
            Require(more.Visibility == Visibility.Collapsed && uninstall.Visibility == Visibility.Collapsed,
                "Unsupported shell commands remain visible.");

            var commands = await AppShellCommands.ReadAsync(new("cmd-fixture", "System command fixture", Path.Combine(Environment.SystemDirectory, "cmd.exe"), false));
            Require(commands.Contains("runas") && commands.Contains("properties"), "The canonical command reader did not find standard executable verbs.");
            Require((await AppShellCommands.ReadAsync(new("missing", "Missing fixture", Path.Combine(directory, "missing.exe"), false))).Count == 0,
                "An unavailable app invented shell capabilities.");
            var folderPath = Path.Combine(directory, "Directory pin sample"); Directory.CreateDirectory(folderPath);
            var folderApp = FilePinning.FromPath(folderPath);
            foreach (var explicitGroup in new[] { false, true })
            {
                fixture._catalog.Icons.TryRemove(folderApp.Id, out _);
                var destination = new TileGroup { Name = "Explicit destination", Tiles = [new()] };
                fixture.Document.Groups = [destination]; fixture.RenderGroups(); fixture.UpdateLayout();
                menu = fixture.CreateAppMenu(folderApp);
                var action = explicitGroup
                    ? menu.Items.OfType<MenuItem>().Single(i => i.Header is "ReStart").Items.OfType<MenuItem>().First().Items.OfType<MenuItem>().Single()
                    : menu.Items.OfType<MenuItem>().First();
                action.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                var tile = AppPinning.Find(fixture.Document, folderApp) ?? throw new InvalidOperationException("The directory pin is missing.");
                bool IconRendered() => fixture._catalog.Icons.TryGetValue(folderApp.Id, out var source) &&
                    VisualChildren<Button>(fixture.GroupsPanel).FirstOrDefault(b => b.Tag == tile) is { } rendered &&
                    VisualChildren<Image>(rendered).SingleOrDefault()?.Source == source;
                // The STA cache can be populated before its UI continuation
                // rebuilds the button. Wait for the rendered result itself.
                for (var attempt = 0; attempt < 100 && !IconRendered(); attempt++) await Task.Delay(50);
                fixture.UpdateLayout();
                Require(tile.Target == folderPath && !tile.IsFolder, "The app menu pinned folder children instead of the directory.");
                var owner = fixture.Document.Groups.Single(g => g.Tiles.Contains(tile));
                Require(explicitGroup ? owner == destination : owner.IsStandalone, "A directory pin did not respect the selected destination.");
                Require(IconRendered(),
                    "Pinning a directory from its app menu leaves the fallback glyph until restart.");
                fixture.SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"app-menu-directory-{explicitGroup}.png"));
            }
            return new(7, 2, 2);
        }
        finally { fixture.Exit(); Activate(); }
    }
}

internal sealed record AppMenuSmokeResult(int PinChecks, int AvailabilityChecks, int ShellReadChecks);
