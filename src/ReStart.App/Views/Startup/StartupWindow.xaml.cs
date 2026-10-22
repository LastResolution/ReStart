using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

/// <summary>Shows startup progress while retaining native text composition for the first search.</summary>
internal partial class StartupWindow : Window
{
    private System.Drawing.Point? _targetPoint;
    private bool _centeringPending;
    internal event Action? CancelRequested;
    internal bool LaunchRequested { get; private set; }
    internal StartupWindow(Func<bool> closeOnDeactivate)
    {
        InitializeComponent(); ThemeWindow.Attach(this);
        CancelButton.Click += (_, _) => CancelRequested?.Invoke();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { CancelRequested?.Invoke(); e.Handled = true; }
            else if (e.Key == Key.Enter && Query.IsKeyboardFocused) { LaunchRequested = Query.Text.Trim().Length > 0; e.Handled = true; }
        };
        Query.TextChanged += (_, _) => LaunchRequested = false;
        Deactivated += (_, _) => Dispatcher.BeginInvoke(() => { if (IsVisible && !IsActive && closeOnDeactivate()) CancelRequested?.Invoke(); });
        ContentRendered += (_, _) => Query.Focus();
        SizeChanged += (_, _) => QueueCentering();
    }

    internal void ShowAt(System.Drawing.Point point, bool requestForeground)
    {
        _targetPoint = point;
        var handle = new WindowInteropHelper(this).EnsureHandle();
        // A hidden Window reports no desired height. Move it onto the target
        // monitor first, then center its real SizeToContent bounds before paint.
        CenterOnTargetMonitor();
        var opacity = Opacity; Opacity = 0;
        try { Show(); UpdateLayout(); CenterOnTargetMonitor(); }
        finally { Opacity = opacity; }
        if (requestForeground) ForegroundActivation.Request(handle); Activate(); Query.Focus();
    }

    private void CenterOnTargetMonitor()
    {
        if (_targetPoint is not { } point) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0 || !Native.GetWindowRect(handle, out var bounds)) return;
        var area = System.Windows.Forms.Screen.FromPoint(point).WorkingArea;
        var left = area.Left + (area.Width - (bounds.Right - bounds.Left)) / 2;
        var top = area.Top + (area.Height - (bounds.Bottom - bounds.Top)) / 2;
        Native.SetWindowPos(handle, 0, left, top, 0, 0, 0x0015); // No size, activation or Z-order change.
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        QueueCentering();
    }

    private void QueueCentering()
    {
        if (_targetPoint == null || _centeringPending) return;
        _centeringPending = true;
        // SizeChanged can precede the HWND resize. Wait for WPF to commit its
        // native size (and DPI suggestion) before measuring physical bounds.
        Dispatcher.BeginInvoke(() => { _centeringPending = false; CenterOnTargetMonitor(); }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    internal void UpdateProgress(TextKey text, double fraction)
    {
        Stage.Text = L.T(text);
        ((ScaleTransform)Progress.RenderTransform).ScaleX = Math.Clamp(fraction, 0, 1);
    }
}
