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
    /// <param name="prepared">True presents an already downloaded package with an explicit restart action.</param>
    /// <param name="compact">True retains a small persistent ready indicator without a dismiss action.</param>
    internal UpdateNotice(string version, string current, Action view, Action dismiss, bool prepared = false, bool compact = false)
    {
        Width = 440; Padding = new(compact ? 12 : 20); BorderThickness = new(1); CornerRadius = new(8);
        SetResourceReference(BackgroundProperty, "PanelBrush"); SetResourceReference(BorderBrushProperty, "BorderBrush");
        SetResourceReference(TextElement.ForegroundProperty, "TextBrush");
        var root = new StackPanel(); Child = root;
        var heading = new DockPanel { Margin = new(0, 0, 0, 12) };
        heading.Children.Add(new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Assets/ReStart.png")), Width = 28, Height = 28, Margin = new(0, 0, 12, 0) });
        heading.Children.Add(new TextBlock { Text = L.T(prepared ? K.UpdateReadyTitle : K.UpdateNoticeTitle), FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        root.Children.Add(heading);
        root.Children.Add(new TextBlock { Text = prepared ? L.F(K.UpdateReadyBody, version) : L.F(K.UpdateNoticeBody, version, current), TextWrapping = TextWrapping.Wrap, FontSize = 14 });
        var help = new TextBlock { Text = L.T(prepared ? K.UpdateReadyHelp : K.UpdateNoticeHelp), TextWrapping = TextWrapping.Wrap, Margin = new(0, 6, 0, 18), FontSize = 12 };
        help.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); help.Visibility = compact ? Visibility.Collapsed : Visibility.Visible; root.Children.Add(help);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        LaterButton = new() { Content = L.T(K.UpdateNoticeLater), MinHeight = 34, Margin = new(0, 0, 8, 0) };
        ViewReleaseButton = new() { Content = L.T(prepared ? K.UpdateRestart : K.UpdateOpenRelease), MinHeight = 34 };
        ViewReleaseButton.SetResourceReference(BackgroundProperty, "AccentBrush"); ViewReleaseButton.SetResourceReference(Control.ForegroundProperty, "AccentTextBrush");
        AutomationProperties.SetName(this, L.T(prepared ? K.UpdateReadyTitle : K.UpdateNoticeTitle)); AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        LaterButton.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        LaterButton.Click += (_, _) => dismiss(); ViewReleaseButton.Click += (_, _) => view();
        if (compact)
        {
            var reminder = new DockPanel(); ViewReleaseButton.Margin = new(12, 0, 0, 0);
            DockPanel.SetDock(ViewReleaseButton, Dock.Right); reminder.Children.Add(ViewReleaseButton);
            reminder.Children.Add(new TextBlock { Text = L.F(K.UpdateReadyBody, version), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 });
            Child = reminder;
        }
        else { actions.Children.Add(LaterButton); actions.Children.Add(ViewReleaseButton); root.Children.Add(actions); }
    }
}
