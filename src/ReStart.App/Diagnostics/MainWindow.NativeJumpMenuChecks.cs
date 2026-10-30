using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private async Task<int> VerifyNativeJumpMenusAsync(string directory, string snapshotPath)
    {
        var app = new AppEntry("native-menu-fixture!app", "Sample application", "native-menu-fixture!app");
        var fixture = new MainWindow(new LayoutStore(Path.Combine(directory, "menus.json")), true, () => Task.FromResult(new List<AppEntry> { app }), _ => { })
        { Owner = this, Width = 1000, Height = 700, Topmost = false };
        var language = L.Language; var checks = 0;
        void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); checks++; }
        static MenuItem Find(ItemsControl parent, string tag) => parent.Items.OfType<MenuItem>().Single(i => Equals(i.Tag, tag));
        var pinPath = Path.Combine(directory, "Pinned sample.txt"); File.WriteAllText(pinPath, "owned menu fixture");
        var historyPath = Path.Combine(directory, "Recent sample.txt"); File.WriteAllText(historyPath, "owned menu fixture");
        var state = new NativeJumpSnapshot(true, true, [new(new string('A', 64), "New private window", "", false, true)], [new(new string('B', 64), "Pinned sample", pinPath, false, false)],
            [new("Owned projects", [new(new string('D', 64), "Project sample", "", false, true)])]);
        var invoked = new List<(string App, string Operation, string Key, string Target)>();
        Task<NativeJumpReply> Respond(AppEntry selected, string operation, string key, string target, CancellationToken token)
        { invoked.Add((selected.Id, operation, key, target)); return Task.FromResult(new NativeJumpReply(true, "", app.Id, state)); }
        try
        {
            fixture.Present(); await fixture.InitializeAsync();
            fixture._shellCommandsFixture = (_, _) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(["opencontaining", "properties"]));
            fixture._appCommandFixture = (_, _) => Task.CompletedTask;
            fixture._jumpDestinationsFixture = _ => Task.FromResult<IReadOnlyList<JumpDestination>>([new("Pinned duplicate", pinPath, false), new("Recent sample", historyPath, false)]);
            fixture._removeJumpDestinationFixture = (_, _) => Task.CompletedTask;
            fixture._nativeJumpFixture = Respond;
            foreach (var tag in L.SupportedLanguages)
                foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
                {
                    fixture.Document.Preferences.Language = tag; fixture.Document.Preferences.Theme = theme; fixture.ApplyAppearance();
                    foreach (var search in new[] { false, true })
                    {
                        var button = fixture.AppButton(app, search); fixture.AppsPanel.Children.Add(button);
                        var menu = button.ContextMenu!; menu.PlacementTarget = button; menu.IsOpen = true; await Task.Delay(15);
                        var tasks = Find(menu, "jump-tasks"); var pinned = Find(menu, "jump-pinned");
                        Check(tasks.Visibility == Visibility.Visible && Equals(tasks.Header, L.T(K.JumpTasks)) && tasks.Items.Count == 1, "Registered tasks are absent or not localized.");
                        Check(pinned.Visibility == Visibility.Visible && Equals(pinned.Header, L.T(K.JumpPinned)) && pinned.Items.Count == 1, "Windows pins are absent or not localized.");
                        var custom = Find(menu, "jump-custom");
                        Check(custom.Header.Equals("Owned projects") && custom.Items.Count == 1, "An application-defined category lost its original label or destinations.");
                        var customRow = custom.Items.OfType<MenuItem>().Single();
                        customRow.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, customRow)); await Task.Delay(10);
                        Check(invoked.Last() == (app.Id, "invoke-custom", state.Categories![0].Items[0].Key, ""), "A custom destination lost its application/category identity.");
                        var recent = menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, L.T(K.JumpRecent)));
                        Check(recent.Items.Count == 1 && recent.Items.OfType<MenuItem>().Single().Tag?.Equals(historyPath) == true, "Pinned Windows destinations also appear in Recent.");
                        var task = tasks.Items.OfType<MenuItem>().Single();
                        task.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, task)); await Task.Delay(10);
                        Check(invoked.Last() == (app.Id, "invoke-task", state.Tasks[0].Key, ""), "A task routes through the wrong app, key or command line.");
                        var row = pinned.Items.OfType<MenuItem>().Single();
                        var actions = row.ContextMenu!;
                        Check(Find(actions, "jump-unpin").Header.Equals(L.T(K.UnpinFromJumpList)) && Find(actions, "properties").IsEnabled, "A pinned destination lacks localized removal or filesystem actions.");
                        Find(actions, "jump-open").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Task.Delay(10);
                        Check(invoked.Last() == (app.Id, "invoke-pinned", state.Pinned[0].Key, ""), "A pinned link lost its native identity on launch.");
                        Find(actions, "jump-unpin").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Task.Delay(10);
                        Check(invoked.Last() == (app.Id, "unpin", state.Pinned[0].Key, ""), "Unpin targeted the wrong app or destination.");
                        var recentActions = recent.Items.OfType<MenuItem>().Single().ContextMenu!;
                        var pin = Find(recentActions, "jump-pin"); Check(pin.Header.Equals(L.T(K.PinToJumpList)), "A Recent destination lacks its localized pin action.");
                        pin.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Task.Delay(10);
                        Check(invoked.Last() == (app.Id, "pin", "", historyPath), "Pinning used a menu label or another application's history.");
                        if (tag == "en" && !search)
                        {
                            if (!menu.IsOpen) { menu.IsOpen = true; await Task.Delay(10); }
                            SaveMenuSnapshot(menu, Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"native-menu-{theme}.png"));
                            row = pinned.Items.OfType<MenuItem>().Single(); actions = row.ContextMenu!;
                            pinned.IsSubmenuOpen = true; await Task.Delay(10);
                            actions.PlacementTarget = row; actions.IsOpen = true; await Task.Delay(10);
                            SaveMenuSnapshot(actions, Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"native-pin-{theme}.png")); await CloseMenuAsync(actions);
                        }
                        await CloseMenuAsync(menu); fixture.AppsPanel.Children.Remove(button);
                    }
                }
            fixture.Document.Preferences.Language = "en"; fixture.ApplyAppearance();
            var root = Tile.FromApp(app); var child = Tile.FromApp(app); var folder = new Tile { Label = "Owned tile folder", Children = [child] };
            foreach (var standalone in new[] { false, true })
            {
                fixture._openFolder = null; fixture.Document.Groups = [new() { IsStandalone = standalone, Tiles = [root, folder] }];
                fixture.RenderGroups(); fixture.UpdateLayout();
                foreach (var tile in new[] { root, child })
                {
                    if (tile == child) { fixture.OpenFolder(folder); fixture.UpdateLayout(); }
                    var button = VisualChildren<Button>(fixture.GroupsPanel).First(b => ReferenceEquals(b.Tag, tile));
                    var tileMenu = button.ContextMenu!; tileMenu.PlacementTarget = button; tileMenu.IsOpen = true; await Task.Delay(10);
                    Check(Find(tileMenu, "jump-tasks").Visibility == Visibility.Visible && Find(tileMenu, "jump-pinned").Visibility == Visibility.Visible,
                        "A grouped, standalone or expanded-folder pin lost its application's Tasks/Pinned sections.");
                    await CloseMenuAsync(tileMenu);
                }
            }
            var anchor = fixture.AppButton(app, true); fixture.AppsPanel.Children.Add(anchor); var current = anchor.ContextMenu!; current.PlacementTarget = anchor;
            foreach (var enabled in new[] { false, true })
            {
                fixture.Document.Preferences.ShowAppTasks = enabled; fixture.Document.Preferences.ShowPinnedJumpLists = enabled;
                fixture.Document.Preferences.ShowCustomJumpLists = enabled;
                current.IsOpen = true; await Task.Delay(10);
                Check((Find(current, "jump-tasks").Visibility == Visibility.Visible) == enabled && (Find(current, "jump-pinned").Visibility == Visibility.Visible) == enabled,
                    "A native list ignores its disabled setting.");
                Check(Find(current, "open").IsEnabled, "Native settings disabled the app's normal Open command."); await CloseMenuAsync(current);
                Check(current.Items.OfType<MenuItem>().Any(row => Equals(row.Tag, "jump-custom")) == enabled, "A custom category ignores its display setting.");
            }
            fixture.Document.Preferences.ShowJumpLists = false; current.IsOpen = true; await Task.Delay(10);
            Check(Find(current, "jump-tasks").Visibility == Visibility.Visible && Find(current, "jump-pinned").Visibility == Visibility.Visible
                && current.Items.OfType<MenuItem>().Single(i => Equals(i.Header, L.T(K.JumpRecent))).Visibility == Visibility.Collapsed,
                "Turning off history hides registered tasks or Windows pins."); await CloseMenuAsync(current);
            fixture.Document.Preferences.ShowJumpLists = true;
            var original = state;
            foreach (var unavailable in new[] { Path.Combine(directory, "Unavailable pin.txt"), "https://example.invalid/owned-fixture" })
            {
                state = state with { Pinned = [new(new string('C', 64), "Unavailable or virtual destination", unavailable, false, false)] };
                current.IsOpen = true; await Task.Delay(10);
                var actions = Find(current, "jump-pinned").Items.OfType<MenuItem>().Single().ContextMenu!;
                Check(actions.Items.OfType<MenuItem>().All(i => i.Tag is not ("properties" or "opencontaining"))
                    && actions.Items.OfType<MenuItem>().Any(i => i.Tag is "jump-unpin"),
                    "A missing or virtual pinned destination advertises filesystem commands or loses unpin.");
                await CloseMenuAsync(current);
            }
            state = original;
            foreach (var failure in new Exception[] { new IOException("Owned read failure."), new TimeoutException("Owned timeout.") })
            {
                fixture._nativeJumpFixture = (_, _, _, _, _) => Task.FromException<NativeJumpReply>(failure);
                current.IsOpen = true; await Task.Delay(10);
                Check(Find(current, "jump-tasks").Visibility == Visibility.Collapsed && Find(current, "jump-pinned").Visibility == Visibility.Collapsed
                    && current.Items.OfType<MenuItem>().Any(i => i.Tag is "opencontaining"), "A native read failure hides normal Shell commands or leaves stale tasks."); await CloseMenuAsync(current);
            }
            var old = new TaskCompletionSource<NativeJumpReply>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture._nativeJumpFixture = (_, _, _, _, _) => old.Task; current.IsOpen = true; await Task.Delay(10); await CloseMenuAsync(current);
            fixture._nativeJumpFixture = (_, _, _, _, _) => Task.FromResult(new NativeJumpReply(true, "", app.Id, NativeJumpSnapshot.Empty));
            current.IsOpen = true; await Task.Delay(10); old.SetResult(new(true, "", app.Id, state)); await Task.Delay(20);
            Check(Find(current, "jump-tasks").Visibility == Visibility.Collapsed, "An old native response replaced a reopened menu."); await CloseMenuAsync(current);
            fixture._nativeJumpFixture = Respond; fixture.Document.Preferences.LayoutLocked = true; current.IsOpen = true; await Task.Delay(10);
            Check(Find(current, "jump-tasks").IsEnabled && !Find(current, "pin").IsEnabled, "Layout locking blocks application tasks or leaves layout pinning enabled."); await CloseMenuAsync(current);
            return checks;
        }
        finally { fixture.Exit(); L.SetLanguage(language); Activate(); }
    }
}
