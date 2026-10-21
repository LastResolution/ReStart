using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;

namespace ReStart.App.Controls;

/// <summary>Provides a nonmodal, keyboard-accessible update notice using the shared application palette.</summary>
internal sealed class UpdateNotice : Border
{
    internal Button ViewReleaseButton { get; }
    internal Button LaterButton { get; }

    /// <summary>Creates an update notice without requesting focus or opening a browser.</summary>
    /// <param name="version">Available release version, displayed as ordinary text.</param>
    /// <param name="current">Currently installed version, displayed as ordinary text.</param>
    /// <param name="view">Action invoked only by an explicit View release activation.</param>
    /// <param name="dismiss">Action invoked when the user chooses Not now.</param>
    internal UpdateNotice(string version, string current, Action view, Action dismiss)
    {
        Width = 440; Padding = new(20); BorderThickness = new(1); CornerRadius = new(8);
        SetResourceReference(BackgroundProperty, "PanelBrush"); SetResourceReference(BorderBrushProperty, "BorderBrush");
        SetResourceReference(TextElement.ForegroundProperty, "TextBrush");
        var root = new StackPanel(); Child = root;
        var heading = new DockPanel { Margin = new(0, 0, 0, 12) };
        heading.Children.Add(new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Assets/ReStart.png")), Width = 28, Height = 28, Margin = new(0, 0, 12, 0) });
        heading.Children.Add(new TextBlock { Text = L.T(K.UpdateNoticeTitle), FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        root.Children.Add(heading);
        root.Children.Add(new TextBlock { Text = L.F(K.UpdateNoticeBody, version, current), TextWrapping = TextWrapping.Wrap, FontSize = 14 });
        var help = new TextBlock { Text = L.T(K.UpdateNoticeHelp), TextWrapping = TextWrapping.Wrap, Margin = new(0, 6, 0, 18), FontSize = 12 };
        help.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); root.Children.Add(help);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        LaterButton = new() { Content = L.T(K.UpdateNoticeLater), MinHeight = 34, Margin = new(0, 0, 8, 0) };
        ViewReleaseButton = new() { Content = L.T(K.UpdateOpenRelease), MinHeight = 34 };
        ViewReleaseButton.SetResourceReference(BackgroundProperty, "AccentBrush"); ViewReleaseButton.SetResourceReference(Control.ForegroundProperty, "AccentTextBrush");
        AutomationProperties.SetName(this, L.T(K.UpdateNoticeTitle)); AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        LaterButton.Click += (_, _) => dismiss(); ViewReleaseButton.Click += (_, _) => view();
        actions.Children.Add(LaterButton); actions.Children.Add(ViewReleaseButton); root.Children.Add(actions);
    }
}
