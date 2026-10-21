using System.Windows;
using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Verifies actual WPF layout and hide/present transitions using an in-memory app list in isolated preview mode.</summary>
    internal NavigationSmokeResult VerifyNavigation(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("Navigation verification requires preview mode.");
        var originalApps = _apps;
        var originalWidth = Root.Width;
        var originalHeight = Root.Height;
        var originalAnimation = Document.Preferences.Animate;
        var sizes = new List<AppListMeasurement>();
        var reopenChecks = 0;
        var prefix = Path.Combine(Path.GetDirectoryName(snapshotPath)!, Path.GetFileNameWithoutExtension(snapshotPath));
        try
        {
            // These names and inert targets are fixtures, never persisted or launched.
            _apps = Enumerable.Range(0, 12).SelectMany(letter => Enumerable.Range(1, 24)
                .Select(index => new AppEntry($"fixture-{letter}-{index}", $"{(char)('A' + letter)} Sample application {index:D2}", "", false))).ToList();
            Document.Preferences.Animate = false;
            RenderApps();
            foreach (var width in new[] { 800, 1280, 1920, 2560 })
            {
                // The CI desktop caps native HWND dimensions. Measure the real root
                // at an explicit DIP width so wide-screen cases remain reproducible.
                Root.Width = width;
                Root.Height = 900;
                ShowAllApps(this, new());
                UpdateLayout();
                Require(Math.Abs(Root.ActualWidth - width) <= 1, "The requested rendering width was not applied.");
                var bounds = AppsPanel.Children.Cast<FrameworkElement>().Select(child =>
                    child.TransformToAncestor(AppsPanel).TransformBounds(new Rect(child.RenderSize))).ToArray();
                var right = bounds.Max(b => b.Right);
                Require(Math.Abs(AppsPanel.ActualWidth - ContentScroll.ViewportWidth) <= 1, "App list does not fill the viewport.");
                Require(Math.Abs(right - AppsPanel.ActualWidth) <= 1, "App columns leave an unused right-hand region.");
                Require(bounds.All(b => b.Left >= -1 && b.Right <= AppsPanel.ActualWidth + 1), "An app row overflows the viewport.");
                Require(width < 1920 || AppsPanel.Columns > 4, "Wide app lists are still capped at four columns.");
                for (var i = 0; i < bounds.Length - 1; i++)
                    if (AppsPanel.Children[i] is FrameworkElement { Tag: "section" })
                        Require(Math.Abs(bounds[i].Left - bounds[i + 1].Left) <= 1, "A section header was separated from its first app.");
                sizes.Add(new(width, ContentScroll.ViewportWidth, AppsPanel.Columns, right));
                SaveScreenshot($"{prefix}-apps-{width}.png");

                ContentScroll.ScrollToEnd();
                UpdateLayout();
                Require(ContentScroll.VerticalOffset > 0, "The scrolling fixture did not scroll.");
                ExpandedRail.Visibility = Visibility.Visible;
                OpenAlphabet();
                var fixtureGroup = new TileGroup { Name = "Fixture", Tiles = [new() { Label = "Fixture folder", Children = [Tile.FromApp(_apps[0])] }] };
                Document.Groups.Add(fixtureGroup);
                OpenFolder(fixtureGroup.Tiles[0]);
                Hide();
                Present();
                Document.Groups.Remove(fixtureGroup);
                RenderGroups();
                UpdateLayout();
                VerifyHome();
                reopenChecks++;

                ShowAllApps(this, new());
                SetSmokeSearch("Sample");
                Hide();
                Present();
                UpdateLayout();
                VerifyHome();
                reopenChecks++;
                SaveScreenshot($"{prefix}-home-{width}.png");
            }
            return new(sizes.ToArray(), reopenChecks);
        }
        finally
        {
            _apps = originalApps;
            Root.Width = originalWidth;
            Root.Height = originalHeight;
            RenderApps();
            ResetToPinnedView();
            Document.Preferences.Animate = originalAnimation;
        }
    }

    private void VerifyHome()
    {
        Require(GroupsPanel.IsVisible && !AppsPanel.IsVisible && !ResultsPanel.IsVisible, "Reopening did not return to tiles.");
        Require(SearchBox.Text.Length == 0 && TopBar.Height == 0, "Reopening retained search state.");
        Require(ContentScroll.VerticalOffset == 0, "Reopening retained the previous scroll offset.");
        Require(ExpandedRail.Visibility != Visibility.Visible && AlphabetOverlay.Visibility != Visibility.Visible
            && _openFolder == null, "Reopening retained an overlay or inline folder.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal sealed record AppListMeasurement(int LayoutWidth, double ViewportWidth, int Columns, double RightEdge);
internal sealed record NavigationSmokeResult(AppListMeasurement[] Sizes, int ReopenChecks);
