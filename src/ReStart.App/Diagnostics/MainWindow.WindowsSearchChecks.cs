using System.Windows;
using System.Windows.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private static async Task<int> VerifyWindowsSearchAsync(MainWindow fixture, List<AppEntry> launched, string directory)
    {
        var checks = 0; var loads = 0; var handoffs = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        static bool TextRenders(Button button) => VisualChildren<TextBlock>(button).Any(t => t.Text == System.Windows.Automation.AutomationProperties.GetName(button)
            && t.ActualWidth > 10 && t.ActualHeight > 10);
        fixture.Document.Preferences.SearchSettings = true;
        var webSearches = new List<Uri>();
        fixture._webSearchFixture = uri => { webSearches.Add(uri); return true; };
        fixture._windowsSearchFixture = () => { handoffs++; return true; };
        fixture._windowsSettingsFixture = async () =>
        {
            loads++; await Task.Delay(35);
            return new WindowsSettingsCatalog.Entry[] { new(new("restart:setting:native:owned", "Owned Windows setting", "ms-settings:display", false), "owned keyword") };
        };
        var beforeLaunch = launched.Count;
        fixture.SetSmokeSearch("owned keyword");
        Check(launched.Count == beforeLaunch && handoffs == 0, "Typing automatically invoked Windows Search.");
        Check(webSearches.Count == 0 && fixture.WebSearchButton.Visibility == Visibility.Visible,
            "Typing invoked the browser or failed to offer the explicit web action.");
        fixture.WebSearchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(webSearches.Count == 1 && webSearches[0] == WebSearchQuery.Build("owned keyword"), "Web search lost or altered the current query.");
        fixture.SetSmokeSearch("owned & updated?value=1");
        fixture.WebSearchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(webSearches.Count == 2 && webSearches[1] == WebSearchQuery.Build(fixture.SearchBox.Text), "Web search reused a stale query or accepted query injection.");
        fixture.Document.Preferences.ShowWebSearch = false; fixture.RefreshSearchResults();
        fixture.WebSearchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(webSearches.Count == 2 && fixture.WebSearchButton.Visibility == Visibility.Collapsed, "A disabled web action remained invokable.");
        fixture.Document.Preferences.ShowWebSearch = true; fixture.SetSmokeSearch("owned keyword");
        var deadline = Environment.TickCount64 + 2000;
        while (!fixture._windowsSettingsLoaded && Environment.TickCount64 < deadline) await Task.Delay(20);
        Check(fixture.SearchResultNames.Contains("Owned Windows setting"), "Native settings keywords did not appear in categorized results.");
        Check(((Button)fixture.SearchResults.SelectedItem).Tag is AppEntry { Id: "restart:setting:native:owned" }, "Late native settings lost the visible result selection.");
        fixture.WindowsSearchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(handoffs == 1 && launched.Count == beforeLaunch, "The explicit Windows Search action did not use the native shortcut route.");
        fixture.Document.Preferences.ShowWindowsSearchHandoff = false; fixture.RefreshSearchResults();
        var afterLaunch = launched.Count; fixture.WindowsSearchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(fixture.WindowsSearchButton.Visibility == Visibility.Collapsed && launched.Count == afterLaunch && handoffs == 1, "A disabled handoff remained visible or invokable.");
        fixture.Document.Preferences.SearchSettings = false; fixture.RefreshSearchResults();
        Check(!fixture.SearchResultNames.Contains("Owned Windows setting"), "Native settings ignored the settings search switch.");
        fixture.Document.Preferences.SearchSettings = true; fixture.Document.Preferences.ShowWindowsSearchHandoff = true;
        var nativeParser = WindowsSearchQuery.Build("System.Kind:picture", false, 1033) != null;
        foreach (var lang in TextCatalog.SupportedLanguages)
        {
            L.SetLanguage(lang);
            var sql = WindowsSearchQuery.Build("System.Kind:picture", false, L.Culture.LCID);
            Check(nativeParser ? sql != null && WindowsSearchQuery.IsSafeProjection(sql, false) : sql == null || WindowsSearchQuery.IsSafeProjection(sql, false),
                "The native parser failed its selected language or generated an unsafe metadata projection.");
            Check(WindowsSearchQuery.Build("System.Search.Contents:ReStartOwnedAbsent", false, L.Culture.LCID) == null,
                "Explicit AQS properties bypassed disabled document-content matching.");
            Check(!string.IsNullOrWhiteSpace(L.T(K.SearchInWindows)) && !string.IsNullOrWhiteSpace(L.T(K.SettingsWindowsSearchSyntax)), "A Windows Search action lost its translated text.");
            fixture.RefreshSearchResults();
            Check(System.Windows.Automation.AutomationProperties.GetName(fixture.WebSearchButton).Contains("owned keyword", StringComparison.Ordinal)
                && fixture.WebSearchButton.ToolTip is string { Length: > 0 }, "A translated web action lost its query or privacy description.");
        }
        fixture.Document.Preferences.Language = "en"; LocalizationResources.Apply("en");
        foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
        {
            fixture.Document.Preferences.Theme = theme; fixture.ApplyAppearance(); fixture.RefreshSearchResults(); fixture.UpdateLayout();
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
            Check(fixture.WebSearchButton.ActualWidth > 30 && fixture.WebSearchButton.ActualHeight > 10
                && TextRenders(fixture.WebSearchButton), "The web action was visible in state but had no rendered text extent.");
            foreach (var action in new[] { fixture.WebSearchButton, fixture.WindowsSearchButton })
            {
                var row = (ListBoxItem)fixture.SearchResults.ItemContainerGenerator.ContainerFromItem(action);
                var text = VisualChildren<TextBlock>(action).Single(t => t.Text == System.Windows.Automation.AutomationProperties.GetName(action));
                Check(text.Foreground is System.Windows.Media.SolidColorBrush ink && row.Foreground is System.Windows.Media.SolidColorBrush expected && ink.Color == expected.Color,
                    "An online action did not inherit its native row's theme/selection text color.");
            }
            SaveFixtureElement(fixture.Root, Path.Combine(directory, "windows-search-" + theme + ".png"));
        }
        Check(loads == 1, "Repeated searches re-enumerated the native settings catalog.");
        var width = fixture.Width;
        foreach (var viewport in new[] { 360d, 768d })
        {
            fixture.Width = viewport;
            fixture.SetSmokeSearch(new string('x', 500)); fixture.UpdateLayout();
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
            Check(fixture.WebSearchButton.ActualWidth > 30 && fixture.WebSearchButton.ActualWidth <= fixture.ResultsPanel.ActualWidth + 1
                && TextRenders(fixture.WebSearchButton),
                "A long web query escaped the narrow search panel or lost its rendered text extent.");
        }
        fixture.Width = width;
        fixture.ResetToPinnedView();
        Check(fixture.WebSearchButton.Visibility == Visibility.Collapsed && fixture.WindowsSearchButton.Visibility == Visibility.Collapsed,
            "Returning home retained online-search actions.");
        checks += VerifyAppHistorySearch(fixture, directory);
        checks += await AppJumpLists.VerifyOwnedAppFlagsAsync(directory);
        checks += await VerifySearchActionKeysAsync(fixture);
        fixture._windowsSettingsFixture = null; fixture._windowsSearchFixture = null; fixture._webSearchFixture = null; fixture.ResetToPinnedView();
        return checks;
    }
}
