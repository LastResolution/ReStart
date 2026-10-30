using System.ComponentModel;
using System.Windows.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private Func<StartSessionState>? _sessionStateFixture;
    private Action<StartSessionAction>? _sessionActionFixture;
    private Func<bool>? _sessionConfirmationFixture;
    private Func<Task<bool>>? _updateRequiredFixture;
    private Task<bool>? _windowsUpdateRead;
    private long _windowsUpdateReadAt;

    private StartSessionState SessionState() => _sessionStateFixture?.Invoke()
        ?? (_preview ? new(false, false, true, true, true, true, false) : StartSessionCommands.Read());

    private ContextMenu CreateAccountMenu()
    {
        var menu = CreateContextMenu(); var state = SessionState();
        Add(menu, L.T(K.AccountSettings), () => LaunchPath("ms-settings:yourinfo"));
        if (state.Lock) AddSessionItem(menu, StartSessionAction.Lock, K.AccountLock);
        if (state.SignOut) AddSessionItem(menu, StartSessionAction.SignOut, K.AccountSignOut);
        if (state.SwitchUser) AddSessionItem(menu, StartSessionAction.SwitchUser, K.AccountSwitchUser);
        return menu;
    }

    private ContextMenu CreatePowerMenu()
    {
        var menu = CreateContextMenu(); var state = SessionState();
        foreach (var (action, label) in StartSessionCommands.PowerItems(state)) AddSessionItem(menu, action, label);
        if (!state.Power) { var empty = new MenuItem { Header = L.T(K.ErrorAppCommandUnavailable), IsEnabled = false }; menu.Items.Add(empty); }
        var generation = 0;
        menu.Closed += (_, _) => generation++;
        menu.Opened += async (_, _) =>
        {
            var current = ++generation;
            if (!state.Power || state.UpdateRequired || _preview && _updateRequiredFixture == null) return;
            try
            {
                if (_windowsUpdateRead == null || _windowsUpdateRead.IsCompleted && Environment.TickCount64 - _windowsUpdateReadAt > 30000)
                { _windowsUpdateReadAt = Environment.TickCount64; _windowsUpdateRead = _updateRequiredFixture?.Invoke() ?? StartSessionCommands.UpdateRequiredAsync(); }
                if (!await _windowsUpdateRead.WaitAsync(TimeSpan.FromSeconds(2)) || !menu.IsOpen || current != generation || !SessionState().Power) return;
                foreach (var (action, label) in StartSessionCommands.PowerItems(state with { UpdateRequired = true }).Where(item => item.Action is StartSessionAction.UpdateRestart or StartSessionAction.UpdateShutdown))
                    AddSessionItem(menu, action, label);
            }
            catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or System.Runtime.InteropServices.COMException) { }
        };
        return menu;
    }

    private void AddSessionItem(ContextMenu menu, StartSessionAction action, TextKey label)
    {
        var row = new MenuItem { Header = L.T(label), Tag = action, IsEnabled = !_preview || _sessionActionFixture != null };
        row.Click += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, row) || _preview && _sessionActionFixture == null) return;
            if (action != StartSessionAction.Lock && !(_sessionConfirmationFixture?.Invoke() ?? Confirm(L.F(K.ConfirmPower, L.T(label))))) return;
            try
            {
                if (_sessionActionFixture != null) _sessionActionFixture(action);
                else { Dismiss(immediately: true); StartSessionCommands.Execute(action); }
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or UnauthorizedAccessException)
            { if (!_preview) Present(); SetStatus(L.T(K.ErrorAppCommandUnavailable)); }
        };
        menu.Items.Add(row);
    }
}
