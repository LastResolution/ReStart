using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ReStart.Core;
using ReStart.App.Services;

namespace ReStart.App;

public partial class MainWindow
{
    private string _renderedSearchQuery = "";
    private string _fileSearchQuery = "";
    private CancellationTokenSource? _fileSearchCancellation;
    private readonly IndexedFileSearch _indexedFiles = new();
    private IReadOnlyList<AppEntry> _fileSearchResults = [];
    private bool _fileIndexAvailable = true;
    private bool _fileSearchPending;
    private Func<string, CancellationToken, Task<(IReadOnlyList<AppEntry> Files, bool Available)>>? _fileSearchFixture;

    /// <summary>Rebuilds the visible results, retaining the selected app only when the query has not changed.</summary>
    private void RefreshSearchResults()
    {
        var query = SearchBox.Text.Trim();
        ScheduleIndexedSearch(query);
        var selectedId = query == _renderedSearchQuery ? (SearchResults.SelectedItem as Button)?.Tag as AppEntry : null;
        var restoreFocus = SearchResults.IsKeyboardFocusWithin;
        SearchResults.Items.Clear();
        _renderedSearchQuery = query;
        SearchResultHint.Visibility = Visibility.Collapsed;
        SearchResultHeading.Text = "";
        if (query.Length == 0) return;
        var apps = AppSearch.Find(_apps, query);
        var settings = Document.Preferences.SearchSettings ? SettingsSearch.Find(query) : [];
        var results = apps.Concat(settings).Concat(_fileSearchResults).DistinctBy(a => a.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        var loading = !_ready && _apps.Count == 0;
        SearchResultHeading.Text = loading ? L.T(K.StatusLoading) : results.Length == 0 ? L.T(_fileSearchPending ? K.SearchIndexSearching : K.SearchNoMatches) : L.T(K.SearchBestMatch);
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
            button.PreviewMouseDown += (_, _) => { SearchResults.SelectedItem = button; FocusSearchResult(); };
            SearchResults.Items.Add(button);
        }
        SearchResultHint.Text = !_fileIndexAvailable ? L.T(K.SearchIndexUnavailable) : L.T(K.SearchHint);
        SearchResultHint.Visibility = !loading && !_fileSearchPending && (results.Length == 0 || !_fileIndexAvailable) ? Visibility.Visible : Visibility.Collapsed;
        SearchResults.SelectedItem = SearchResults.Items.OfType<Button>().FirstOrDefault(b => b.Tag is AppEntry app && app.Id == selectedId?.Id)
            ?? SearchResults.Items.OfType<Button>().FirstOrDefault();
        if (restoreFocus && ResultsPanel.IsVisible) FocusSearchResult();
    }

    private void ScheduleIndexedSearch(string query)
    {
        var requested = Document.Preferences.SearchFiles && (!_preview || _fileSearchFixture != null) && IndexedFileQuery.Build(query) != null ? query : "";
        if (requested == _fileSearchQuery) return;
        _fileSearchQuery = requested;
        _fileSearchCancellation?.Cancel(); _fileSearchCancellation?.Dispose(); _fileSearchCancellation = null;
        _fileSearchResults = []; _fileIndexAvailable = true; _fileSearchPending = requested.Length > 0;
        if (requested.Length == 0) return;
        var cancellation = new CancellationTokenSource(); _fileSearchCancellation = cancellation;
        _ = LoadIndexedResultsAsync(requested, cancellation.Token);
    }

    private async Task LoadIndexedResultsAsync(string query, CancellationToken token)
    {
        try
        {
            await Task.Delay(200, token);
            var result = await (_fileSearchFixture?.Invoke(query, token) ?? _indexedFiles.FindAsync(query, token));
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
        if (e.Key == Key.Enter || e.Key == Key.Space && SearchResults.IsKeyboardFocusWithin)
        {
            // Launch the selected rendered action, never rerank a separate Enter-only result.
            if (SearchResults.SelectedItem is Button selected) selected.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        else if (e.Key == Key.Down && SearchBox.IsKeyboardFocused)
        {
            // The first row is already selected while typing. Move from that
            // selection before transferring keyboard focus into the results.
            if (SearchResults.Items.Count > 0)
                SearchResults.SelectedIndex = Math.Min(SearchResults.SelectedIndex + 1, SearchResults.Items.Count - 1);
            FocusSearchResult();
        }
        else if (e.Key == Key.Up && SearchResults.IsKeyboardFocusWithin && SearchResults.SelectedIndex == 0) FocusSearchInput();
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
}
