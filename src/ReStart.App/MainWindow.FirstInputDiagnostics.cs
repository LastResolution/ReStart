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
                Require(fixture.SearchResultNames.Contains("Microsoft Edge"), "Native activation lost the first character.");
            }
            var other = new Window { Width = 200, Height = 100, ShowInTaskbar = false, Content = new TextBox() };
            try
            {
                other.Show(); other.Activate();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Require(!fixture.IsVisible, "The launcher remained visible after another window took focus.");
            }
            finally { other.Close(); }
            return new(1, 1, 1, 1, 1, 1, 10, 1);
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
}

internal sealed record FirstInputSmokeResult(int ColdStartChecks, int QueryContinuationChecks, int ReopenChecks, int NavigationFocusChecks,
    int IntegrationBeforeCatalogChecks, int FocusRetentionChecks, int NativeActivationChecks, int DeactivationChecks);
