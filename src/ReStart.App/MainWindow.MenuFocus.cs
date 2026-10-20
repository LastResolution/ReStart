using System.Windows.Controls;
using System.Windows.Threading;

namespace ReStart.App;

public partial class MainWindow
{
    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();
        menu.Closed += (_, _) => Dispatcher.BeginInvoke(RecheckAfterMenu, DispatcherPriority.Background);
        return menu;
    }

    private void RecheckAfterMenu()
    {
        // A popup can own the deactivation event. Closing that popup after a
        // taskbar/thumbnail switch must not leave an inactive Start on screen.
        if (!IsVisible || _dialogDepth != 0 || _dragging || IsAnyMenuOpen()) return;
        if (!IsActive && Document.Preferences.CloseOnDeactivate && (!_preview || _verifyNativeActivation)) Dismiss();
        else if (IsActive && !IsKeyboardFocusWithin) FocusSearchInput();
    }
}
