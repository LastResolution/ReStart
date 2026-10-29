using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private static async Task VerifyHistoryMenusAsync(MainWindow fixture, AppEntry app, AppEntry other, ReStart.Core.ThemeMode theme, string snapshotPath, Action<bool, string> check)
    {
        static MenuItem? Find(ItemsControl menu, string tag) => menu.Items.OfType<MenuItem>().FirstOrDefault(i => Equals(i.Tag, tag));
        var apps = fixture._apps; var activity = fixture._activity;
        var recentEnabled = fixture.Document.Preferences.ShowRecentlyAdded; var frequentEnabled = fixture.Document.Preferences.ShowMostUsed;
        try
        {
            fixture._apps = [app, other]; fixture._activity = new() { Initialized = true };
            fixture.Activity.Observe(fixture._apps, DateTimeOffset.Now);
            fixture.Activity.RecordLaunch(app.Id, true, DateTimeOffset.Now); fixture.Activity.RecordLaunch(other.Id, true, DateTimeOffset.Now);
            fixture.Document.Preferences.ShowRecentlyAdded = fixture.Document.Preferences.ShowMostUsed = true;
            fixture.RenderApps();
            Button Row(K heading, AppEntry selected) => fixture.AppsPanel.Children.OfType<Button>().Single(b => Equals(b.Tag, selected) && b.Uid == "history" && AutomationProperties.GetItemStatus(b) == L.T(heading));
            async Task<ContextMenu> OpenRowAsync(K heading, AppEntry selected)
            {
                var row = Row(heading, selected); var menu = row.ContextMenu!; menu.PlacementTarget = row; menu.IsOpen = true; await Task.Delay(10); return menu;
            }
            var menu = await OpenRowAsync(K.RecentlyAdded, app);
            var more = Find(menu, "shell-more")!;
            check(Find(more, "history-remove")?.Header?.Equals(L.T(K.RemoveFromThisList)) == true
                && Find(more, "history-clear")?.Header?.Equals(L.T(K.ClearRecentList)) == true, "Recently added lacks correctly placed removal/clear commands.");
            more.IsSubmenuOpen = true; await Task.Delay(10);
            if (L.Language == "en" && more.Template.FindName("PART_Popup", more) is Popup popup && popup.Child is FrameworkElement content)
                SaveMenuSnapshot(content, Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"menu-history-{theme}.png"));
            Find(more, "history-remove")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await CloseMenuAsync(menu);
            check(!fixture.Activity.Recent(fixture._apps, DateTimeOffset.Now).Contains(app)
                && fixture.Activity.Recent(fixture._apps, DateTimeOffset.Now).Contains(other)
                && fixture.Activity.Frequent(fixture._apps).Contains(app), "Recent removal changed another app or its usage history.");
            menu = await OpenRowAsync(K.RecentlyAdded, other); more = Find(menu, "shell-more")!;
            Find(more, "history-clear")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await CloseMenuAsync(menu);
            check(fixture.Activity.Recent(fixture._apps, DateTimeOffset.Now).Count == 0 && fixture.Activity.Frequent(fixture._apps).Count == 2, "Clearing Recently added erased usage data or left recent rows.");
            menu = await OpenRowAsync(K.MostUsed, app); more = Find(menu, "shell-more")!;
            check(Find(more, "history-remove")?.Header?.Equals(L.T(K.DontShowInThisList)) == true && Find(more, "history-clear") == null, "Most used uses the Recently added command set.");
            Find(more, "history-remove")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await CloseMenuAsync(menu);
            fixture.Activity.RecordLaunch(app.Id, true, DateTimeOffset.Now);
            check(!fixture.Activity.Frequent(fixture._apps).Contains(app) && fixture.Activity.Frequent(fixture._apps).Contains(other)
                && fixture.AppsPanel.Children.OfType<Button>().Any(b => Equals(b.Tag, app) && b.Uid != "history"), "Hiding a Most used app removed its alphabetical entry or was undone by launching.");
            fixture.RefreshNavigationShortcuts();
            K? selectedSection = null; fixture._menuSettingsFixture = section => selectedSection = section;
            foreach (var expanded in new[] { false, true })
            {
                fixture.SetRailOpen(expanded, explicitToggle: true);
                var folders = expanded ? fixture.ExpandedFolderShortcuts : fixture.CollapsedFolderShortcuts;
                foreach (var button in folders.Children.OfType<Button>().Append(expanded ? fixture.ExpandedSettingsNav : fixture.SettingsNav))
                {
                    menu = button.ContextMenu!; menu.PlacementTarget = button; menu.IsOpen = true; await Task.Delay(10);
                    var personalize = Find(menu, "personalize-navigation");
                    check(personalize?.Header?.Equals(L.T(K.PersonalizeThisList)) == true, "A collapsed/expanded navigation shortcut lacks Personalize this list.");
                    personalize!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await CloseMenuAsync(menu);
                    check(selectedSection == K.SettingsStartExperience, "Personalize this list opens the wrong settings section."); selectedSection = null;
                }
            }
            fixture.SetRailOpen(false);
            check(fixture.GroupMenu(new()).Items.OfType<MenuItem>().Any(i => Equals(i.Header, L.T(K.UnpinGroupFromStart))), "The group menu does not describe unpinning from Start.");
            var removed = new List<(string App, string Target)>();
            fixture._removeJumpDestinationFixture = (owner, target) => { removed.Add((owner.Id, target)); return Task.CompletedTask; };
            fixture._jumpDestinationsFixture = _ => Task.FromResult<IReadOnlyList<JumpDestination>>([new("History sample", other.LaunchTarget, false)]);
            var anchor = fixture.AppButton(app); fixture.AppsPanel.Children.Add(anchor);
            menu = anchor.ContextMenu!; menu.PlacementTarget = anchor; menu.IsOpen = true; await Task.Delay(10);
            var recent = menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, L.T(K.JumpRecent)));
            check(recent.Visibility == Visibility.Visible, "Recent destinations did not render.");
            recent.IsSubmenuOpen = true; await Task.Delay(10);
            var destination = recent.Items.OfType<MenuItem>().Single(); var actions = destination.ContextMenu!;
            actions.PlacementTarget = destination; actions.IsOpen = true; await Task.Delay(10);
            check(new[] { "jump-open", "opencontaining", "properties", "jump-remove" }.All(tag => Find(actions, tag) is { IsEnabled: true }), "A recent document's right-click actions are missing or disabled.");
            if (L.Language == "en") SaveMenuSnapshot(actions, Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"menu-destination-{theme}.png"));
            Find(actions, "jump-remove")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Task.Delay(10);
            check(removed.SequenceEqual([(app.Id, other.LaunchTarget)]) && recent.Items.Count == 0 && recent.Visibility == Visibility.Collapsed
                && Directory.Exists(other.LaunchTarget), "A destination removal targeted the wrong app, retained its row or deleted the directory.");
            await CloseMenuAsync(actions); await CloseMenuAsync(menu); fixture.AppsPanel.Children.Remove(anchor);
            fixture._jumpDestinationsFixture = _ => Task.FromResult<IReadOnlyList<JumpDestination>>([]);
        }
        finally
        {
            fixture._apps = apps; fixture._activity = activity;
            fixture.Document.Preferences.ShowRecentlyAdded = recentEnabled; fixture.Document.Preferences.ShowMostUsed = frequentEnabled;
            fixture._removeJumpDestinationFixture = null; fixture._menuSettingsFixture = null;
        }
    }
}
