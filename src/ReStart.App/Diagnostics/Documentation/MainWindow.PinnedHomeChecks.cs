using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Checks the empty pinned-home action against the rail instead of a tile-size-dependent offset.</summary>
    /// <param name="directory">Owned fixture directory receiving only fictional empty-home screenshots.</param>
    /// <returns>The number of geometry, visibility and lock assertions performed.</returns>
    private int VerifyEmptyPinnedHome(string directory)
    {
        var previous = Document; var width = Width; var height = Height; var checks = 0;
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        try
        {
            Document = new() { Preferences = new() { ShowWallpaper = false, Animate = false } };
            SetLayoutEditing(false); ResetToPinnedView();
            foreach (var language in TextCatalog.SupportedLanguages)
                foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
                    foreach (var viewport in new[] { 640d, 1100d, 1920d })
                        foreach (var scale in new[] { .7, 1d, 1.5 })
                        {
                            Width = viewport; Height = 800;
                            Document.Preferences.Language = language; Document.Preferences.Theme = theme; Document.Preferences.TileScale = scale;
                            ApplyAppearance(); RenderGroups(); UpdateLayout();
                            var action = GroupsPanel.Children.OfType<Button>().Single();
                            var actionY = action.TranslatePoint(new(0, action.ActualHeight / 2), Root).Y;
                            var pinnedY = PinnedNav.TranslatePoint(new(0, PinnedNav.ActualHeight / 2), Root).Y;
                            Check(Math.Abs(actionY - pinnedY) <= 2 && action.ActualWidth <= ContentScroll.ViewportWidth + 1,
                                "The empty-home action is not aligned with Pinned tiles or exceeds its viewport.");
                            Check(ContentScroll.ScrollableWidth <= 1 && ContentScroll.ScrollableHeight <= 1,
                                "An empty pinned home introduced unnecessary scrolling.");
                            Check(AutomationProperties.GetName(action) == L.T(K.EmptyGroups), "The empty-home action lost its localized accessible name.");
                            if (language == "en" && viewport == 1100 && scale == 1)
                                SaveScreenshot(Path.Combine(directory, $"empty-pinned-{theme}.png"));
                        }
            Document.Preferences.LayoutLocked = true; RenderGroups(); UpdateLayout();
            Check(!GroupsPanel.Children.OfType<Button>().Single().IsEnabled, "The empty-home action bypassed the layout lock.");
            Document.Preferences.LayoutLocked = false; RenderGroups();
            ShowAllApps(this, new()); UpdateLayout(); Check(!GroupsPanel.IsVisible, "The empty-home action overlaps All apps.");
            ShowPinned(this, new()); SearchBox.Text = "Sample"; UpdateLayout(); Check(!GroupsPanel.IsVisible, "The empty-home action overlaps search results.");
            ShowPinned(this, new()); UpdateLayout(); Check(GroupsPanel.IsVisible && GroupsPanel.Children.OfType<Button>().Single().IsVisible, "Returning home lost the empty action.");
        }
        finally { Document = previous; Width = width; Height = height; ApplyAppearance(); ResetToPinnedView(); RenderGroups(); }
        return checks;
    }
}
