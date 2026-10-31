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
        fixture.Document.Preferences.SearchSettings = true;
        fixture._windowsSearchFixture = () => { handoffs++; return true; };
        fixture._windowsSettingsFixture = async () =>
        {
            loads++; await Task.Delay(35);
            return new WindowsSettingsCatalog.Entry[] { new(new("restart:setting:native:owned", "Owned Windows setting", "ms-settings:display", false), "owned keyword") };
        };
        var beforeLaunch = launched.Count;
        fixture.SetSmokeSearch("owned keyword");
        Check(launched.Count == beforeLaunch && handoffs == 0, "Typing automatically invoked Windows Search.");
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
        }
        fixture.Document.Preferences.Language = "en"; LocalizationResources.Apply("en");
        foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
        {
            fixture.Document.Preferences.Theme = theme; fixture.ApplyAppearance(); fixture.RefreshSearchResults(); fixture.UpdateLayout();
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
            SaveFixtureElement(fixture.Root, Path.Combine(directory, "windows-search-" + theme + ".png"));
        }
        Check(loads == 1, "Repeated searches re-enumerated the native settings catalog.");
        fixture._windowsSettingsFixture = null; fixture._windowsSearchFixture = null; fixture.ResetToPinnedView();
        return checks;
    }
}
