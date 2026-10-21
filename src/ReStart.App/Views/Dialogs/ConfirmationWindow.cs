using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace ReStart.App;

/// <summary>Uses the shared palette for application-owned confirmation, retaining No as the keyboard default.</summary>
internal sealed class ConfirmationWindow : Window
{
    internal Button AcceptButton { get; }
    internal Button CancelButton { get; }

    /// <summary>Creates a read-only prompt; accepting it does not itself execute or mutate the requested operation.</summary>
    /// <param name="message">Localized question, including any user-supplied name as ordinary text.</param>
    internal ConfirmationWindow(string message)
    {
        Style = (Style)FindResource(typeof(Window));
        ThemeWindow.Attach(this);
        Title = L.T(K.CommonConfirm); Width = 520; SizeToContent = SizeToContent.Height;
        MaxHeight = Math.Max(180, Math.Min(600, SystemParameters.WorkArea.Height - 48));
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false; UseLayoutRounding = true;
        var root = new DockPanel { Margin = new(24) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 20, 0, 0) };
        CancelButton = new() { Content = L.T(K.CommonNo), IsCancel = true, IsDefault = true, MinWidth = 100 };
        AcceptButton = new() { Content = L.T(K.CommonYes), MinWidth = 100, Margin = new(10, 0, 0, 0) };
        AutomationProperties.SetName(CancelButton, L.T(K.CommonNo)); AutomationProperties.SetName(AcceptButton, L.T(K.CommonYes));
        AcceptButton.SetResourceReference(BackgroundProperty, "AccentBrush"); AcceptButton.SetResourceReference(ForegroundProperty, "AccentTextBrush");
        CancelButton.Click += (_, _) => DialogResult = false;
        AcceptButton.Click += (_, _) => DialogResult = true;
        footer.Children.Add(CancelButton); footer.Children.Add(AcceptButton); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = L.T(K.CommonConfirm), FontSize = 20, Margin = new(0, 0, 0, 14) });
        body.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        root.Children.Add(new ScrollViewer { Content = body, MaxHeight = Math.Max(60, MaxHeight - 130), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Content = root;
        Loaded += (_, _) => CancelButton.Focus();
    }

    /// <summary>Shows an owned prompt and treats No, Escape and window closure as cancellation.</summary>
    /// <param name="owner">Live owner whose theme resources and monitor positioning are used.</param>
    /// <param name="message">Localized question; no action runs until the caller receives true.</param>
    /// <returns>True only after explicit acceptance.</returns>
    internal static bool Ask(Window owner, string message)
    {
        var window = new ConfirmationWindow(message) { Owner = owner, MaxWidth = Math.Max(280, owner.ActualWidth - 32) };
        return window.ShowDialog() == true;
    }
}
