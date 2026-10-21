using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

internal sealed record NavigationShortcut(string Id, TextKey Label, string Glyph, Func<string> Target);

public partial class MainWindow
{
    internal static readonly NavigationShortcut[] FolderShortcuts =
    [
        new("documents", K.NavDocuments, "\uE8A5", () => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
        new("downloads", K.NavDownloads, "\uE896", DownloadsPath),
        new("music", K.NavMusic, "\uE8D6", () => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)),
        new("pictures", K.NavPictures, "\uEB9F", () => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
        new("videos", K.NavVideos, "\uE714", () => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)),
        new("network", K.NavNetwork, "\uE968", () => "shell:NetworkPlacesFolder"),
        new("personal", K.NavPersonalFolder, "\uE8B7", () => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
    ];
    private DispatcherTimer? _railHoverTimer;
    private bool _railPinnedOpen;

    private static string DownloadsPath()
    {
        var id = new Guid("374de290-123f-4565-9164-39c4925e467b");
        if (SHGetKnownFolderPath(ref id, 0, 0, out var pointer) < 0) return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        try { return System.Runtime.InteropServices.Marshal.PtrToStringUni(pointer)!; }
        finally { System.Runtime.InteropServices.Marshal.FreeCoTaskMem(pointer); }
    }
    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, nint token, out nint path);

    private void InstallNavigationBehavior()
    {
        _railHoverTimer = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(600) };
        _railHoverTimer.Tick += (_, _) =>
        {
            _railHoverTimer.Stop();
            if (Document.Preferences.ExpandNavigationOnHover && NavigationRail.IsMouseOver && !_dragging && !_layoutEditing && !IsAnyMenuOpen()) SetRailOpen(true);
        };
        NavigationRail.MouseEnter += (_, _) => { if (Document.Preferences.ExpandNavigationOnHover && !_dragging && ExpandedRail.Visibility != Visibility.Visible) _railHoverTimer.Start(); };
        NavigationRail.MouseLeave += (_, _) => _railHoverTimer.Stop();
        ExpandedRail.MouseLeave += (_, _) =>
        {
            if (!_railPinnedOpen && !ExpandedRail.IsKeyboardFocusWithin && !IsAnyMenuOpen()) SetRailOpen(false);
        };
        ExpandedRail.LostKeyboardFocus += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (!_railPinnedOpen && !ExpandedRail.IsMouseOver && !ExpandedRail.IsKeyboardFocusWithin && !IsAnyMenuOpen()) SetRailOpen(false);
        });
        PreviewMouseDown += (_, e) =>
        {
            if (ExpandedRail.Visibility == Visibility.Visible && !ExpandedRail.IsMouseOver && !NavigationRail.IsMouseOver)
                SetRailOpen(false);
        };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { _railHoverTimer.Stop(); _railPinnedOpen = false; ExpandedRail.Visibility = Visibility.Collapsed; } };
    }

    private void SetRailOpen(bool open, bool explicitToggle = false)
    {
        _railHoverTimer?.Stop();
        _railPinnedOpen = open && explicitToggle;
        ExpandedRail.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshNavigationShortcuts()
    {
        var picture = _preview || SystemParameters.HighContrast ? null : ReStart.App.Services.AccountPictureSource.Current();
        CollapsedAccountLabel.Picture = picture; UserRailLabel.Picture = picture;
        if (!Document.Preferences.ExpandNavigationOnHover) _railHoverTimer?.Stop();
        if (CollapsedFolderShortcuts == null) return;
        CollapsedFolderShortcuts.Children.Clear(); ExpandedFolderShortcuts.Children.Clear();
        var enabled = Document.Preferences.NavigationFolders ?? ["documents", "pictures"];
        foreach (var shortcut in FolderShortcuts.Where(s => enabled.Contains(s.Id, StringComparer.OrdinalIgnoreCase)))
            foreach (var expanded in new[] { false, true })
            {
                var button = new Button { Height = 48, Padding = new(0), ToolTip = L.T(shortcut.Label), HorizontalContentAlignment = HorizontalAlignment.Stretch };
                System.Windows.Automation.AutomationProperties.SetName(button, L.T(shortcut.Label));
                button.Content = new RailLabel { Glyph = shortcut.Glyph, Label = expanded ? L.T(shortcut.Label) : "" };
                button.Click += (_, _) => LaunchPath(shortcut.Target());
                (expanded ? ExpandedFolderShortcuts : CollapsedFolderShortcuts).Children.Add(button);
            }
    }
}
