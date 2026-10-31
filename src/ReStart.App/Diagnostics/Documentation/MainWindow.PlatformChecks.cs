using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Checks native monitor placement and accessibility providers with an isolated fictional catalog.</summary>
    /// <param name="directory">Local output directory for a sanitized JSON report; no monitor names, account data or app catalog are recorded.</param>
    /// <param name="visible">Whether to show the isolated fixture to exercise a real WPF window; false preserves foreground focus.</param>
    /// <param name="interactive">Whether to leave the fictional window open for up to three minutes for manual keyboard/screen-reader checks; requires <paramref name="visible"/>.</param>
    /// <returns>A task completing after every connected monitor and eight language/two theme provider combinations have been checked.</returns>
    /// <remarks>Does not change display settings, synthesize physical touch or verify Narrator's spoken output.</remarks>
    internal async Task VerifyPlatformAsync(string directory, bool visible = false, bool interactive = false)
    {
        if (!_preview) throw new InvalidOperationException("Platform verification requires preview mode.");
        if (interactive && !visible) throw new ArgumentException("Interactive verification requires a visible window.", nameof(interactive));
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
        System.Windows.Interop.HwndSource? host = null;
        nint handle;
        if (visible)
        {
            _initialization = Task.CompletedTask;
            Topmost = false;
            ShowInTaskbar = true;
            Document.Preferences.CloseOnDeactivate = false;
            Show();
            handle = new WindowInteropHelper(this).Handle;
        }
        else
        {
            Content = null;
            host = HiddenRenderHost(Root, 800, 600);
            handle = host.Handle;
        }
        try
        {
            // Reuse one HWND across monitors so Windows sends real DPI changes.
            foreach (var screen in screens.Concat(screens.Reverse()))
            {
                var area = screen.WorkingArea;
                Check(MonitorPlacement.FitWorkingArea(handle, 0, area, 0x0014), "Native monitor placement failed.");
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                var dpi = PlatformWindowDpi(handle);
                var scale = VisualTreeHelper.GetDpi(Root);
                Check(dpi >= 96 && Math.Abs(scale.PixelsPerInchX - dpi) < .1 && Math.Abs(scale.PixelsPerInchY - dpi) < .1, "WPF and native window DPI disagree.");
                Check(PlatformWindowRect(handle, out var actual) && actual.Left == area.Left && actual.Top == area.Top
                    && actual.Right == area.Right && actual.Bottom == area.Bottom,
                    $"Native host bounds ({actual.Left},{actual.Top})-({actual.Right},{actual.Bottom}) do not match working area ({area.Left},{area.Top})-({area.Right},{area.Bottom}) at {dpi} DPI.");
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
            Check(visible || !IsVisible && Native.GetForegroundWindow() == foreground, "Platform verification showed a window or changed foreground focus.");
            File.WriteAllText(Path.Combine(directory, "verification.json"), JsonSerializer.Serialize(new
            {
                success = true,
                checks,
                automationCases = cases,
                monitorCount = screens.Length,
                visibleWindow = visible,
                monitorPasses = monitors,
                maximumTouchContacts = PlatformMetric(95),
                digitizerFlags = PlatformMetric(94),
                narratorInstalled = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "Narrator.exe")),
                narratorSpeechVerified = false,
                physicalTouchVerified = false
            }, new JsonSerializerOptions { WriteIndented = true }));
            if (interactive)
            {
                MonitorPlacement.Release(handle);
                Document.Preferences.Language = "en"; Document.Preferences.Theme = ReStart.Core.ThemeMode.Dark;
                Document.Groups.Add(new()
                {
                    Name = "Scroll right",
                    X = Math.Ceiling(Math.Max(ContentScroll.ActualWidth, 800) / Cell) + 8,
                    Row = 0,
                    Tiles = Enum.GetValues<TileSize>().Select(size => new Tile { AppId = "fixture:alpha", Label = size + " sample", Size = size }).ToList()
                });
                Document.Groups.Add(new()
                {
                    Name = "Scroll down",
                    X = 0,
                    Row = (int)Math.Ceiling(Math.Max(ContentScroll.ActualHeight, 800) / Cell) + 8,
                    Tiles = _apps.Select(Tile.FromApp).ToList()
                });
                ApplyAppearance(); RenderGroups(); RenderApps(); ResetToPinnedView();
                SetStatus("");
                Root.ClearValue(WidthProperty); Root.ClearValue(HeightProperty);
                Title = "ReStart platform verification";
                WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize;
                WindowState = WindowState.Maximized;
                _touchInput = new();
                var hidden = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args) { if (!IsVisible) hidden.TrySetResult(); }
                IsVisibleChanged += OnVisibilityChanged;
                try { await Task.WhenAny(hidden.Task, Task.Delay(TimeSpan.FromMinutes(3))); }
                finally
                {
                    IsVisibleChanged -= OnVisibilityChanged;
                    _touchPanning.Release();
                    _touchInput!.FinalPanningMode = (int)ContentScroll.PanningMode;
                    File.WriteAllText(Path.Combine(directory, "touch-input.json"), JsonSerializer.Serialize(_touchInput, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true }));
                    _touchInput = null;
                }
            }
        }
        finally
        {
            if (host != null) { host.RootVisual = null; host.Dispose(); Content = Root; }
            Root.ClearValue(WidthProperty); Root.ClearValue(HeightProperty);
            if (visible) Hide();
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetDpiForWindow")] private static extern uint PlatformWindowDpi(nint window);
    [DllImport("user32.dll", EntryPoint = "GetSystemMetrics")] private static extern int PlatformMetric(int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowRect")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool PlatformWindowRect(nint window, out PlatformRect rect);
    [StructLayout(LayoutKind.Sequential)]
    private struct PlatformRect { public int Left, Top, Right, Bottom; }
}
