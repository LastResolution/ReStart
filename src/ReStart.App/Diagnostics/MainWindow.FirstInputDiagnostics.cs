using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Exercises routed first-character input before catalog completion in a fresh, isolated launcher window.</summary>
    internal async Task<FirstInputSmokeResult> VerifyFirstInputAsync(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("First-input verification requires preview mode.");
        var ready = new TaskCompletionSource<List<AppEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var directory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "fixture-" + Guid.NewGuid().ToString("N"));
        var fixture = new MainWindow(new LayoutStore(Path.Combine(directory, "layout.json")), true, () => ready.Task)
        {
            Owner = this,
            Width = 1280,
            Height = 800,
            Topmost = false,
            ShowInTaskbar = false
        };
        var integrationUpdates = 0;
        fixture.PreferencesChanged = () => integrationUpdates++;
        try
        {
            fixture.Present();
            fixture.UpdateLayout();
            Require(!fixture.IsReady && fixture.SearchBox.IsKeyboardFocused,
                "Cold startup did not focus the native search TextBox before catalog completion.");
            Require(integrationUpdates > 0, "Input integration preferences still wait for catalog completion.");
            fixture.ComposeFixtureText("E");
            Require(fixture.SearchBox.Text == "E", "The first routed character was lost or duplicated at cold startup.");
            Require(fixture.SearchResultHeading.Text == L.T(K.StatusLoading),
                "Pending search reports no matches before the catalog has loaded.");
            fixture.SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "first-input-loading.png"));
            fixture.PinnedNav.Focus();
            ready.SetResult([new("fixture-edge", "Microsoft Edge", "", false), new("fixture-editor", "Editor sample", "", false)]);
            await fixture.InitializeAsync();
            fixture.UpdateLayout();
            Require(fixture.SearchBox.Text == "E" && fixture.SearchResultNames.Contains("Microsoft Edge") && fixture.ResultsPanel.IsVisible,
                "The pending E query was not refreshed when the catalog became ready.");
            Require(fixture.PinnedNav.IsKeyboardFocused, "Catalog completion stole keyboard focus from the user's navigation control.");
            fixture.SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "first-input-ready.png"));
            fixture.ComposeFixtureText("d");
            Require(fixture.SearchBox.Text == "Ed" && fixture.SearchResultNames.Contains("Microsoft Edge"),
                "Input after catalog/icon completion lost the current query or caret.");
            fixture.Hide();
            fixture.Present();
            fixture.UpdateLayout();
            fixture.ComposeFixtureText("E");
            Require(fixture.SearchBox.Text == "E" && fixture.SearchResultNames.Contains("Microsoft Edge"),
                "The first character after reopening did not search the ready catalog.");
            fixture.Hide();
            fixture.Present();
            fixture.PinnedNav.Focus();
            fixture.ComposeFixtureText("E");
            Require(fixture.SearchBox.Text == "E" && fixture.SearchResultNames.Contains("Microsoft Edge"),
                "First-character search failed when the navigation button owned keyboard focus.");
            fixture._verifyNativeActivation = true;
            for (var attempt = 0; attempt < 10; attempt++)
            {
                fixture.Hide(); fixture.Present();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Require(Native.GetForegroundWindow() == new WindowInteropHelper(fixture).Handle && fixture.SearchBox.IsKeyboardFocused,
                    $"The native activation path failed at {attempt}: hwnd={new WindowInteropHelper(fixture).Handle}, foreground={Native.GetForegroundWindow()}, active={fixture.IsActive}, visible={fixture.IsVisible}, focus={Keyboard.FocusedElement?.GetType().Name}, searchFocused={fixture.SearchBox.IsKeyboardFocused}.");
                fixture.ComposeFixtureText("E");
                Require(fixture.SearchResultNames.Contains("Microsoft Edge"), $"Native activation lost the first character: attempt={attempt}, query={fixture.SearchBox.Text}, focus={Keyboard.FocusedElement?.GetType().Name}.");
            }
            var other = new Window { Width = 200, Height = 100, ShowInTaskbar = false, Content = new TextBox() };
            try
            {
                other.Show(); other.Activate();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                await fixture.WaitForMotionAsync();
                Require(!fixture.IsVisible, "The launcher remained visible after another window took focus.");
            }
            finally { other.Close(); }
            var foregroundChecks = await fixture.VerifyForegroundDismissalAsync();
            var windowsKeyChecks = await fixture.VerifyWindowsKeyReturnAsync();
            return new(1, 1, 1, 1, 1, 1, 10, 1, foregroundChecks, windowsKeyChecks);
        }
        finally
        {
            ready.TrySetResult([]);
            await fixture.InitializeAsync();
            fixture.Exit();
            Activate();
        }
    }

    private void ComposeFixtureText(string text)
    {
        var source = Keyboard.FocusedElement ?? throw new InvalidOperationException("The input fixture has no focused element.");
        TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, source, text));
    }

    private sealed class WindowsKeyReturnBackend : IWindowsSearchHandoffBackend
    {
        internal int Dismissals;
        internal TaskCompletionSource<bool> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ModifiersHeld => false;
        public bool Open() => throw new InvalidOperationException("A Windows-key return must not launch native Search.");
        public Task<bool> TransferAsync(string query, CancellationToken cancellation) => throw new InvalidOperationException("A Windows-key return must not transfer text.");
        public Task<bool> DismissForegroundAsync() { Dismissals++; return Completion.Task; }
    }

    private async Task<int> VerifyWindowsKeyReturnAsync()
    {
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        var previous = _verifyNativeActivation; var close = Document.Preferences.CloseOnDeactivate;
        var backend = new WindowsKeyReturnBackend();
        using var handoff = new WindowsSearchHandoff(backend);
        _windowsKeyReturnFixture = handoff; _verifyNativeActivation = true;
        Document.Preferences.CloseOnDeactivate = false;
        System.Drawing.Point? shownAt = null;
        void Visibility(object sender, DependencyPropertyChangedEventArgs args) { if (IsVisible) shownAt = _startButtonPoint; }
        IsVisibleChanged += Visibility;
        var genericToggles = 0; var startToggles = 0;
        using var integration = new StartIntegration(new WindowInteropHelper(this).Handle, Dispatcher,
            () => genericToggles++, () => { }, point => { startToggles++; ToggleFromStartButton(point); }, ToggleFromWindowsKey);
        integration.Apply(new Preferences { CaptureWindowsKey = false, CaptureStartButton = false, CloseOnDeactivate = false });
        async Task Until(Func<bool> condition)
        {
            var deadline = Environment.TickCount64 + 2500;
            while (!condition() && Environment.TickCount64 < deadline) await Task.Delay(20);
            Check(condition(), "The owned Windows-key return did not settle.");
        }
        try
        {
            foreach (var (point, startButton) in new[] { (new Native.Point { X = -1200, Y = 80 }, false), (new Native.Point { X = 1700, Y = 150 }, false), (new Native.Point { X = -1000, Y = 50 }, true) })
            {
                Hide(); shownAt = null;
                backend.Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                var count = backend.Dismissals;
                if (startButton) integration.QueueStartButtonToggle(point);
                else integration.QueueWindowsKeyToggle(point);
                Check(backend.Dismissals == count, "The input adapter ran foreground work synchronously instead of queuing it.");
                await Until(() => backend.Dismissals == count + 1);
                Check(!IsVisible && _returningFromWindowsSearch, "ReStart appeared before native dismissal completed.");
                backend.Completion.SetResult(true);
                await Until(() => IsVisible && !_returningFromWindowsSearch);
                Check(shownAt == new System.Drawing.Point(point.X, point.Y), "A deferred Windows-key open lost its captured display point.");
                Check(SearchBox.IsKeyboardFocused, "The Windows-key return did not focus the search box.");
                ComposeFixtureText("E"); Check(SearchBox.Text == "E", "The first character after a Windows-key return was lost.");
                ToggleFromWindowsKey(point);
                await Until(() => !IsVisible);
                Check(backend.Dismissals == count + 1, "Closing an active launcher tried to dismiss an external surface.");
            }
            Check(genericToggles == 0 && startToggles == 1, "A Windows-key request was confused with a different activation source.");
            return checks;
        }
        finally
        {
            backend.Completion.TrySetResult(false);
            IsVisibleChanged -= Visibility;
            _windowsKeyReturnFixture = null; _startButtonPoint = null;
            _verifyNativeActivation = previous; Document.Preferences.CloseOnDeactivate = close;
        }
    }

    private async Task<int> VerifyForegroundDismissalAsync()
    {
        // Disable WPF's one-shot Deactivated path: these checks must pass using
        // the native foreground subscription after an intermediate activation.
        _verifyNativeActivation = false;
        Document.Preferences.Animate = false;
        var first = new Window { Title = "Sample preview", Width = 200, Height = 100, ShowInTaskbar = false };
        var second = new Window { Title = "Sample application", Width = 200, Height = 100, ShowInTaskbar = false };
        using var integration = new StartIntegration(new WindowInteropHelper(this).Handle, Dispatcher, () => { }, DismissFromTaskbar, _ => { });
        var preferences = new Preferences { CaptureWindowsKey = false, CaptureStartButton = false, CloseOnDeactivate = true };
        var checks = 0;
        try
        {
            integration.Apply(preferences);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                Present(); integration.SetMenuOpen(true);
                await WaitForWatchAsync(true);
                _dialogDepth++;
                first.Show(); first.Activate();
                await Task.Delay(100);
                Require(IsVisible && !IsActive, "The intermediate activation fixture did not leave an inactive visible launcher.");
                _dialogDepth--;
                second.Show(); second.Activate();
                await Task.Delay(150);
                Require(!IsVisible, "A second foreground transition left the inactive launcher visible."); checks++;
                integration.SetMenuOpen(false); await WaitForWatchAsync(false);
            }
            Present(); integration.SetMenuOpen(true); await WaitForWatchAsync(true);
            var owned = new Window { Owner = this, Width = 180, Height = 100, ShowInTaskbar = false };
            try
            {
                owned.Show(); owned.Activate(); await Task.Delay(100);
                Require(IsVisible, $"An owned application dialog was treated as a foreign foreground window: owner={Native.GetAncestor(new WindowInteropHelper(this).Handle, 3)}, dialogOwner={Native.GetAncestor(new WindowInteropHelper(owned).Handle, 3)}, foreground={Native.GetForegroundWindow()}, dialog={new WindowInteropHelper(owned).Handle}."); checks++;
            }
            finally { owned.Close(); }
            preferences.CloseOnDeactivate = false; Document.Preferences.CloseOnDeactivate = false;
            integration.Apply(preferences); await WaitForWatchAsync(false);
            first.Activate(); await Task.Delay(100);
            Require(IsVisible, "Disabling close-on-deactivation still dismissed the launcher."); checks++;
            Document.Preferences.CloseOnDeactivate = true;
            integration.SetMenuOpen(false);
            return checks;
        }
        finally { first.Close(); second.Close(); _dialogDepth = 0; }

        async Task WaitForWatchAsync(bool enabled)
        {
            var until = Environment.TickCount64 + 2000;
            while (integration.WatchesForeground != enabled && Environment.TickCount64 < until) await Task.Delay(20);
            Require(integration.WatchesForeground == enabled, "Foreground observation did not follow menu visibility/preferences.");
        }
    }
}

internal sealed record FirstInputSmokeResult(int ColdStartChecks, int QueryContinuationChecks, int ReopenChecks, int NavigationFocusChecks,
    int IntegrationBeforeCatalogChecks, int FocusRetentionChecks, int NativeActivationChecks, int DeactivationChecks, int ForegroundDismissalChecks, int WindowsKeyReturnChecks);
