using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ReStart.App;

public partial class MainWindow
{
    private Border? _dragShortcutToast;
    private DispatcherTimer? _dragShortcutTimer;
    private int _dragShortcutGeneration;
    private static readonly TimeSpan DragShortcutHintDuration = TimeSpan.FromSeconds(5);

    private void ShowDragShortcutHint(string format)
    {
        HideDragShortcutHint(immediately: true);
        // Group drags do not use either modifier. Keep the toast specific to
        // operations where both shortcuts can change the drop behavior.
        if (!Document.Preferences.ShowDragShortcutHints || format == "ReStart.Group") return;
        if (_dragShortcutToast == null)
        {
            _dragShortcutToast = new()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                MaxWidth = 680,
                Margin = new(64, 0, 24, 48),
                Padding = new(20, 14, 20, 14),
                CornerRadius = new(8),
                BorderThickness = new(1),
                IsHitTestVisible = false,
                Focusable = false
            };
            _dragShortcutToast.SetResourceReference(Border.BackgroundProperty, "ControlBrush");
            _dragShortcutToast.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            AutomationProperties.SetLiveSetting(_dragShortcutToast, AutomationLiveSetting.Polite);
            Panel.SetZIndex(_dragShortcutToast, int.MaxValue);
            Root.Children.Add(_dragShortcutToast);
            _dragShortcutTimer = new(DispatcherPriority.Normal) { Interval = DragShortcutHintDuration };
            _dragShortcutTimer.Tick += (_, _) => HideDragShortcutHint();
        }
        var body = new StackPanel();
        void Line(string key, string description, bool last)
        {
            var text = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, last ? 0 : 7), FontSize = 14 };
            text.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            text.Inlines.Add(new Run(key) { FontWeight = FontWeights.SemiBold });
            text.Inlines.Add(new Run("  ·  " + description));
            body.Children.Add(text);
        }
        Line("Alt", L.T(K.DragHintIndependent), false);
        Line("Shift", L.T(K.DragHintReorder), true);
        _dragShortcutToast.Child = body;
        AutomationProperties.SetName(_dragShortcutToast, "Alt: " + L.T(K.DragHintIndependent) + ". Shift: " + L.T(K.DragHintReorder));
        _dragShortcutToast.Visibility = Visibility.Visible;
        if (MotionEnabled) _dragShortcutToast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
        _dragShortcutTimer!.Start();
        var peer = UIElementAutomationPeer.FromElement(_dragShortcutToast) ?? UIElementAutomationPeer.CreatePeerForElement(_dragShortcutToast);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void HideDragShortcutHint(bool immediately = false)
    {
        _dragShortcutTimer?.Stop();
        if (_dragShortcutToast == null) return;
        var generation = ++_dragShortcutGeneration;
        if (!immediately && MotionEnabled && _dragShortcutToast.Visibility == Visibility.Visible && IsVisible)
        {
            var fade = new DoubleAnimation(_dragShortcutToast.Opacity, 0, TimeSpan.FromMilliseconds(220));
            fade.Completed += (_, _) =>
            {
                if (generation != _dragShortcutGeneration) return;
                _dragShortcutToast.Visibility = Visibility.Collapsed;
                _dragShortcutToast.BeginAnimation(OpacityProperty, null);
            };
            _dragShortcutToast.BeginAnimation(OpacityProperty, fade);
        }
        else
        {
            _dragShortcutToast.BeginAnimation(OpacityProperty, null);
            _dragShortcutToast.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Shows the localized reference without changing the layout or stealing focus during a drag.</summary>
    /// <param name="owner">Start or its settings window, used for modal ownership and monitor placement.</param>
    internal void ShowKeyboardShortcuts(Window owner)
    {
        if (!_dragging) WithDialog(() => new KeyboardShortcutsWindow(owner).ShowDialog());
    }
}
