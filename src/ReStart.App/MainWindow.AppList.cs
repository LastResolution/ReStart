using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private readonly HashSet<string> _expandedAppFolders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AppFolderButton> _appFolderRows = new(StringComparer.OrdinalIgnoreCase);

    private void RenderApps()
    {
        var focusedFolder = (Keyboard.FocusedElement as AppFolderButton)?.FolderKey;
        var focusedApp = ((Keyboard.FocusedElement as Button)?.Tag as AppEntry)?.Id;
        AppsPanel.Children.Clear();
        _sections.Clear();
        _appFolderRows.Clear();
        var tree = AppListTree.Build(_apps);
        foreach (var section in tree.GroupBy(n => n.Section).OrderBy(g => g.Key, StringComparer.CurrentCulture))
        {
            var header = new Button { Content = section.Key, Tag = "section", HorizontalContentAlignment = HorizontalAlignment.Left, FontSize = 20, FontWeight = FontWeights.Light, Padding = new(8, 8, 0, 8) };
            header.Click += (_, _) => OpenAlphabet();
            AppsPanel.Children.Add(header);
            _sections[section.Key] = header;
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
        if (focusedFolder != null) FocusAppFolder(focusedFolder);
        else if (focusedApp != null) Dispatcher.BeginInvoke(() =>
        {
            if (_allApps && SearchBox.Text.Length == 0) AppsPanel.Children.OfType<Button>()
                .FirstOrDefault(b => b.Tag is AppEntry app && app.Id == focusedApp)?.Focus();
        }, DispatcherPriority.Input);
    }

    private void FocusAppFolder(string key) => Dispatcher.BeginInvoke(() =>
    {
        if (_allApps && SearchBox.Text.Length == 0 && _appFolderRows.TryGetValue(key, out var row))
        { row.Focus(); row.BringIntoView(); }
    }, DispatcherPriority.Input);

    private bool FocusAppInitial(string text)
    {
        if (!_allApps || SearchBox.Text.Length != 0 || text.Length != 1 || !char.IsAsciiLetterOrDigit(text[0])
            || Ancestor<AppListPanel>(Keyboard.FocusedElement as DependencyObject) == null) return false;
        var rows = AppsPanel.Children.OfType<Button>().Where(b => b.Tag is AppEntry || b is AppFolderButton).ToArray();
        var start = Array.IndexOf(rows, Keyboard.FocusedElement as Button);
        for (var offset = 1; offset <= rows.Length; offset++)
        {
            var row = rows[(start + offset) % rows.Length];
            if (!System.Windows.Automation.AutomationProperties.GetName(row).StartsWith(text, StringComparison.CurrentCultureIgnoreCase)) continue;
            row.Focus(); row.BringIntoView();
            return true;
        }
        // A navigation letter without a matching visible row should not switch
        // an explicitly focused app list into search unexpectedly.
        return true;
    }
}
