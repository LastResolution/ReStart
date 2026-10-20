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
    internal event Action? CancelRequested;
    internal bool LaunchRequested { get; private set; }
    internal StartupWindow(Func<bool> closeOnDeactivate)
    {
        InitializeComponent(); ThemeWindow.Attach(this);
        CancelButton.Click += (_, _) => CancelRequested?.Invoke();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { CancelRequested?.Invoke(); e.Handled = true; }
            else if (e.Key == Key.Enter) { LaunchRequested = Query.Text.Trim().Length > 0; e.Handled = true; }
        };
        Query.TextChanged += (_, _) => LaunchRequested = false;
        Deactivated += (_, _) => Dispatcher.BeginInvoke(() => { if (IsVisible && !IsActive && closeOnDeactivate()) CancelRequested?.Invoke(); });
        ContentRendered += (_, _) => Query.Focus();
    }

    internal void ShowAt(System.Drawing.Point point, bool requestForeground)
    {
        var handle = new WindowInteropHelper(this).EnsureHandle();
        var screen = System.Windows.Forms.Screen.FromPoint(point).WorkingArea;
        var transform = HwndSource.FromHwnd(handle).CompositionTarget.TransformFromDevice;
        var area = new Rect(screen.X, screen.Y, screen.Width, screen.Height); area.Transform(transform);
        Measure(new Size(Width, double.PositiveInfinity));
        Left = area.Left + (area.Width - Width) / 2; Top = area.Top + (area.Height - DesiredSize.Height) / 2;
        Show(); if (requestForeground) ForegroundActivation.Request(handle); Activate(); Query.Focus();
    }

    internal void UpdateProgress(TextKey text, double fraction)
    {
        Stage.Text = L.T(text);
        ((ScaleTransform)Progress.RenderTransform).ScaleX = Math.Clamp(fraction, 0, 1);
    }
}
