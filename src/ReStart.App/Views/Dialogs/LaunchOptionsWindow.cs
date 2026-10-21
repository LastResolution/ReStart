using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App;

/// <summary>Edits a tile's launch preferences transactionally; Windows retains responsibility for elevation consent.</summary>
internal sealed class LaunchOptionsWindow : Window
{
    internal LaunchOptionsWindow(Tile tile, bool canConfigure, bool canElevate)
    {
        Style = (Style)FindResource(typeof(Window));
        ThemeWindow.Attach(this);
        Title = L.T(K.LaunchOptions); Width = 620; Height = 480; MinWidth = 480; MinHeight = 430;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var root = new DockPanel { Margin = new(24) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = L.T(K.CommonCancel), IsCancel = true, MinWidth = 90 };
        var save = new Button { Content = L.T(K.CommonSave), IsDefault = true, MinWidth = 90, Margin = new(10, 0, 0, 0) };
        save.SetResourceReference(BackgroundProperty, "AccentBrush"); save.SetResourceReference(ForegroundProperty, "AccentTextBrush");
        footer.Children.Add(cancel); footer.Children.Add(save); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var body = new StackPanel(); root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        body.Children.Add(new TextBlock { Text = tile.Label, FontSize = 20, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(0, 0, 0, 14) });
        var enabled = new CheckBox { Content = L.T(K.UseLaunchOptions), IsChecked = canConfigure && tile.UseLaunchOptions, IsEnabled = canConfigure };
        AutomationProperties.SetName(enabled, L.T(K.UseLaunchOptions)); body.Children.Add(enabled);
        TextBox Field(K key, string value)
        {
            body.Children.Add(new TextBlock { Text = L.T(key), Margin = new(0, 10, 0, 5) });
            var field = new TextBox { Text = value, MaxLength = 32760 };
            AutomationProperties.SetName(field, L.T(key)); body.Children.Add(field); return field;
        }
        var arguments = Field(K.LaunchArguments, tile.LaunchArguments);
        var directory = Field(K.WorkingDirectory, tile.WorkingDirectory);
        var elevated = new CheckBox { Content = L.T(K.RunAsAdministrator), IsChecked = canElevate && tile.RunAsAdministrator };
        AutomationProperties.SetName(elevated, L.T(K.RunAsAdministrator)); body.Children.Add(elevated);
        body.Children.Add(new TextBlock { Text = L.T(canConfigure ? K.LaunchOptionsHelp : K.LaunchOptionsUnavailable), TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new(0, 8, 0, 8) });
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap }; body.Children.Add(feedback);
        void Refresh() { arguments.IsEnabled = directory.IsEnabled = enabled.IsChecked == true; elevated.IsEnabled = enabled.IsChecked == true && canElevate; }
        enabled.Checked += (_, _) => Refresh(); enabled.Unchecked += (_, _) => Refresh(); Refresh();
        save.Click += (_, _) =>
        {
            var path = Environment.ExpandEnvironmentVariables(directory.Text.Trim());
            if (enabled.IsChecked == true && path.Length > 0 && !Directory.Exists(path))
            { feedback.Text = L.T(K.ErrorWorkingDirectory); directory.Focus(); return; }
            tile.UseLaunchOptions = enabled.IsChecked == true;
            tile.LaunchArguments = arguments.Text; tile.WorkingDirectory = directory.Text.Trim(); tile.RunAsAdministrator = elevated.IsChecked == true;
            DialogResult = true;
        };
        Content = root;
    }
}
