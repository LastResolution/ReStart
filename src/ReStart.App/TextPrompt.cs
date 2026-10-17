using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ReStart.App;

internal static class TextPrompt
{
    public static string? Show(Window owner, string title, string value)
    {
        var window = new Window { Title = title, Width = 440, Height = 215, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = owner, ShowInTaskbar = false };
        var panel = new StackPanel { Margin = new(24) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 19, Margin = new(0, 0, 0, 14) });
        var input = new TextBox { Text = value, MaxLength = 200 };
        System.Windows.Automation.AutomationProperties.SetName(input, title);
        panel.Children.Add(input);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 16, 0, 0) };
        var cancel = new Button { Content = "キャンセル", IsCancel = true, MinWidth = 100 };
        var save = new Button { Content = "保存", IsDefault = true, MinWidth = 90, Margin = new(8, 0, 0, 0) };
        save.SetResourceReference(Control.BackgroundProperty, "AccentBrush");
        save.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(input.Text)) window.DialogResult = true; };
        buttons.Children.Add(cancel); buttons.Children.Add(save);
        panel.Children.Add(buttons); window.Content = panel;
        window.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return window.ShowDialog() == true ? input.Text.Trim() : null;
    }
}
