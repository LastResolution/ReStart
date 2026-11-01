using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private static async Task<int> VerifySearchActionKeysAsync(MainWindow fixture)
    {
        var apps = fixture._apps;
        var p = fixture.Document.Preferences;
        var previous = (p.SearchSettings, p.SearchFiles, p.ShowWebSearch, p.ShowWindowsSearchHandoff);
        var web = fixture._webSearchFixture; var windows = fixture._windowsSearchFixture;
        var fileFixture = fixture._fileSearchFixture;
        var checks = 0; var webCalls = 0; var windowsCalls = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        string? Selected() => SearchResultKey(fixture.SearchResults.SelectedItem);
        void Press(Key key)
        {
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(fixture)!, Environment.TickCount, key)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            ((UIElement)Keyboard.FocusedElement).RaiseEvent(args);
            if (!args.Handled) { args.RoutedEvent = Keyboard.KeyDownEvent; ((UIElement)Keyboard.FocusedElement).RaiseEvent(args); }
            fixture.UpdateLayout();
        }
        try
        {
            p.SearchFiles = false; p.SearchSettings = false; p.ShowWebSearch = true; p.ShowWindowsSearchHandoff = true;
            fixture._apps = [new("action-local", "Owned action sample", "")];
            fixture._webSearchFixture = _ => { webCalls++; return true; };
            fixture._windowsSearchFixture = _ => { windowsCalls++; return true; };
            foreach (var language in TextCatalog.SupportedLanguages)
            {
                LocalizationResources.Apply(language);
                fixture.SetSmokeSearch("Owned action"); fixture.RefreshSearchResults(); fixture.UpdateLayout();
                Check(Selected() == "action-local" && fixture.SearchResults.Items.Count == 3, "Online actions displaced the local best match or were omitted.");
                Press(Key.Down);
                Check(Selected() == "restart:search:web" && fixture.SearchResults.IsKeyboardFocusWithin, "Down could not reach web search after the final local match.");
                Press(Key.Down);
                Check(Selected() == "restart:search:windows", "Down could not reach Windows/cloud search.");
                Press(Key.Down); Check(Selected() == "restart:search:windows", "Down escaped the last search action.");
                var row = (ListBoxItem)fixture.SearchResults.ItemContainerGenerator.ContainerFromItem(fixture.WindowsSearchButton);
                Check(row.IsKeyboardFocused, $"The Windows action lost native row focus ({language}; {Keyboard.FocusedElement?.GetType().Name}).");
                Check(AutomationProperties.GetName(row) == L.T(K.SearchInWindows), $"The Windows action lost its translated accessible name ({language}; actual={AutomationProperties.GetName(row)}; expected={L.T(K.SearchInWindows)}).");
                var beforeWindows = windowsCalls;
                Press(Key.Enter); Check(windowsCalls == beforeWindows + 1 && webCalls == 0, "Enter on the Windows action invoked a different route.");
                Press(Key.Up); Check(Selected() == "restart:search:web", "Up could not return from Windows search to web search.");
                fixture.RefreshSearchResults(); fixture.UpdateLayout();
                Check(Selected() == "restart:search:web", "A same-query refresh discarded an explicitly selected online action.");
                Press(Key.Up); Check(Selected() == "action-local", "Up could not return to local matches.");
                fixture.FocusSearchInput(); Press(Key.Tab);
                Check(Selected() == "action-local" && fixture.SearchResults.IsKeyboardFocusWithin, "Tab did not enter the visible selected result.");
                Press(Key.Tab); Check(Selected() == "restart:search:web", "Tab skipped the web action.");
                Press(Key.Tab); Check(Selected() == "restart:search:windows", "Tab skipped the Windows action.");
                Check(!fixture.MoveSearchTab(false), "Tab was trapped after the final result.");
                Check(fixture.MoveSearchTab(true) && Selected() == "restart:search:web", "Shift+Tab could not return to web search.");
                Check(fixture.MoveSearchTab(true) && Selected() == "action-local", "Shift+Tab could not return to local results.");
                Check(fixture.MoveSearchTab(true) && fixture.SearchBox.IsKeyboardFocused, "Shift+Tab from the first row did not return to the query.");
            }
            LocalizationResources.Apply("en");
            fixture.SetSmokeSearch("No owned match"); fixture.UpdateLayout();
            Check(Selected() == "restart:search:web" && webCalls == 0, "No-match fallback searched automatically or lacked a visible selected web action.");
            Press(Key.Enter); Check(webCalls == 1, "Enter did not invoke the visible no-match web action exactly once.");
            p.ShowWebSearch = false; fixture.RefreshSearchResults();
            Check(Selected() == "restart:search:windows" && !fixture.SearchResults.Items.Contains(fixture.WebSearchButton), "Disabling web search left its row or broke the Windows fallback.");
            p.ShowWindowsSearchHandoff = false; fixture.RefreshSearchResults(); fixture.FocusSearchInput();
            var before = windowsCalls; Press(Key.Enter);
            Check(fixture.SearchResults.Items.Count == 0 && windowsCalls == before && webCalls == 1, "Disabled online actions remained reachable by Enter.");

            p.ShowWebSearch = true; p.ShowWindowsSearchHandoff = true;
            fixture.SetSmokeSearch("Owned late result"); fixture.UpdateLayout();
            Check(Selected() == "restart:search:web", "The absent local result did not offer a passive online fallback.");
            fixture._apps = [new("late-action-local", "Owned late result", "")]; fixture.RefreshSearchResults();
            Check(Selected() == "late-action-local", "An untouched online fallback displaced a late-arriving local match.");
            fixture._apps = []; fixture.SetSmokeSearch("Explicit late result"); fixture.UpdateLayout(); Press(Key.Tab);
            fixture._apps = [new("explicit-local", "Explicit late result", "")]; fixture.RefreshSearchResults();
            Check(Selected() == "restart:search:web", "A late local match replaced the action explicitly selected by the user.");

            p.SearchFiles = true;
            var completion = new TaskCompletionSource<(IReadOnlyList<AppEntry>, bool)>();
            fixture._fileSearchFixture = (_, _) => completion.Task;
            fixture._apps = []; fixture.SetSmokeSearch("Pending owned file"); fixture.UpdateLayout();
            Check(fixture.SearchResults.SelectedItem == null, "An unfinished local file query selected an automatic external fallback.");
            completion.SetResult(([], true));
            var deadline = Environment.TickCount64 + 2000;
            while (fixture._fileSearchPending && Environment.TickCount64 < deadline) await Task.Delay(20);
            Check(Selected() == "restart:search:web", "A completed empty file query did not select the web fallback.");
        }
        finally
        {
            fixture.ResetToPinnedView(); fixture._apps = apps;
            (p.SearchSettings, p.SearchFiles, p.ShowWebSearch, p.ShowWindowsSearchHandoff) = previous;
            fixture._webSearchFixture = web; fixture._windowsSearchFixture = windows; fixture._fileSearchFixture = fileFixture;
            LocalizationResources.Apply("en");
        }
        return checks;
    }
}
