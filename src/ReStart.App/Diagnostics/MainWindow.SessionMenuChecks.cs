using System.Windows;
using System.Windows.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private async Task<int> VerifySessionMenusAsync(MainWindow fixture, string directory)
    {
        var checks = 0;
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        var state = new StartSessionState(true, true, true, true, true, true, true);
        var effects = new List<StartSessionAction>();
        fixture._sessionStateFixture = () => state;
        fixture._sessionActionFixture = effects.Add;
        fixture._sessionConfirmationFixture = () => true;
        var anchor = fixture.AppButton(new("session-fixture", "Session fixture", ""), true);
        fixture.AppsPanel.Children.Add(anchor);
        try
        {
            _ = StartSessionCommands.Read(); // Real read-only Windows ABI/policy path; never invoke it in diagnostics.
            foreach (var language in L.SupportedLanguages)
                foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
                {
                    fixture.Document.Preferences.Language = language; fixture.Document.Preferences.Theme = theme; fixture.ApplyAppearance();
                    state = new(true, true, true, true, true, true, true);
                    var power = fixture.CreatePowerMenu();
                    Check(power.Items.Count == 6, "An available sleep, hibernate or Windows update action is missing.");
                    foreach (var (action, label) in StartSessionCommands.PowerItems(state))
                    {
                        var item = power.Items.OfType<MenuItem>().Single(row => Equals(row.Tag, action));
                        Check(Equals(item.Header, L.T(label)), "A power command is not localized.");
                        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
                        Check(effects.Last() == action, "A power command routes to a different session effect.");
                    }
                    var account = fixture.CreateAccountMenu();
                    Check(account.Items.OfType<MenuItem>().Any(row => Equals(row.Tag, StartSessionAction.SwitchUser) && Equals(row.Header, L.T(K.AccountSwitchUser))), "The localized switch-user action is absent.");
                    foreach (var item in account.Items.OfType<MenuItem>().Where(row => row.Tag is StartSessionAction))
                    { item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item)); Check(Equals(effects.Last(), item.Tag), "An account action lost its routing."); }
                    if (language == "en")
                    {
                        power.PlacementTarget = anchor; power.IsOpen = true; await Task.Delay(20);
                        SaveMenuSnapshot(power, Path.Combine(directory, $"power-{theme}.png")); await CloseMenuAsync(power);
                        account.PlacementTarget = anchor; account.IsOpen = true; await Task.Delay(20);
                        SaveMenuSnapshot(account, Path.Combine(directory, $"account-{theme}.png")); await CloseMenuAsync(account);
                    }
                }
            foreach (var sleep in new[] { false, true })
                foreach (var hibernate in new[] { false, true })
                    foreach (var powerEnabled in new[] { false, true })
                    {
                        state = new(sleep, hibernate, powerEnabled, false, false, false, false);
                        var items = fixture.CreatePowerMenu().Items.OfType<MenuItem>().Where(row => row.Tag is StartSessionAction).ToArray();
                        Check(items.Any(row => Equals(row.Tag, StartSessionAction.Sleep)) == (sleep && powerEnabled), "Sleep ignores its capability or policy.");
                        Check(items.Any(row => Equals(row.Tag, StartSessionAction.Hibernate)) == (hibernate && powerEnabled), "Hibernate ignores its capability or policy.");
                        Check(items.Length == (powerEnabled ? 2 + (sleep ? 1 : 0) + (hibernate ? 1 : 0) : 0), "A disabled power menu retains an executable action.");
                        Check(fixture.CreateAccountMenu().Items.OfType<MenuItem>().All(row => row.Tag is not StartSessionAction), "An account action ignores Windows policy.");
                    }
            state = new(true, true, true, true, true, true, false);
            fixture._sessionConfirmationFixture = () => false;
            var before = effects.Count;
            foreach (var row in fixture.CreatePowerMenu().Items.OfType<MenuItem>()) row.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, row));
            Check(effects.Count == before, "Cancelling confirmation executed a power effect.");
            fixture._sessionConfirmationFixture = () => true;
            fixture._updateRequiredFixture = () => Task.FromResult(true); fixture._windowsUpdateRead = null;
            var dynamicMenu = fixture.CreatePowerMenu(); dynamicMenu.PlacementTarget = anchor; dynamicMenu.IsOpen = true; await Task.Delay(60);
            Check(dynamicMenu.Items.OfType<MenuItem>().Count(row => row.Tag is StartSessionAction.UpdateShutdown or StartSessionAction.UpdateRestart) == 2, "Windows update readiness did not populate the open power menu.");
            await CloseMenuAsync(dynamicMenu);
            var delayed = new TaskCompletionSource<bool>(); fixture._updateRequiredFixture = () => delayed.Task; fixture._windowsUpdateRead = null;
            var stale = fixture.CreatePowerMenu(); stale.PlacementTarget = anchor; stale.IsOpen = true; await CloseMenuAsync(stale);
            delayed.SetResult(true); await Task.Delay(30);
            Check(stale.Items.OfType<MenuItem>().All(row => row.Tag is not (StartSessionAction.UpdateShutdown or StartSessionAction.UpdateRestart)), "A closed power menu accepted obsolete update results.");
            foreach (var action in new[] { StartSessionAction.Shutdown, StartSessionAction.Restart, StartSessionAction.UpdateShutdown, StartSessionAction.UpdateRestart })
            {
                var flags = StartSessionCommands.ShutdownFlags(action);
                Check((flags & 3) == 0, "A shutdown request forces another user or application to close.");
                Check(((flags & 0x40) != 0) == (action is StartSessionAction.UpdateShutdown or StartSessionAction.UpdateRestart), "Install-updates intent was lost or added to an ordinary power action.");
            }
            return checks;
        }
        finally
        {
            fixture._sessionStateFixture = null; fixture._sessionActionFixture = null; fixture._sessionConfirmationFixture = null;
            fixture._updateRequiredFixture = null; fixture._windowsUpdateRead = null; fixture.AppsPanel.Children.Remove(anchor);
        }
    }
}
