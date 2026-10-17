using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ReStart.Core;

namespace ReStart.App.Controls;

/// <summary>Edits a group name in place without opening a modal window or intercepting header dragging.</summary>
internal sealed class GroupHeader : Grid
{
    private readonly TextBox _editor = new() { Visibility = Visibility.Collapsed, MaxLength = 256, VerticalContentAlignment = VerticalAlignment.Center, Padding = new(4, 0, 4, 0) };
    private string _name;
    private readonly TextKey _placeholder;
    internal Button Handle { get; } = new() { HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new(0, 6, 0, 6), FontSize = 15 };
    internal Button EditButton { get; } = new()
    {
        Content = "\uE70F",
        FontFamily = new("Segoe MDL2 Assets"),
        FontSize = 12,
        Width = 26,
        Height = 28,
        Padding = new(0),
        Opacity = .6,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
    internal bool IsEditing { get; private set; }
    internal event Action? EditStarted;
    internal event Action<string?, bool>? EditEnded;

    internal GroupHeader(string name, TextKey placeholder = K.NameGroup, TextKey prompt = K.PromptGroupName)
    {
        Height = ReStart.Core.GroupLayout.HeaderHeight;
        _name = name;
        _placeholder = placeholder;
        ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new() { Width = new GridLength(28) });
        Handle.Content = DisplayName;
        SetColumn(EditButton, 1); SetColumnSpan(_editor, 2);
        EditButton.ToolTip = L.T(placeholder == K.NameGroup ? K.RenameGroup : K.RenameTile);
        System.Windows.Automation.AutomationProperties.SetName(EditButton, (string)EditButton.ToolTip);
        Children.Add(Handle);
        Children.Add(EditButton);
        Children.Add(_editor);
        EditButton.Click += (_, e) => { BeginEdit(); e.Handled = true; };
        Handle.Click += (_, _) => { if (_name.Length == 0) BeginEdit(); };
        Handle.IsMouseDirectlyOverChanged += (_, _) => { if (!IsEditing) Handle.Content = DisplayName; };
        Handle.IsKeyboardFocusWithinChanged += (_, _) => { if (!IsEditing) Handle.Content = DisplayName; };
        Handle.MouseDoubleClick += (_, e) => { BeginEdit(); e.Handled = true; };
        Handle.PreviewKeyDown += (_, e) => { if (e.Key is Key.Enter or Key.F2) { BeginEdit(); e.Handled = true; } };
        _editor.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Escape) { EndEdit(e.Key == Key.Enter, true); e.Handled = true; Handle.Focus(); }
        };
        _editor.LostKeyboardFocus += (_, _) => EndEdit(true);
        System.Windows.Automation.AutomationProperties.SetName(_editor, L.T(prompt));
    }

    private string DisplayName => string.IsNullOrEmpty(_name)
        ? _placeholder != K.NameGroup || Handle.IsMouseOver || Handle.IsKeyboardFocusWithin ? L.T(_placeholder) : "" : _name;

    internal void BeginEdit()
    {
        if (IsEditing) return;
        IsEditing = true;
        EditStarted?.Invoke();
        _editor.Text = _name;
        Handle.Visibility = Visibility.Collapsed;
        EditButton.Visibility = Visibility.Collapsed;
        _editor.Visibility = Visibility.Visible;
        _editor.Focus();
        _editor.SelectAll();
    }

    internal void EndEdit(bool commit, bool returnFocus = false)
    {
        if (!IsEditing) return;
        IsEditing = false;
        if (commit) _name = _editor.Text.Trim();
        _editor.Visibility = Visibility.Collapsed;
        Handle.Content = DisplayName;
        Handle.Visibility = Visibility.Visible;
        EditButton.Visibility = Visibility.Visible;
        EditEnded?.Invoke(commit ? _name : null, returnFocus);
    }
}
