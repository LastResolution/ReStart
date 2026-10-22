using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Microsoft.Web.WebView2.Core;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

/// <summary>Previews the complete, whitelist-only report before an explicit clipboard action.</summary>
internal sealed class DiagnosticsWindow : Window
{
    internal TextBox Report { get; }
    internal Button Copy { get; }
    /// <summary>Creates a themed preview; creating/showing it never changes the clipboard.</summary>
    /// <param name="owner">Settings window used for ownership, placement and theme.</param>
    /// <param name="main">Launcher supplying versions, theme and classified check status, without its layout contents.</param>
    /// <param name="copy">Optional test sink invoked only by the copy button; null uses the system clipboard.</param>
    internal DiagnosticsWindow(Window owner, MainWindow main, Action<string>? copy = null)
    {
        Owner = owner; Style = (Style)FindResource(typeof(Window)); ThemeWindow.Attach(this);
        Title = L.T(K.DiagnosticsTitle); Width = 650; Height = 610; MinWidth = 420; MinHeight = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var root = new DockPanel { Margin = new(24) };
        var footer = new DockPanel { Margin = new(0, 14, 0, 0) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var feedback = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 12, 0), TextWrapping = TextWrapping.Wrap };
        Copy = new() { Content = L.T(K.DiagnosticsCopy), MinWidth = 110 };
        var close = new Button { Content = L.T(K.CommonClose), IsCancel = true, MinWidth = 90, Margin = new(10, 0, 0, 0) };
        close.Click += (_, _) => Close();
        Copy.Click += (_, _) =>
        {
            try { (copy ?? Clipboard.SetText)(Report!.Text); feedback.Text = L.T(K.DiagnosticsCopied); }
            catch (ExternalException) { feedback.Text = L.T(K.DiagnosticsClipboardBusy); }
        };
        actions.Children.Add(Copy); actions.Children.Add(close); DockPanel.SetDock(actions, Dock.Right);
        footer.Children.Add(actions); footer.Children.Add(feedback); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var help = new TextBlock { Text = L.T(K.DiagnosticsHelp), TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 16) };
        DockPanel.SetDock(help, Dock.Top); root.Children.Add(help);
        string? webView = null;
        try { webView = CoreWebView2Environment.GetAvailableBrowserVersionString(); }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or COMException) { }
        DiagnosticCode[] codes; lock (PortableUpdates.RecentCodes) codes = PortableUpdates.RecentCodes.ToArray();
        Report = new()
        {
            Text = DiagnosticReport.Create(AppBuildInfo.Build, AppBuildInfo.Runtime, Environment.OSVersion.Version, Environment.Version,
                webView, L.Language, ThemeWindow.IsLight, SystemParameters.HighContrast, main.Document.Version, main.LastUpdateCheck?.Status, codes),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new("Consolas"),
            FontSize = 12,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        AutomationProperties.SetName(Report, L.T(K.DiagnosticsTitle));
        root.Children.Add(Report); Content = root;
    }
}
