using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private async Task<int> VerifyDragHintsAsync(string directory)
    {
        var checks = 0;
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        Check(new Preferences().ShowDragShortcutHints && JsonSerializer.Deserialize<Preferences>("{}")!.ShowDragShortcutHints,
            "New and existing settings do not default drag shortcut hints on.");
        var before = JsonSerializer.Serialize(Document.Groups);
        var focus = Keyboard.FocusedElement;
        Document.Preferences.ShowDragShortcutHints = false;
        ShowDragShortcutHint("ReStart.Tile");
        Check(_dragShortcutToast?.Visibility != Visibility.Visible && _dragShortcutTimer?.IsEnabled != true, "Disabled hints still display or start a timer.");
        Document.Preferences.ShowDragShortcutHints = true;
        ShowDragShortcutHint("ReStart.Group");
        Check(_dragShortcutToast?.Visibility != Visibility.Visible, "A group drag shows irrelevant tile modifiers.");
        _dragging = true;
        ShowDragShortcutHint("ReStart.Selection"); UpdateLayout();
        var timer = _dragShortcutTimer;
        Check(_dragShortcutToast is { IsVisible: true, IsHitTestVisible: false, Focusable: false } && ReferenceEquals(focus, Keyboard.FocusedElement),
            "The drag toast is missing or takes pointer/keyboard input.");
        Check(_dragShortcutToast!.TranslatePoint(new(), Root).Y > Root.ActualHeight / 2
            && _dragShortcutToast.ActualWidth <= Root.ActualWidth - 64, "The toast is not within the lower viewport.");
        Check(!_dragShortcutToast.HasAnimatedProperties, "Disabled motion still animates the toast.");
        await Task.Delay(DragShortcutHintDuration + TimeSpan.FromMilliseconds(250));
        Check(_dragging && !_dragShortcutToast.IsVisible && timer?.IsEnabled == false, "The toast did not expire during an ongoing drag.");
        ShowDragShortcutHint("ReStart.Tile"); StopDragGhost();
        Check(!_dragShortcutToast.IsVisible && timer?.IsEnabled == false, "Cancelling a drag leaves the toast or timer alive.");
        ShowDragShortcutHint("ReStart.App"); Hide();
        Check(!_dragShortcutToast.IsVisible && timer?.IsEnabled == false, "Hiding Start leaves a stale hint behind.");
        _dragging = false; Present();
        foreach (var language in TextCatalog.SupportedLanguages)
            foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
            {
                Document.Preferences.Language = language; Document.Preferences.Theme = theme; ApplyAppearance();
                ShowDragShortcutHint("ReStart.Tile"); UpdateLayout();
                Check(AutomationProperties.GetName(_dragShortcutToast).Contains(L.T(K.DragHintIndependent))
                    && VisualChildren<TextBlock>(_dragShortcutToast).SelectMany(t => t.Inlines.OfType<System.Windows.Documents.Run>())
                        .Any(run => run.Text.Contains(L.T(K.DragHintReorder))), "Drag modifiers are not localized.");
                Check(Equals(_dragShortcutToast.Background, FindResource("ControlBrush"))
                    && Equals(_dragShortcutToast.BorderBrush, FindResource("BorderBrush")), "The toast does not follow the current palette.");
                if (language == "en") SaveScreenshot(Path.Combine(directory, "drag-hint-" + theme.ToString().ToLowerInvariant() + ".png"));
                HideDragShortcutHint();
                var help = new KeyboardShortcutsWindow(this) { Width = language == "en" ? 720 : 440 };
                try
                {
                    help.Show(); help.UpdateLayout();
                    Check(help.Title == L.T(K.KeyboardShortcuts) && help.Entries.Count == 22
                        && help.Entries.Any(e => e.Keys == "Shift" && e.Action == K.DragHintReorder)
                        && help.Entries.Any(e => e.Keys == "Alt" && e.Action == K.DragHintIndependent), "The shortcut reference omits localized drag or editing actions.");
                    Check(VisualChildren<TextBlock>(help).All(t => t.ActualWidth > 0 && t.ActualWidth <= help.ActualWidth), "The shortcut reference clips at a narrow width.");
                    if (language == "en") SaveFixtureElement(help, Path.Combine(directory, "keyboard-shortcuts-" + theme.ToString().ToLowerInvariant() + ".png"));
                }
                finally { help.Close(); }
            }
        Document.Preferences.Language = "en"; ApplyAppearance();
        Activate(); UpdateLayout();
        var opened = false;
        var closer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        closer.Tick += (_, _) =>
        {
            var help = Application.Current.Windows.OfType<KeyboardShortcutsWindow>().SingleOrDefault();
            if (help?.IsVisible != true) return;
            opened = true; help.Close();
        };
        closer.Start();
        var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, Environment.TickCount, Key.F1) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        OnPreviewKeyDown(this, key);
        closer.Stop();
        Check(opened && key.Handled && _dialogDepth == 0, "F1 does not open and close the owned shortcut reference cleanly.");
        Document.Preferences.Animate = true; _verifyMotion = true;
        ShowDragShortcutHint("ReStart.Tile"); await Task.Delay(170); HideDragShortcutHint(); await Task.Delay(80);
        Check(_dragShortcutToast.IsVisible && _dragShortcutToast.Opacity is > 0 and < 1, "The toast disappears abruptly instead of fading out.");
        ShowDragShortcutHint("ReStart.Selection"); await Task.Delay(250);
        Check(_dragShortcutToast.IsVisible && _dragShortcutToast.Opacity > .99, "An old fade completion hid a new drag toast.");
        HideDragShortcutHint(); await Task.Delay(300);
        Check(!_dragShortcutToast.IsVisible && _dragShortcutTimer?.IsEnabled == false, "Fade completion leaves a visible toast or running timer.");
        Document.Preferences.Animate = false; _verifyMotion = false;
        Check(before == JsonSerializer.Serialize(Document.Groups), "Shortcut help modified the tile layout.");
        return checks;
    }
}
