using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ReStart.App.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private readonly HashSet<string> _expandedAppFolders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AppFolderButton> _appFolderRows = new(StringComparer.OrdinalIgnoreCase);
    private WindowsAppIndex _appIndex = WindowsAppIndex.ForLanguage(L.Language);
    private string[] _appAlphabet = [];
    private bool _recentAppsExpanded;

    private void RenderApps()
    {
        RefreshWindowsUsage();
        var focusedFolder = (Keyboard.FocusedElement as AppFolderButton)?.FolderKey;
        var focusedApp = ((Keyboard.FocusedElement as Button)?.Tag as AppEntry)?.Id;
        var focusedRecentToggle = (Keyboard.FocusedElement as Button)?.Uid == "recent-expand";
        AppsPanel.Children.Clear();
        _sections.Clear();
        _appFolderRows.Clear();
        if (Activity.Observe(_apps, DateTimeOffset.Now)) SaveActivity();
        void HistorySection(K heading, IReadOnlyList<AppEntry> apps)
        {
            if (apps.Count == 0) return;
            var text = new TextBlock { Text = L.T(heading), FontSize = 14 };
            text.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            AppsPanel.Children.Add(new Border { Tag = "section", Padding = new(8, 12, 0, 8), Child = text });
            foreach (var app in apps)
            {
                var row = AppButton(app); row.Uid = "history";
                row.ContextMenu = CreateAppMenu(app, history: heading);
                AutomationProperties.SetItemStatus(row, L.T(heading));
                AppsPanel.Children.Add(row);
            }
        }
        var allRecent = DisplayPreferences.ShowRecentlyAdded ? Activity.Recent(_apps, DateTimeOffset.Now, 4096) : [];
        if (allRecent.Count <= 3) _recentAppsExpanded = false;
        HistorySection(K.RecentlyAdded, _recentAppsExpanded ? allRecent : allRecent.Take(3).ToArray());
        if (allRecent.Count > 3)
        {
            var expand = new Button
            {
                Content = L.T(_recentAppsExpanded ? K.RecentAppsShowLess : K.RecentAppsShowMore),
                Uid = "recent-expand",
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new(8, 8, 0, 8)
            };
            AutomationProperties.SetName(expand, (string)expand.Content);
            AutomationProperties.SetItemStatus(expand, L.T(K.RecentlyAdded));
            expand.Click += (_, _) =>
            {
                _recentAppsExpanded = !_recentAppsExpanded; RenderApps();
                AppsPanel.Children.OfType<Button>().FirstOrDefault(b => b.Uid == "recent-expand")?.Focus();
            };
            AppsPanel.Children.Add(expand);
        }
        if (DisplayPreferences.ShowMostUsed) HistorySection(K.MostUsed, Activity.Frequent(_apps, _windowsUsage));
        var previousAlphabet = _appAlphabet;
        var focusedLetter = (Keyboard.FocusedElement as Button)?.Content as string;
        _appIndex = WindowsAppIndex.ForLanguage(L.Language);
        var tree = AppListTree.Build(_apps, _appIndex.Names);
        _appAlphabet = _appIndex.Alphabet(tree.Select(n => n.Name));
        var sections = tree.GroupBy(n => _appIndex.Section(n.Name)).ToDictionary(g => g.Key, StringComparer.Ordinal);
        foreach (var label in _appAlphabet.Where(sections.ContainsKey))
        {
            var section = sections[label];
            var header = new Button { Content = label, Tag = "section", HorizontalContentAlignment = HorizontalAlignment.Left, FontSize = 20, FontWeight = FontWeights.Light, Padding = new(8, 8, 0, 8) };
            header.Click += (_, _) => OpenAlphabet();
            AppsPanel.Children.Add(header);
            _sections[label] = header;
            foreach (var row in AppListTree.VisibleRows(section, _expandedAppFolders))
            {
                Button button;
                if (row.Node.App is AppEntry app) button = AppButton(app);
                else
                {
                    var node = row.Node;
                    _catalog.FolderIcons.TryGetValue(node.FolderPath, out var icon);
                    var expanded = _expandedAppFolders.Contains(node.Key);
                    var folder = new AppFolderButton(node.Name, node.Key, expanded, icon, value =>
                    {
                        if (value) _expandedAppFolders.Add(node.Key);
                        else _expandedAppFolders.Remove(node.Key);
                        RenderApps();
                        FocusAppFolder(node.Key);
                    });
                    folder.Tag = expanded ? "section" : null;
                    if (_catalog.FolderEntry(node.FolderPath) is AppEntry directory) folder.ContextMenu = CreateAppMenu(directory);
                    _appFolderRows[node.Key] = folder;
                    button = folder;
                }
                button.Margin = new(Math.Min(row.Depth, 6) * 16, 0, 0, 0);
                AppsPanel.Children.Add(button);
            }
        }
        FitPanels();
        if (AlphabetOverlay.Visibility == Visibility.Visible && (!previousAlphabet.SequenceEqual(_appAlphabet)
            || AlphabetButtons.Children.OfType<Button>().Any(b => b.IsEnabled != _sections.ContainsKey((string)b.Content))))
        {
            OpenAlphabet();
            AlphabetButtons.Children.OfType<Button>().FirstOrDefault(b => b.IsEnabled && Equals(b.Content, focusedLetter))?.Focus();
        }
        if (focusedFolder != null) FocusAppFolder(focusedFolder);
        else if (focusedApp != null) Dispatcher.BeginInvoke(() =>
        {
            if (_allApps && SearchBox.Text.Length == 0) AppsPanel.Children.OfType<Button>()
                .FirstOrDefault(b => b.Tag is AppEntry app && app.Id == focusedApp)?.Focus();
        }, DispatcherPriority.Input);
        else if (focusedRecentToggle) AppsPanel.Children.OfType<Button>().FirstOrDefault(b => b.Uid == "recent-expand")?.Focus();
    }

    private void FocusAppFolder(string key) => Dispatcher.BeginInvoke(() =>
    {
        if (_allApps && SearchBox.Text.Length == 0 && _appFolderRows.TryGetValue(key, out var row))
        { row.Focus(); row.BringIntoView(); }
    }, DispatcherPriority.Input);

    private bool FocusAppInitial(string text)
    {
        if (!_allApps || SearchBox.Text.Length != 0 || !_appIndex.IsInitial(text)
            || Ancestor<AppListPanel>(Keyboard.FocusedElement as DependencyObject) == null) return false;
        var rows = AppsPanel.Children.OfType<Button>().Where(b => b.Tag is AppEntry || b is AppFolderButton).ToArray();
        var start = Array.IndexOf(rows, Keyboard.FocusedElement as Button);
        for (var offset = 1; offset <= rows.Length; offset++)
        {
            var row = rows[(start + offset) % rows.Length];
            var name = row.Tag is AppEntry app ? app.Name : System.Windows.Automation.AutomationProperties.GetName(row);
            if (_appIndex.Section(name) != _appIndex.Section(text)) continue;
            row.Focus(); row.BringIntoView();
            return true;
        }
        // A navigation letter without a matching visible row should not switch
        // an explicitly focused app list into search unexpectedly.
        return true;
    }
}
