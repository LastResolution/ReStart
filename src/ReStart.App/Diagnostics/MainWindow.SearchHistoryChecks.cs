using System.Windows;
using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private static int VerifyAppHistorySearch(MainWindow fixture, string directory)
    {
        var apps = fixture._apps; var activity = fixture._activity;
        var preferences = fixture.Document.Preferences;
        var mostUsed = preferences.ShowMostUsed; var recent = preferences.ShowRecentlyAdded;
        var personalize = preferences.PersonalizeAppSearch; var native = preferences.UseWindowsAppHistory;
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        try
        {
            fixture._activity = new();
            fixture._apps = Enumerable.Range(0, 6).Select(i => new AppEntry("history-" + i, "Owned Editor " + i, "")
            { ExcludeFromRecentlyAdded = i == 5, ExcludeFromMostUsed = i == 5 }).ToList();
            var now = DateTimeOffset.Now;
            fixture.Activity.Observe([fixture._apps[0]], now.AddMinutes(-5));
            fixture.Activity.Observe(fixture._apps, now.AddMinutes(-1));
            preferences.ShowRecentlyAdded = true; preferences.ShowMostUsed = false;
            fixture.RenderApps(); fixture.ShowAllApps(fixture, new()); fixture.UpdateLayout();
            Check(fixture.AppsPanel.Children.OfType<Button>().Count(b => b.Uid == "history") == 3, "Recently added did not start with three eligible rows.");
            var expand = fixture.AppsPanel.Children.OfType<Button>().Single(b => b.Uid == "recent-expand");
            Check(expand.IsVisible && fixture.AppsPanel.IsVisible, "Recently added expansion was not visible in All apps.");
            expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            fixture.UpdateLayout();
            Check(fixture.AppsPanel.Children.OfType<Button>().Count(b => b.Uid == "history") == 4 && fixture._recentAppsExpanded,
                "Recently added could not expand beyond three entries or included an excluded app.");
            var expandedToggle = fixture.AppsPanel.Children.OfType<Button>().Single(b => b.Uid == "recent-expand");
            Check(expandedToggle.IsKeyboardFocused, "Expanding Recently added lost keyboard focus.");
            fixture.RenderApps(); fixture.UpdateLayout();
            Check(fixture.AppsPanel.Children.OfType<Button>().Single(b => b.Uid == "recent-expand").IsKeyboardFocused,
                "Refreshing usage discarded the focused Recently added toggle.");
            SaveFixtureElement(fixture.Root, Path.Combine(directory, "recent-apps-expanded.png"));
            fixture.AppsPanel.Children.OfType<Button>().Single(b => b.Uid == "recent-expand").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(fixture.AppsPanel.Children.OfType<Button>().Count(b => b.Uid == "history") == 3 && !fixture._recentAppsExpanded,
                "Recently added did not collapse back to three rows.");
            preferences.ShowRecentlyAdded = false; fixture.RenderApps();
            Check(!fixture.AppsPanel.Children.OfType<Button>().Any(b => b.Uid is "history" or "recent-expand"), "Disabling recent apps retained its rows or expand action.");
            preferences.ShowMostUsed = true; preferences.UseWindowsAppHistory = false; preferences.PersonalizeAppSearch = true;
            fixture.Activity.RecordLaunch("history-4", true, now);
            fixture.SetSmokeSearch("Owned Editor");
            Check(((Button)fixture.SearchResults.Items[0]).Tag is AppEntry { Id: "history-4" }, "Opted-in usage did not rank equally strong app matches.");
            fixture.Activity.RecordLaunch("history-0", true, now.AddSeconds(1)); fixture.RefreshSearchResults();
            Check(((Button)fixture.SearchResults.Items[0]).Tag is AppEntry { Id: "history-0" }
                && ((Button)fixture.SearchResults.SelectedItem).Tag is AppEntry { Id: "history-4" }, "Usage refresh changed the selected Enter target.");
            fixture.Activity.RecordLaunch("history-4", true, now.AddSeconds(2)); fixture.RefreshSearchResults();
            Check(((Button)fixture.SearchResults.Items[0]).Tag is AppEntry { Id: "history-4" }, "Later launch counts did not update personalized ranking.");
            preferences.PersonalizeAppSearch = false; fixture.RefreshSearchResults();
            Check(((Button)fixture.SearchResults.Items[0]).Tag is AppEntry { Id: "history-0" }, "Disabling personalization did not restore name ordering.");
            preferences.PersonalizeAppSearch = true; preferences.ShowMostUsed = false; fixture.RefreshSearchResults();
            Check(((Button)fixture.SearchResults.Items[0]).Tag is AppEntry { Id: "history-0" }, "Disabled history still personalized app results.");
        }
        finally
        {
            fixture.ResetToPinnedView(); fixture._apps = apps; fixture._activity = activity; fixture._recentAppsExpanded = false;
            preferences.ShowMostUsed = mostUsed; preferences.ShowRecentlyAdded = recent;
            preferences.PersonalizeAppSearch = personalize; preferences.UseWindowsAppHistory = native;
            fixture.RenderApps();
        }
        return checks;
    }
}
