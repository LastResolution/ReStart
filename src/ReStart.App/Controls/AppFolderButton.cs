using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ReStart.App.Controls;

/// <summary>Provides an all-apps folder row with keyboard and UI Automation expand/collapse semantics.</summary>
internal sealed class AppFolderButton : Button
{
    private readonly Action<bool> _changed;
    private readonly TextBlock _arrow = new() { FontFamily = new("Segoe MDL2 Assets"), FontSize = 12, Width = 20, VerticalAlignment = VerticalAlignment.Center };
    internal string FolderKey { get; }
    internal bool Expanded { get; private set; }

    internal AppFolderButton(string name, string key, bool expanded, ImageSource? icon, Action<bool> changed)
    {
        FolderKey = key;
        Expanded = expanded;
        _changed = changed;
        SetResourceReference(StyleProperty, typeof(Button));
        Height = 44;
        Padding = new(4, 3, 4, 3);
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        ToolTip = name;
        AutomationProperties.SetName(this, name);
        var row = new DockPanel();
        var box = new Border { Width = 32, Height = 32, Margin = new(0, 0, 12, 0) };
        box.Background = Brushes.Transparent;
        box.Child = icon != null ? new Image { Source = icon, Width = 25, Height = 25 } : new TextBlock { Text = "\uE8B7", FontFamily = new("Segoe MDL2 Assets"), FontSize = 23, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        DockPanel.SetDock(box, Dock.Left);
        DockPanel.SetDock(_arrow, Dock.Right);
        row.Children.Add(box);
        row.Children.Add(_arrow);
        row.Children.Add(new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
        Content = row;
        SetArrow();
        Click += (_, _) => SetExpanded(!Expanded);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Right && !Expanded || e.Key == Key.Left && Expanded)
            { SetExpanded(!Expanded); e.Handled = true; }
        };
    }

    internal void SetExpanded(bool value)
    {
        if (value == Expanded) return;
        var previous = Expanded;
        Expanded = value;
        SetArrow();
        if (UIElementAutomationPeer.FromElement(this) is AppFolderPeer peer)
            peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                previous ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed, value ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
        _changed(value);
    }

    private void SetArrow() => _arrow.Text = Expanded ? "\uE70D" : "\uE76C";
    protected override AutomationPeer OnCreateAutomationPeer() => new AppFolderPeer(this);

    private sealed class AppFolderPeer(AppFolderButton owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TreeItem;
        protected override string GetLocalizedControlTypeCore() => L.T(K.TileFolder);
        public ExpandCollapseState ExpandCollapseState => owner.Expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
        public void Collapse() => Set(false);
        public void Expand() => Set(true);
        private void Set(bool value) => owner.Dispatcher.Invoke(() =>
        {
            if (!owner.IsEnabled) throw new ElementNotEnabledException();
            owner.SetExpanded(value);
        });
    }
}
