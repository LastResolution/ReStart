using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Verifies all-apps directory expansion, keyboard/provider semantics, hidden-leaf search and responsive header chains.</summary>
    internal AppFolderSmokeResult VerifyAppFolders(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("App-folder verification requires preview mode.");
        var original = _apps;
        var originalExpanded = _expandedAppFolders.ToArray();
        var originalWidth = Root.Width;
        var originalHeight = Root.Height;
        var originalTheme = Document.Preferences.Theme;
        try
        {
            _apps = [new("root", "Root sample", ""), new("alpha", "Alpha sample", "", FolderPath: "Sample tools"),
                new("beta", "Beta sample", "", FolderPath: "Sample tools"), new("gamma", "Gamma sample", "", FolderPath: "Sample tools/Nested samples"),
                new("delta", "Delta sample", "", FolderPath: "Other tools"), new("zulu", "Zulu sample", "")];
            _expandedAppFolders.Clear();
            Root.Width = 1920;
            Root.Height = 1000;
            RenderApps();
            ShowAllApps(this, new());
            UpdateLayout();
            var root = AppListTree.Build(_apps).Single(n => n.Name == "Sample tools");
            var nested = root.Children.Single(n => n.IsFolder);
            foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
            {
                Document.Preferences.Theme = theme;
                ApplyAppearance();
                UpdateLayout();
                var initialFolder = _appFolderRows[root.Key];
                Require(initialFolder.Background is System.Windows.Media.SolidColorBrush { Color.A: 0 }
                    && initialFolder.Foreground is System.Windows.Media.SolidColorBrush foreground
                    && foreground.Color == ((System.Windows.Media.SolidColorBrush)FindResource("TextBrush")).Color,
                    "The directory row does not follow the shared theme.");
                SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"app-folders-{theme}.png"));
            }
            Document.Preferences.Theme = originalTheme;
            ApplyAppearance();
            Require(!VisibleAppNames().Contains("Alpha sample"), "A collapsed directory still shows its app rows.");
            _appFolderRows[root.Key].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            UpdateLayout();
            Require(VisibleAppNames().Contains("Alpha sample") && !VisibleAppNames().Contains("Gamma sample"), "Folder expansion ignored an ancestor boundary.");
            var peer = UIElementAutomationPeer.CreatePeerForElement(_appFolderRows[root.Key]);
            var provider = peer?.GetPattern(PatternInterface.ExpandCollapse) as IExpandCollapseProvider;
            Require(provider?.ExpandCollapseState == ExpandCollapseState.Expanded, "The directory does not expose its expanded accessibility state.");
            provider!.Collapse();
            UpdateLayout();
            Require(!VisibleAppNames().Contains("Alpha sample"), "The accessibility Collapse action did not collapse the directory.");
            RaiseFolderKey(_appFolderRows[root.Key], Key.Right);
            UpdateLayout();
            Require(_expandedAppFolders.Contains(root.Key), "Right arrow did not expand the folder.");
            RaiseFolderKey(_appFolderRows[root.Key], Key.Left);
            UpdateLayout();
            Require(!_expandedAppFolders.Contains(root.Key), "Left arrow did not collapse the folder.");
            OpenAlphabet();
            Require(AlphabetButtons.Children.OfType<Button>().Count() >= 27
                && !AlphabetButtons.Children.OfType<Button>().Single(b => b.Content is "A").IsEnabled,
                "The alphabet picker did not expose unavailable letters as disabled.");
            RenderApps();
            var sectionButton = AlphabetButtons.Children.OfType<Button>().Single(b => b.Content is "S");
            sectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(_sections["S"].IsKeyboardFocused, "The alphabet picker focused an obsolete header after a redraw.");
            FocusSearchInput();
            SearchBox.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, Environment.TickCount, Key.Down)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Require(Keyboard.FocusedElement is AppFolderButton, "Down arrow did not enter the all-apps list from idle search.");
            Require(FocusAppInitial("Z") && ((Keyboard.FocusedElement as Button)?.Tag as AppEntry)?.Id == "zulu"
                && SearchBox.Text.Length == 0, "An alphabet key did not navigate the focused app list.");
            SetSmokeSearch("Gamma");
            Require(SearchResultNames.SequenceEqual(new[] { "Gamma sample" }), "Search lost a hidden app inside nested directories.");
            ShowAllApps(this, new());
            _appFolderRows[root.Key].SetExpanded(true);
            _appFolderRows[nested.Key].SetExpanded(true);
            foreach (var width in new[] { 1920, 3840 })
            {
                Root.Width = width;
                UpdateLayout();
                VerifyAppFolderBounds();
                SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"app-folders-{width}.png"));
            }
            Hide();
            Present();
            UpdateLayout();
            Require(_expandedAppFolders.Count == 0 && GroupsPanel.IsVisible, "Reopening retained all-apps directory state.");
            return new(4, 2, 1, 1, 2, 3);
        }
        finally
        {
            _apps = original;
            _expandedAppFolders.Clear();
            _expandedAppFolders.UnionWith(originalExpanded);
            Root.Width = originalWidth;
            Root.Height = originalHeight;
            Document.Preferences.Theme = originalTheme;
            ApplyAppearance();
            RenderApps();
            ResetToPinnedView();
        }
    }

    private string[] VisibleAppNames() => AppsPanel.Children.OfType<Button>().Where(b => b.Tag is AppEntry).Select(b => ((AppEntry)b.Tag).Name).ToArray();

    private static void RaiseFolderKey(AppFolderButton folder, Key key) => folder.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
        PresentationSource.FromVisual(folder) ?? throw new InvalidOperationException("The app folder has no presentation source."), Environment.TickCount, key)
    { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    private void VerifyAppFolderBounds()
    {
        var rows = AppsPanel.Children.Cast<FrameworkElement>().ToArray();
        var bounds = rows.Select(row => row.TransformToAncestor(AppsPanel).TransformBounds(new Rect(row.RenderSize))).ToArray();
        Require(Math.Abs(bounds.Max(b => b.Right) - ContentScroll.ViewportWidth) <= 1, "Expanded directory chains left unused right-hand columns.");
        for (var i = 0; i < rows.Length - 1; i++)
            if (rows[i].Tag is "section")
                Require(Math.Abs(bounds[i].Left - rows[i].Margin.Left - bounds[i + 1].Left + rows[i + 1].Margin.Left) <= 1,
                    "An alphabet or folder header was split from the following row.");
    }
}

internal sealed record AppFolderSmokeResult(int ExpansionChecks, int LayoutWidths, int HiddenSearchChecks, int ReopenChecks, int ThemeChecks, int NavigationChecks);
