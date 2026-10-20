using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private string _renderedSearchQuery = "";

    /// <summary>Rebuilds the visible results, retaining the selected app only when the query has not changed.</summary>
    private void RefreshSearchResults()
    {
        var query = SearchBox.Text.Trim();
        var selectedId = query == _renderedSearchQuery ? (SearchResults.SelectedItem as Button)?.Tag as AppEntry : null;
        var restoreFocus = SearchResults.IsKeyboardFocusWithin;
        SearchResults.Items.Clear();
        _renderedSearchQuery = query;
        SearchResultHint.Visibility = Visibility.Collapsed;
        SearchResultHeading.Text = "";
        if (query.Length == 0) return;
        var results = AppSearch.Find(_apps, query);
        var loading = !_ready && _apps.Count == 0;
        SearchResultHeading.Text = loading ? L.T(K.StatusLoading) : results.Count == 0 ? L.T(K.SearchNoMatches) : L.T(K.SearchBestMatch);
        foreach (var app in results)
        {
            var button = AppButton(app);
            button.Style = (Style)FindResource("SearchResultAction");
            button.PreviewMouseDown += (_, _) => { SearchResults.SelectedItem = button; FocusSearchResult(); };
            SearchResults.Items.Add(button);
        }
        SearchResultHint.Visibility = results.Count == 0 && !loading ? Visibility.Visible : Visibility.Collapsed;
        SearchResults.SelectedItem = SearchResults.Items.OfType<Button>().FirstOrDefault(b => b.Tag is AppEntry app && app.Id == selectedId?.Id)
            ?? SearchResults.Items.OfType<Button>().FirstOrDefault();
        if (restoreFocus && ResultsPanel.IsVisible) FocusSearchResult();
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
