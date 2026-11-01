using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ReStart.Core;
using ReStart.App.Services;

namespace ReStart.App;

public partial class MainWindow
{
    private string _renderedSearchQuery = "";
    private bool _searchSelectionExplicit;
    private int _searchRefreshDepth;
    private Button WebSearchButton = null!;
    private Button WindowsSearchButton = null!;
    private string _fileSearchQuery = "";
    private CancellationTokenSource? _fileSearchCancellation;
    private readonly IndexedFileSearch _indexedFiles = new();
    private IReadOnlyList<AppEntry> _fileSearchResults = [];
    private bool _fileIndexAvailable = true;
    private bool _fileSearchPending;
    private bool _fileSearchIncludesContents;
    private bool _fileSearchWindowsSyntax;
    private int _fileSearchLocale;
    private IReadOnlyList<WindowsSettingsCatalog.Entry> _windowsSettings = [];
    private bool _windowsSettingsLoading;
    private bool _windowsSettingsLoaded;
    private Func<Task<IReadOnlyList<WindowsSettingsCatalog.Entry>>>? _windowsSettingsFixture = null;
    private Func<bool>? _windowsSearchFixture;
    private Func<Uri, bool>? _webSearchFixture;
    private bool CanHandoffToWindows => _preview || WindowsSearchLauncher.IsAvailable();
    private Func<string, CancellationToken, Task<(IReadOnlyList<AppEntry> Files, bool Available)>>? _fileSearchFixture;

    /// <summary>Rebuilds the visible results, retaining the selected app only when the query has not changed.</summary>
    private void RefreshSearchResults()
    {
        _searchRefreshDepth++;
        try { RefreshSearchResultsCore(); }
        finally { _searchRefreshDepth--; }
    }

    private static string? SearchResultKey(object? item) => (item as Button)?.Tag switch { AppEntry app => app.Id, string action => action, _ => null };

    private void SearchResultSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_searchRefreshDepth == 0 && e.AddedItems.Count > 0) _searchSelectionExplicit = true;
    }

    private void SelectSearchAction(object sender, MouseButtonEventArgs e)
    {
        SearchResults.SelectedItem = sender; _searchSelectionExplicit = true; FocusSearchResult();
    }

    private void RefreshSearchResultsCore()
    {
        var query = SearchBox.Text.Trim();
        ScheduleIndexedSearch(query);
        var selected = SearchResults.SelectedItem as Button;
        var selectedId = query == _renderedSearchQuery && (selected?.Tag is AppEntry || _searchSelectionExplicit)
            ? SearchResultKey(selected) : null;
        if (query != _renderedSearchQuery) _searchSelectionExplicit = false;
        var restoreFocus = SearchResults.IsKeyboardFocusWithin;
        SearchResults.Items.Clear();
        _renderedSearchQuery = query;
        SearchResultHint.Visibility = Visibility.Collapsed;
        SearchResultHeading.Text = "";
        WebSearchButton = SearchAction("restart:search:web", L.F(K.SearchWebFor, query), "\uE774", OpenWebSearch);
        WindowsSearchButton = SearchAction("restart:search:windows", L.T(K.SearchInWindows), "\uE721", OpenWindowsSearch);
        WindowsSearchButton.Visibility = query.Length > 0 && Document.Preferences.ShowWindowsSearchHandoff && CanHandoffToWindows ? Visibility.Visible : Visibility.Collapsed;
        WindowsSearchButton.ToolTip = L.T(K.WindowsSearchHandoffHelp);
        WebSearchButton.Visibility = Document.Preferences.ShowWebSearch && WebSearchQuery.Build(query) != null ? Visibility.Visible : Visibility.Collapsed;
        WebSearchButton.ToolTip = L.T(K.WebSearchHelp);
        if (query.Length == 0) return;
        EnsureWindowsSettingsCatalog();
        var usage = Document.Preferences.PersonalizeAppSearch && Document.Preferences.ShowMostUsed
            ? Activity.Usage(_apps, Document.Preferences.UseWindowsAppHistory ? _windowsUsage : null) : null;
        var apps = AppSearch.Find(_apps, query, usage: usage, names: WindowsAppIndex.ForLanguage(L.Language).Names);
        var settings = Document.Preferences.SearchSettings ? SettingsSearch.Find(query).Concat(WindowsSettingsCatalog.Find(_windowsSettings, query))
            .DistinctBy(a => a.LaunchTarget, StringComparer.OrdinalIgnoreCase).ToArray() : [];
        var results = apps.Concat(settings).Concat(_fileSearchResults).DistinctBy(a => a.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        var loading = !_ready && _apps.Count == 0;
        var settingsPending = Document.Preferences.SearchSettings && _windowsSettingsLoading && !_windowsSettingsLoaded;
        SearchResultHeading.Text = loading || results.Length == 0 && settingsPending ? L.T(K.StatusLoading) : results.Length == 0 ? L.T(_fileSearchPending ? K.SearchIndexSearching : K.SearchNoMatches) : L.T(K.SearchBestMatch);
        var shownCategories = new HashSet<K>();
        foreach (var app in results)
        {
            var button = AppButton(app, search: true);
            var category = app.Id.StartsWith("restart:setting:", StringComparison.Ordinal) ? K.SearchSettings
                : _fileSearchResults.Contains(app) ? K.SearchFiles : K.SearchApps;
            var content = new StackPanel();
            if (shownCategories.Add(category))
            {
                var label = new TextBlock { Text = L.T(category), FontSize = 12, Margin = new(0, 6, 0, 4) };
                label.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); content.Children.Add(label);
                button.Height = 68;
            }
            var originalContent = (UIElement)button.Content; button.Content = null;
            content.Children.Add(originalContent); button.Content = content;
            if (category == K.SearchFiles) button.ToolTip = app.LaunchTarget;
            button.Style = (Style)FindResource("SearchResultAction");
            button.PreviewMouseDown += SelectSearchAction;
            SearchResults.Items.Add(button);
        }
        if (WebSearchButton.Visibility == Visibility.Visible) SearchResults.Items.Add(WebSearchButton);
        if (WindowsSearchButton.Visibility == Visibility.Visible) SearchResults.Items.Add(WindowsSearchButton);
        SearchResultHint.Text = !_fileIndexAvailable ? L.T(K.SearchIndexUnavailable) : L.T(K.SearchHint);
        SearchResultHint.Visibility = !loading && !settingsPending && !_fileSearchPending && (results.Length == 0 || !_fileIndexAvailable) ? Visibility.Visible : Visibility.Collapsed;
        SearchResults.SelectedItem = selectedId == null ? null : SearchResults.Items.OfType<Button>().FirstOrDefault(b => SearchResultKey(b) == selectedId);
        SearchResults.SelectedItem ??= SearchResults.Items.OfType<Button>().FirstOrDefault(b => b.Tag is AppEntry)
            ?? (loading || settingsPending || _fileSearchPending ? null : SearchResults.Items.OfType<Button>().FirstOrDefault());
        if (restoreFocus && ResultsPanel.IsVisible) FocusSearchResult();
    }

    private Button SearchAction(string id, string name, string glyph, RoutedEventHandler invoke)
    {
        var button = new Button
        {
            Tag = id,
            Height = 44,
            Padding = new(4, 3, 4, 3),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Style = (Style)FindResource("SearchResultAction")
        };
        System.Windows.Automation.AutomationProperties.SetName(button, name);
        var panel = new DockPanel();
        var icon = new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 20,
            Width = 32,
            TextAlignment = TextAlignment.Center,
            Margin = new(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(icon, Dock.Left); panel.Children.Add(icon);
        panel.Children.Add(new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
        button.Content = panel; button.Click += invoke; button.PreviewMouseDown += SelectSearchAction;
        return button;
    }

    private void OpenWindowsSearch(object sender, RoutedEventArgs e)
    {
        if (!Document.Preferences.ShowWindowsSearchHandoff || !CanHandoffToWindows || _preview && _windowsSearchFixture == null) return;
        if (_windowsSearchFixture != null) { _windowsSearchFixture(); return; }
        Dismiss(immediately: true);
        if (!WindowsSearchLauncher.TryOpen()) { Present(); SetStatus(L.T(K.ErrorLaunch) + L.T(K.SearchInWindows)); }
    }

    private void OpenWebSearch(object sender, RoutedEventArgs e)
    {
        if (!Document.Preferences.ShowWebSearch || WebSearchQuery.Build(SearchBox.Text) is not { } uri) return;
        if (_webSearchFixture != null) { _webSearchFixture(uri); return; }
        if (_preview) return;
        try { ShellLauncher.Launch(uri.AbsoluteUri); Dismiss(immediately: true); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        { SetStatus(L.T(K.ErrorLaunch) + ex.Message); }
    }

    private async void EnsureWindowsSettingsCatalog()
    {
        if (_exit || !Document.Preferences.SearchSettings || _preview && _windowsSettingsFixture == null || _windowsSettingsLoading || _windowsSettingsLoaded) return;
        _windowsSettingsLoading = true;
        try
        {
            var entries = await (_windowsSettingsFixture?.Invoke() ?? WindowsSettingsCatalog.ReadAsync());
            if (_exit) return;
            _windowsSettings = entries; _windowsSettingsLoaded = true;
            RefreshSearchResults();
            await _catalog.LoadIconsAsync(entries.Select(e => e.App).Where(a => a.Id.StartsWith("restart:setting:control-panel:", StringComparison.Ordinal)).ToArray());
            if (!_exit) RefreshSearchResults();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        { _windowsSettingsLoaded = true; }
        finally { _windowsSettingsLoading = false; if (!_exit && ResultsPanel.IsVisible) RefreshSearchResults(); }
    }

    private void ScheduleIndexedSearch(string query)
    {
        var requested = Document.Preferences.SearchFiles && (!_preview || _fileSearchFixture != null) && IndexedFileQuery.Build(query) != null ? query : "";
        var contents = Document.Preferences.SearchFileContents;
        var syntax = Document.Preferences.SearchWindowsSyntax; var locale = L.Culture.LCID;
        if (requested == _fileSearchQuery && contents == _fileSearchIncludesContents && syntax == _fileSearchWindowsSyntax && locale == _fileSearchLocale) return;
        _fileSearchIncludesContents = contents;
        _fileSearchWindowsSyntax = syntax; _fileSearchLocale = locale;
        _fileSearchQuery = requested;
        _fileSearchCancellation?.Cancel(); _fileSearchCancellation?.Dispose(); _fileSearchCancellation = null;
        _fileSearchResults = []; _fileIndexAvailable = true; _fileSearchPending = requested.Length > 0;
        if (requested.Length == 0) return;
        var cancellation = new CancellationTokenSource(); _fileSearchCancellation = cancellation;
        _ = LoadIndexedResultsAsync(requested, contents, syntax, locale, cancellation.Token);
    }

    private async Task LoadIndexedResultsAsync(string query, bool includeContents, bool windowsSyntax, int locale, CancellationToken token)
    {
        try
        {
            await Task.Delay(200, token);
            var result = await (_fileSearchFixture?.Invoke(query, token) ?? _indexedFiles.FindAsync(query, token, includeContents, windowsSyntax, locale));
            token.ThrowIfCancellationRequested();
            if (_exit || SearchBox.Text.Trim() != query) return;
            _fileSearchResults = result.Files; _fileIndexAvailable = result.Available; _fileSearchPending = false;
            RefreshSearchResults();
            await _catalog.LoadIconsAsync(result.Files);
            if (!token.IsCancellationRequested && !_exit && SearchBox.Text.Trim() == query) RefreshSearchResults();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            if (!token.IsCancellationRequested && !_exit && SearchBox.Text.Trim() == query)
            { _fileIndexAvailable = false; _fileSearchPending = false; RefreshSearchResults(); }
        }
    }

    /// <summary>Moves keyboard focus to the selected native result row and scrolls it into view without changing selection.</summary>
    private void FocusSearchResult()
    {
        if (SearchResults.SelectedItem is not Button selected) return;
        SearchResults.UpdateLayout();
        if (SearchResults.ItemContainerGenerator.ContainerFromItem(selected) is ListBoxItem row)
        { row.Focus(); row.BringIntoView(); }
    }

    /// <summary>Handles search navigation and invokes the currently selected action without reranking it.</summary>
    /// <param name="e">The preview key event; handled search actions set its <see cref="RoutedEventArgs.Handled"/> flag.</param>
    /// <returns>Whether the event was consumed by search. Unhandled keys continue through normal WPF routing.</returns>
    private bool HandleSearchSelectionKey(KeyEventArgs e)
    {
        if (!ResultsPanel.IsVisible || SearchBox.Text.Trim().Length == 0 || !(SearchBox.IsKeyboardFocused || SearchResults.IsKeyboardFocusWithin)) return false;
        if (e.Key == Key.Tab && (Keyboard.Modifiers & ~ModifierKeys.Shift) == ModifierKeys.None)
        {
            if (!MoveSearchTab(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))) return false;
        }
        else if (e.Key == Key.Enter || e.Key == Key.Space && SearchResults.IsKeyboardFocusWithin)
        {
            // Launch the selected rendered action, never rerank a separate Enter-only result.
            if (SearchResults.SelectedItem is Button selected) selected.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        else if (e.Key == Key.Down)
        {
            // The first row is already selected while typing. Move from that
            // selection before transferring keyboard focus into the results.
            if (SearchResults.Items.Count > 0)
                SearchResults.SelectedIndex = Math.Min(SearchResults.SelectedIndex + 1, SearchResults.Items.Count - 1);
            _searchSelectionExplicit = true;
            FocusSearchResult();
        }
        else if (e.Key == Key.Up && SearchResults.IsKeyboardFocusWithin && SearchResults.SelectedIndex == 0) FocusSearchInput();
        else if (e.Key == Key.Up && SearchResults.IsKeyboardFocusWithin)
        { SearchResults.SelectedIndex = Math.Max(0, SearchResults.SelectedIndex - 1); _searchSelectionExplicit = true; FocusSearchResult(); }
        else if (e.Key == Key.Back && SearchResults.IsKeyboardFocusWithin && Keyboard.Modifiers == ModifierKeys.None)
        {
            FocusSearchInput();
            SearchBox.CaretIndex = SearchBox.Text.Length;
            if (SearchBox.CaretIndex > 0) SearchBox.Text = SearchBox.Text[..^1];
            SearchBox.CaretIndex = SearchBox.Text.Length;
        }
        else return false;
        e.Handled = true;
        return true;
    }

    private bool MoveSearchTab(bool backward)
    {
        if (SearchResults.Items.Count == 0 || SearchBox.IsKeyboardFocused && backward) return false;
        if (SearchBox.IsKeyboardFocused)
        {
            if (SearchResults.SelectedIndex < 0) SearchResults.SelectedIndex = 0;
        }
        else if (backward && SearchResults.SelectedIndex == 0) { FocusSearchInput(); return true; }
        else
        {
            var next = SearchResults.SelectedIndex + (backward ? -1 : 1);
            if (next < 0 || next >= SearchResults.Items.Count) return false;
            SearchResults.SelectedIndex = next;
        }
        _searchSelectionExplicit = true; FocusSearchResult(); return true;
    }
}
