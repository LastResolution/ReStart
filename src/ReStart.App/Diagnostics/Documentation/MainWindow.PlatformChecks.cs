using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Checks native monitor placement and accessibility providers with a hidden host and fictional catalog.</summary>
    /// <param name="directory">Local output directory for a sanitized JSON report; no monitor names, account data or app catalog are recorded.</param>
    /// <returns>A task completing after every connected monitor and eight language/two theme provider combinations have been checked.</returns>
    /// <remarks>Does not change display settings, synthesize physical touch or verify Narrator's spoken output.</remarks>
    internal async Task VerifyPlatformAsync(string directory)
    {
        if (!_preview) throw new InvalidOperationException("Platform verification requires preview mode.");
        var foreground = Native.GetForegroundWindow();
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        _clock.Stop(); _ready = true;
        _apps = [new("fixture:alpha", "Alpha Editor", ""), new("fixture:beta", "Beta Editor", "")];
        Document = new()
        {
            Preferences = new() { Language = "en", Animate = false, ShowWallpaper = false },
            Groups = [new() { Name = "Sample group", Tiles = _apps.Select(Tile.FromApp).ToList() }]
        };
        ApplyAppearance(); RenderGroups(); RenderApps();
        var screens = System.Windows.Forms.Screen.AllScreens;
        var monitors = new List<object>();
        Content = null;
        using var host = HiddenRenderHost(Root, 800, 600);
        try
        {
            // Reuse one HWND across monitors so Windows sends real DPI changes.
            foreach (var screen in screens.Concat(screens.Reverse()))
            {
                var area = screen.WorkingArea;
                Check(Native.SetWindowPos(host.Handle, 0, area.X, area.Y, area.Width, area.Height, 0x0014), "Native monitor placement failed.");
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                var dpi = PlatformWindowDpi(host.Handle);
                var scale = VisualTreeHelper.GetDpi(Root);
                Check(dpi >= 96 && Math.Abs(scale.PixelsPerInchX - dpi) < .1 && Math.Abs(scale.PixelsPerInchY - dpi) < .1, "WPF and native window DPI disagree.");
                Check(PlatformWindowRect(host.Handle, out var actual) && actual.Left == area.Left && actual.Top == area.Top
                    && actual.Right == area.Right && actual.Bottom == area.Bottom, "The native host does not match the monitor working area.");
                Root.Width = area.Width / scale.DpiScaleX; Root.Height = area.Height / scale.DpiScaleY;
                Root.Measure(new(Root.Width, Root.Height)); Root.Arrange(new Rect(0, 0, Root.Width, Root.Height)); Root.UpdateLayout(); FitPanels(); Root.UpdateLayout();
                Check(Math.Abs(Root.ActualWidth * scale.DpiScaleX - area.Width) < 1 && Math.Abs(Root.ActualHeight * scale.DpiScaleY - area.Height) < 1,
                    "Logical content does not fit the physical working area.");
                var point = new System.Drawing.Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
                Check(System.Windows.Forms.Screen.FromPoint(point).Bounds == screen.Bounds, "Pointer-to-monitor selection is inconsistent.");
                monitors.Add(new { primary = screen.Primary, x = area.X, y = area.Y, width = area.Width, height = area.Height, dpi, scalePercent = dpi * 100 / 96 });
            }
            var cases = 0;
            foreach (var language in TextCatalog.SupportedLanguages)
                foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
                {
                    Document.Preferences.Language = language; Document.Preferences.Theme = theme;
                    ApplyAppearance(); RenderGroups(); RenderApps();
                    ResetToPinnedView(); Root.UpdateLayout();
                    foreach (var rail in new FrameworkElement[] { NavigationRail, ExpandedRail })
                        foreach (var button in VisualChildren<Button>(rail))
                        {
                            var peer = UIElementAutomationPeer.CreatePeerForElement(button);
                            Check(peer != null && !string.IsNullOrWhiteSpace(peer.GetName()) && peer.GetAutomationControlType() == AutomationControlType.Button
                                && peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider, "A navigation action lacks a named invokable Button provider.");
                        }
                    foreach (var view in _selectionViews.Values)
                    {
                        var peer = UIElementAutomationPeer.CreatePeerForElement(view.Button);
                        Check(peer != null && !string.IsNullOrWhiteSpace(peer.GetName()) && peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider,
                            "An app tile lacks a named invokable provider.");
                    }
                    SetSmokeSearch("Editor"); Root.UpdateLayout();
                    var searchPeer = UIElementAutomationPeer.CreatePeerForElement(SearchBox);
                    Check(searchPeer != null && !string.IsNullOrWhiteSpace(searchPeer.GetName()) && searchPeer.GetAutomationControlType() == AutomationControlType.Edit
                        && searchPeer.GetPattern(PatternInterface.Value) is IValueProvider, "Search lacks a named editable Value provider.");
                    var selection = UIElementAutomationPeer.CreatePeerForElement(SearchResults)?.GetPattern(PatternInterface.Selection) as ISelectionProvider;
                    var selectedRow = SearchResults.ItemContainerGenerator.ContainerFromItem(SearchResults.SelectedItem) as ListBoxItem;
                    Check(selection?.GetSelection().Length == 1 && selectedRow != null
                        && UIElementAutomationPeer.CreatePeerForElement(selectedRow)?.GetName() == "Alpha Editor", "The selected search result is not named in UI Automation.");
                    cases++;
                }
            Check(!IsVisible && Native.GetForegroundWindow() == foreground, "Platform verification showed a window or changed foreground focus.");
            File.WriteAllText(Path.Combine(directory, "verification.json"), JsonSerializer.Serialize(new
            {
                success = true,
                checks,
                automationCases = cases,
                monitorCount = screens.Length,
                monitorPasses = monitors,
                maximumTouchContacts = PlatformMetric(95),
                digitizerFlags = PlatformMetric(94),
                narratorInstalled = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "Narrator.exe")),
                narratorSpeechVerified = false,
                physicalTouchVerified = false
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { host.RootVisual = null; Content = Root; }
    }

    [DllImport("user32.dll", EntryPoint = "GetDpiForWindow")] private static extern uint PlatformWindowDpi(nint window);
    [DllImport("user32.dll", EntryPoint = "GetSystemMetrics")] private static extern int PlatformMetric(int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowRect")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool PlatformWindowRect(nint window, out PlatformRect rect);
    [StructLayout(LayoutKind.Sequential)]
    private struct PlatformRect { public int Left, Top, Right, Bottom; }
}
