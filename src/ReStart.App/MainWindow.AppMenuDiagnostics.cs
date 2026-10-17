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
            return new(3, 2, 2);
        }
        finally { fixture.Exit(); Activate(); }
    }
}

internal sealed record AppMenuSmokeResult(int PinChecks, int AvailabilityChecks, int ShellReadChecks);
