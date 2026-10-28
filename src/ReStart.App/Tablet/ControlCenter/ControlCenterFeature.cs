using System.Windows.Threading;

namespace ReStart.App.Tablet.ControlCenter;

/// <summary>
/// Wires Control Center and the side button into the tablet shell:
/// <see cref="TabletShell.ShowControlCenter"/>, <see cref="TabletShell.OverlaysClosing"/> and
/// the commands <c>side-button</c>, <c>lock</c>, <c>screen-off</c>, <c>control-center-state</c>, <c>control-center-test</c>.
/// </summary>
internal static class ControlCenterFeature
{
    private static ControlCenterWindow? _window;

    /// <summary>Called once at startup after <see cref="TabletShell.Initialize"/>.</summary>
    internal static void Initialize(Dispatcher dispatcher)
    {
        SystemActions.Initialize();
        TabletShell.ShowControlCenter = Show;
        TabletShell.IsControlCenterOpen = () => _window?.IsOpen == true;
        TabletShell.OverlaysClosing += () => { if (_window?.IsOpen == true) _window.Dismiss(); };
        TabletCommands.Register("side-button", argument => SystemActions.SideButton(dispatcher, argument));
        TabletCommands.Register("lock", _ => SystemActions.Lock());
        TabletCommands.Register("screen-off", _ => SystemActions.ScreenOff());
        TabletCommands.Register("control-center-state", argument => { _ = ControlCenterProbe.LogStateAsync(); });
        TabletCommands.Register("control-center-test", argument => { _ = ControlCenterProbe.RunAsync(argument ?? ""); });
        // Build the window while the launcher idles, so the first swipe does not pay for it.
        var warm = new DispatcherTimer(TimeSpan.FromSeconds(4), DispatcherPriority.ApplicationIdle, (s, _) =>
        {
            ((DispatcherTimer)s!).Stop();
            try { (_window ??= new ControlCenterWindow()).Warm(); }
            catch (Exception ex) { TabletLog.Write("cc", "warm failed: " + ex); }
        }, dispatcher);
        warm.Start();
    }

    internal static ControlCenterWindow? Window => _window;

    /// <summary>Opens Control Center (the swipe-down gesture and <c>--tablet-command control-center</c>).</summary>
    internal static void Show()
    {
        try { (_window ??= new ControlCenterWindow()).Open(); }
        catch (Exception ex) { TabletLog.Write("cc", "open failed: " + ex); }
    }
}
