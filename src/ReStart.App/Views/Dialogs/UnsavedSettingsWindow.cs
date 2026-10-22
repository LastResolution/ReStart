using System.Windows;
using ReStart.Core;
using System.Windows.Automation;
using System.Windows.Controls;

namespace ReStart.App;

internal enum SettingsCloseChoice { KeepEditing, Discard, Save }

/// <summary>Offers an explicit save/discard/continue choice using the current preview language and palette.</summary>
internal sealed class UnsavedSettingsWindow : Window
{
    internal SettingsCloseChoice Choice { get; private set; }
    internal Button KeepButton { get; }
    internal Button DiscardButton { get; }
    internal Button SaveButton { get; }
    internal UnsavedSettingsWindow()
    {
        Style = (Style)FindResource(typeof(Window)); ThemeWindow.Attach(this);
        Title = L.T(K.SettingsUnsavedTitle); Width = 650; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var root = new StackPanel { Margin = new(24) };
        root.Children.Add(new TextBlock { Text = Title, FontSize = 20, TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 14) });
        root.Children.Add(new TextBlock { Text = L.T(K.SettingsUnsavedBody), TextWrapping = TextWrapping.Wrap });
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 20, 0, 0) };
        Button Action(TextKey key, SettingsCloseChoice choice)
        {
            var button = new Button { Content = L.T(key), MinHeight = 36, Margin = new(8, 6, 0, 0) };
            AutomationProperties.SetName(button, L.T(key));
            button.Click += (_, _) => { Choice = choice; DialogResult = choice != SettingsCloseChoice.KeepEditing; };
            actions.Children.Add(button); return button;
        }
        KeepButton = Action(K.SettingsKeepEditing, SettingsCloseChoice.KeepEditing); KeepButton.IsDefault = KeepButton.IsCancel = true;
        DiscardButton = Action(K.SettingsDiscardClose, SettingsCloseChoice.Discard);
        SaveButton = Action(K.SettingsSaveClose, SettingsCloseChoice.Save);
        SaveButton.SetResourceReference(BackgroundProperty, "AccentBrush"); SaveButton.SetResourceReference(ForegroundProperty, "AccentTextBrush");
        root.Children.Add(actions); Content = root; Loaded += (_, _) => KeepButton.Focus();
    }
    /// <summary>Asks before discarding a draft; Escape, the window close control and the default action keep editing.</summary>
    /// <param name="owner">Settings editor whose current preview palette and language are used.</param>
    /// <returns>The explicit decision; closing without a decision returns KeepEditing.</returns>
    internal static SettingsCloseChoice Ask(Window owner)
    {
        var dialog = new UnsavedSettingsWindow { Owner = owner, MaxWidth = Math.Max(300, owner.ActualWidth - 32) };
        dialog.ShowDialog(); return dialog.Choice;
    }
}
