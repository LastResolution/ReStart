using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using ReStart.App.Tablet;
using ReStart.Core;

namespace ReStart.App;

// Tablet edition: reliable taps on Home. WPF's touch stack can miss the first contact after the
// window appears (the touch reaches WPF only as Windows' promoted mouse input, at a stale press
// point), so a tap would neither launch nor reach a dashboard button. The raw WM_POINTER
// messages are exact: when a short, still touch ends without WPF raising a Click, the tap is
// replayed here at the finger's position.
public partial class MainWindow
{
    private const int WmPointerUpdate = 0x0245, WmPointerDown = 0x0246, WmPointerUp = 0x0247, WmPointerCaptureChanged = 0x024C;
    private (uint Id, Point Screen, long At)? _pointerDown;
    private long _lastClickAt;

    private void InstallTabletTaps()
    {
        HwndSource.FromHwnd(new WindowInteropHelper(this).EnsureHandle())?.AddHook(PointerHook);
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, _) => _lastClickAt = Environment.TickCount64), true);
    }

    private nint PointerHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg is not (WmPointerDown or WmPointerUp or WmPointerUpdate or WmPointerCaptureChanged) || !TabletShell.IsTablet) return 0;
        var id = (uint)(wParam.ToInt64() & 0xFFFF);
        try
        {
            // The rearranging gesture (MainWindow.TabletEditing.cs) follows the same exact stream.
            if (msg == WmPointerCaptureChanged) { TabletPointerCancelled(id); return 0; }
            if (!GetPointerType(id, out var type) || type is not (2 or 3)) // PT_TOUCH, PT_PEN
            { if (msg == WmPointerDown && _layoutEditing) TabletLog.Write("home", $"edit mode: pointer {id} down skipped (type {type})"); return 0; }
            var screen = new Point((short)(lParam.ToInt64() & 0xFFFF), (short)((lParam.ToInt64() >> 16) & 0xFFFF));
            if (msg == WmPointerUpdate) { TabletPointerMoved(id, screen); return 0; }
            if (msg == WmPointerDown) { _pointerDown = (id, screen, Environment.TickCount64); TabletPointerPressed(id, screen); return 0; }
            TabletPointerReleased(id, screen);
            if (_pointerDown is not { } down || down.Id != id) return 0;
            _pointerDown = null;
            var still = Math.Abs(screen.X - down.Screen.X) < 24 && Math.Abs(screen.Y - down.Screen.Y) < 24; // physical px (~12 DIP)
            if (!still || Environment.TickCount64 - down.At > 600) return 0;
            // Give WPF's own promotion time to click first; replay only when it did not.
            _ = Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(250);
                if (_lastClickAt >= down.At || _exit || !IsVisible || _dragging || _dialogDepth > 0) return;
                ReplayTap(screen);
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        { TabletLog.Write("home", "pointer: " + ex.Message); }
        return 0;
    }

    private void ReplayTap(Point screen)
    {
        var point = PointFromScreen(screen);
        if (InputHitTest(point) is not DependencyObject hit || Ancestor<Button>(hit) is not { IsEnabled: true } button) return;
        if (button.Tag is Tile tile)
        {
            TabletLog.Write("home", "tap replayed on " + TileDisplayName(tile));
            if (_dismissing) return;
            if (_layoutEditing) { if (!TabletEditTap(tile, button)) ToggleTileSelection(tile.Id); return; }
            if (tile.Widget == "script") InteractWithScriptTile(tile, button.PointFromScreen(screen));
            else if (tile.IsFolder) OpenFolder(tile);
            else _ = LaunchTileAsync(tile);
            return;
        }
        // Rail and other buttons: an ordinary invoke, as a click would.
        TabletLog.Write("home", "tap replayed on " + (button.Name.Length > 0 ? button.Name : button.GetType().Name));
        (new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke) as IInvokeProvider)?.Invoke();
    }

    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetPointerType(uint pointerId, out int pointerType);
}
