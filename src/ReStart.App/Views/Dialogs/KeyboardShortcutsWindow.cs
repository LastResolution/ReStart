using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ReStart.Core;

namespace ReStart.App;

/// <summary>Provides a read-only, localized reference for implemented launcher and editing commands.</summary>
internal sealed class KeyboardShortcutsWindow : Window
{
    internal readonly List<(string Keys, TextKey Action)> Entries = [];

    /// <summary>Builds the themed reference; opening it performs no commands or preference changes.</summary>
    /// <param name="owner">Visible owner supplying monitor placement and modal lifetime.</param>
    internal KeyboardShortcutsWindow(Window owner)
    {
        Owner = owner; Style = (Style)FindResource(typeof(Window)); ThemeWindow.Attach(this);
        Title = L.T(K.KeyboardShortcuts); Width = 720; Height = 690; MinWidth = 440; MinHeight = 350;
        MaxWidth = Math.Max(440, SystemParameters.WorkArea.Width - 32);
        MaxHeight = Math.Max(350, SystemParameters.WorkArea.Height - 32);
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false; UseLayoutRounding = true;
        var root = new DockPanel { Margin = new(24) };
        var close = new Button { Content = L.T(K.CommonClose), IsCancel = true, IsDefault = true, MinWidth = 100, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 16, 0, 0) };
        close.SetResourceReference(BackgroundProperty, "TileBrush");
        close.SetResourceReference(BorderBrushProperty, "BorderBrush");
        close.BorderThickness = new(1);
        close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Bottom); root.Children.Add(close);
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = Title, FontSize = 26, Margin = new(0, 0, 0, 16) });
        void Section(TextKey title)
        {
            body.Children.Add(new TextBlock { Text = L.T(title), TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, FontSize = 17, Margin = new(0, 18, 0, 10) });
        }
        void Row(string keys, TextKey action)
        {
            Entries.Add((keys, action));
            var row = new Grid { Margin = new(0, 0, 0, 10) };
            row.ColumnDefinitions.Add(new() { Width = new GridLength(210) }); row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            var chord = new WrapPanel { Margin = new(0, 0, 20, 0) };
            AutomationProperties.SetName(chord, keys);
            foreach (var token in System.Text.RegularExpressions.Regex.Split(keys, @"(\+| / | )").Where(t => t.Length > 0))
            {
                if (token is "+" or " / " or " ")
                {
                    var separator = new TextBlock { Text = token, VerticalAlignment = VerticalAlignment.Center, Margin = new(1, 0, 1, 4), FontSize = 12 };
                    separator.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); chord.Children.Add(separator);
                }
                else
                {
                    var cap = new Border
                    {
                        CornerRadius = new(4),
                        BorderThickness = new(1, 1, 1, 2),
                        Padding = new(6, 3, 6, 3),
                        Margin = new(0, 0, 0, 4),
                        Child = new TextBlock { Text = token, FontSize = 12, FontWeight = FontWeights.SemiBold }
                    };
                    cap.SetResourceReference(Border.BackgroundProperty, "ControlBrush"); cap.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                    chord.Children.Add(cap);
                }
            }
            var meaning = new TextBlock { Text = L.T(action), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(row, keys + ": " + L.T(action));
            row.Children.Add(chord); Grid.SetColumn(meaning, 1); row.Children.Add(meaning); body.Children.Add(row);
        }
        Section(K.ShortcutsGeneral);
        Row("Win / Ctrl+Alt+Space", K.ShortcutOpen);
        Row("Ctrl+F", K.SearchTitle);
        Row("F1", K.KeyboardShortcuts);
        Row("Esc", K.ShortcutEscape);
        Row("Ctrl+E", K.EditLayout);
        Row("Ctrl+Shift+L", K.LockLayout);
        Row("Ctrl+Z", K.UndoLayout);
        Row("Ctrl+Y / Ctrl+Shift+Z", K.RedoLayout);
        Section(K.ShortcutsDragging);
        Row("Alt", K.DragHintIndependent);
        Row("Shift", K.DragHintReorder);
        Section(K.ShortcutsSelection);
        Row("↑ ↓ ← →", K.ShortcutFocus);
        Row("Space", K.ShortcutSelect);
        Row("Ctrl+A", K.SelectAllTiles);
        Row("Ctrl+↑ ↓ ← →", K.ShortcutMoveTiles);
        Row("Ctrl+Shift+↑ ↓ ← →", K.ShortcutMoveGroup);
        Row("Alt+1 / 2 / 3 / 4", K.ShortcutSize);
        Row("Ctrl+G", K.ShortcutNewGroup);
        Row("Shift+F10", K.ShortcutTileMenu);
        Row("Delete", K.UnpinSelectedTiles);
        Row("Esc", K.ShortcutClearSelection);
        Section(K.StudioTitle);
        Row("Ctrl+Enter", K.StudioPreview);
        Row("Ctrl+S", K.StudioSaveLibrary);
        root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new(0, 0, 14, 0) });
        Content = root;
    }
}
