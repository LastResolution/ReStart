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
            Require(fixture.ResultsPanel.Children.OfType<TextBlock>().Any(t => t.Text == L.T(K.StatusLoading)),
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
            return new(1, 1, 1, 1, 1, 1, 10, 1, foregroundChecks);
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
    int IntegrationBeforeCatalogChecks, int FocusRetentionChecks, int NativeActivationChecks, int DeactivationChecks, int ForegroundDismissalChecks);
