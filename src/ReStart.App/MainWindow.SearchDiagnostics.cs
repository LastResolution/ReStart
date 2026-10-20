using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Checks the selected Enter action, native row focus and typing without launching external applications.</summary>
    internal async Task<SearchSelectionSmokeResult> VerifySearchSelectionAsync(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("Search selection verification requires preview mode.");
        var directory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "search-" + Guid.NewGuid().ToString("N"));
        var apps = new List<AppEntry> { new("alpha", "Alpha Editor", ""), new("beta", "Beta Editor", ""), new("gamma", "Gamma Editor", "") };
        var failCatalog = false;
        var launched = new List<string>();
        var fixture = new MainWindow(new LayoutStore(Path.Combine(directory, "layout.json")), true, () => failCatalog ? Task.FromException<List<AppEntry>>(new IOException("Sample failure")) : Task.FromResult(apps), app => launched.Add(app.Id))
        { Owner = this, Width = 1280, Height = 800, Topmost = false, ShowInTaskbar = false };
        var language = L.Language; var checks = 0; var themes = 0;
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        string? Selected() => ((fixture.SearchResults.SelectedItem as Button)?.Tag as AppEntry)?.Id;
        ListBoxItem Row() => (ListBoxItem)fixture.SearchResults.ItemContainerGenerator.ContainerFromItem(fixture.SearchResults.SelectedItem);
        void Key(Key key)
        {
            var input = (UIElement)Keyboard.FocusedElement;
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(fixture)!, Environment.TickCount, key)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            input.RaiseEvent(args);
            if (!args.Handled) { args.RoutedEvent = Keyboard.KeyDownEvent; input.RaiseEvent(args); }
            fixture.UpdateLayout();
        }
        try
        {
            fixture.Present(); await fixture.InitializeAsync(); fixture._clock.Stop();
            fixture.SetSmokeSearch("Editor"); fixture.UpdateLayout();
            Check(Selected() == "alpha" && fixture.SearchBox.IsKeyboardFocused && Row().IsSelected,
                "Typing did not visibly select the first result while retaining the text caret.");
            var selection = (ISelectionProvider)UIElementAutomationPeer.CreatePeerForElement(fixture.SearchResults).GetPattern(PatternInterface.Selection);
            Check(selection.GetSelection().Length == 1 && AutomationProperties.GetName(Row()) == "Alpha Editor",
                "The default result was not exposed as a named native selection.");
            Key(System.Windows.Input.Key.Enter);
            Check(launched.SequenceEqual(new[] { "alpha" }), "Enter did not invoke exactly the visible default result.");
            Key(System.Windows.Input.Key.Down);
            Check(Row().IsKeyboardFocused && Selected() == "beta", "One Down press did not advance from the default result to the second result.");
            Key(System.Windows.Input.Key.Down);
            Check(Selected() == "gamma" && Row().IsKeyboardFocused, "The second Down press did not advance to the third result.");
            Key(System.Windows.Input.Key.Down);
            Check(Selected() == "gamma", "Down beyond the last result wrapped or cleared selection.");
            Key(System.Windows.Input.Key.Up);
            Check(Selected() == "beta" && Row().IsKeyboardFocused, "Up did not move selection and focus back together.");
            Key(System.Windows.Input.Key.Enter);
            Check(launched.SequenceEqual(new[] { "alpha", "beta" }), "Enter ignored the keyboard-selected result.");
            fixture._apps = apps.Select(a => a.Id == "beta" ? a with { Name = "Zulu Editor" } : a).ToList();
            fixture.RefreshSearchResults(); fixture.UpdateLayout();
            Check(Selected() == "beta" && fixture.SearchResults.SelectedIndex == 2 && Row().IsKeyboardFocused,
                "Refreshing results lost the selected app identity or stole row focus.");
            fixture.FocusSearchInput(); Key(System.Windows.Input.Key.Enter);
            Check(launched.Last() == "beta" && launched.Count == 3, "Returning to the search field silently changed the Enter target.");
            Key(System.Windows.Input.Key.Down);
            Check(Selected() == "beta" && Row().IsKeyboardFocused, "Down from the input did not clamp a preserved last selection.");
            fixture.FocusSearchInput();
            fixture.SearchBox.CaretIndex = fixture.SearchBox.Text.Length;
            fixture.ComposeFixtureText("x");
            Check(fixture.SearchBox.Text == "Editorx" && fixture.SearchResults.SelectedItem == null, "Continued typing retained an obsolete selection.");
            Key(System.Windows.Input.Key.Enter);
            Check(launched.Count == 3, "An empty result list launched a stale result.");
            Key(System.Windows.Input.Key.Down);
            Check(fixture.SearchBox.IsKeyboardFocused && fixture.SearchResults.SelectedItem == null, "Down into an empty result list stole focus or selected a stale app.");
            fixture.SetSmokeSearch("Beta"); fixture.UpdateLayout();
            Check(fixture.SearchResults.SelectedItem == null, "A changed catalog matched an obsolete display name.");
            fixture.SetSmokeSearch("Editor"); Key(System.Windows.Input.Key.Down); Key(System.Windows.Input.Key.Back);
            Check(fixture.SearchBox.Text == "Edito" && fixture.SearchBox.IsKeyboardFocused && Selected() == "alpha",
                "Backspace from a result did not resume query editing and select the new best result.");
            fixture.SetSmokeSearch("Editor"); Key(System.Windows.Input.Key.Down); Key(System.Windows.Input.Key.Up); Key(System.Windows.Input.Key.Up);
            Check(fixture.SearchBox.IsKeyboardFocused && Selected() == "alpha", "Up from the first result did not return to the input.");
            Key(System.Windows.Input.Key.Down); fixture.ComposeFixtureText("x");
            Check(fixture.SearchBox.Text == "Editorx" && fixture.SearchBox.IsKeyboardFocused && fixture.SearchResults.SelectedItem == null,
                "Typing from a result row did not continue the query.");
            fixture.SetSmokeSearch("Editor"); fixture.UpdateLayout();
            var last = fixture.SearchResults.Items.OfType<Button>().Last();
            last.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right) { RoutedEvent = Mouse.PreviewMouseDownEvent });
            Check(Selected() == "beta" && last.ContextMenu != null && launched.Count == 3, "Right-click selection launched an app or lost its context menu.");
            fixture.RefreshSearchResults(); fixture.UpdateLayout();
            Check(Selected() == "beta", "A same-query icon refresh reset the selected result.");
            fixture.SearchBox.Clear();
            Check(fixture.SearchResults.SelectedItem == null && !fixture.ResultsPanel.IsVisible, "Clearing search retained a hidden launch target.");
            fixture.SetSmokeSearch("Gamma"); Key(System.Windows.Input.Key.Down);
            Check(Selected() == "gamma" && Row().IsKeyboardFocused, "Down with one result lost the only selection.");
            Key(System.Windows.Input.Key.Up);
            Check(fixture.SearchBox.IsKeyboardFocused && Selected() == "gamma", "Up from the only result did not return to query editing.");
            foreach (var tag in L.SupportedLanguages)
                foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
                {
                    fixture.Document.Preferences.Language = tag; fixture.Document.Preferences.Theme = theme;
                    fixture.ApplyAppearance(); fixture.SetSmokeSearch("Editor"); fixture.UpdateLayout();
                    Check(Row().IsSelected && Row().ActualWidth > 300 && fixture.SearchBox.IsKeyboardFocused,
                        "The default selection or input focus was missing in a language/theme case.");
                    if (tag == "en") fixture.SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"search-selected-{theme}.png"));
                    themes++;
                }
            fixture.Hide(); fixture.Present();
            Check(fixture.SearchResults.SelectedItem == null && fixture.SearchBox.Text.Length == 0, "Reopening retained search selection.");
            fixture.Document.Groups = [new() { Name = "User pins", Tiles = [Tile.FromApp(apps[0])] }];
            var pins = System.Text.Json.JsonSerializer.Serialize(fixture.Document.Groups);
            fixture.SetSmokeSearch("Editor");
            fixture.SearchResults.SelectedItem = fixture.SearchResults.Items.OfType<Button>().Single(b => b.Tag is AppEntry a && a.Id == "gamma");
            apps = [apps[2], new("delta", "Delta Editor", "")];
            var refreshing = fixture.RefreshCatalogAsync();
            Check(ReferenceEquals(refreshing, fixture.RefreshCatalogAsync()), "Repeated catalog requests started concurrent enumerations.");
            await refreshing; fixture.UpdateLayout();
            Check(!fixture.SearchResultNames.Contains("Alpha Editor") && !fixture.SearchResultNames.Contains("Zulu Editor") && fixture.SearchResultNames.Contains("Delta Editor"),
                "A catalog refresh retained uninstalled apps or omitted a newly installed app from search.");
            Check(fixture.AppsPanel.Children.OfType<Button>().Where(b => b.Tag is AppEntry).Select(b => ((AppEntry)b.Tag).Id).ToHashSet().SetEquals(new[] { "gamma", "delta" }),
                "All-apps rows did not use the refreshed catalog.");
            Check(Selected() == "gamma" && fixture.SearchBox.IsKeyboardFocused && pins == System.Text.Json.JsonSerializer.Serialize(fixture.Document.Groups),
                "A catalog refresh changed user pins or lost the selected installed app/input focus.");
            failCatalog = true; await fixture.RefreshCatalogAsync();
            Check(fixture._catalogRefresh == null && fixture.SearchResultNames.Contains("Delta Editor"), "A failed enumeration discarded the last successful catalog or blocked future refreshes.");
            return new(checks, themes);
        }
        finally { fixture.Exit(); L.SetLanguage(language); ApplyAppearance(); Activate(); }
    }
}

internal sealed record SearchSelectionSmokeResult(int Checks, int LanguageThemeCases);
