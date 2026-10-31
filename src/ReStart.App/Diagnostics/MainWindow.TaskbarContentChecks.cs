using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using ReStart.Core;
using ReStart.App.Services;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Checks power controls in both rail states, all edge reservations and four DPI scales without changing native bounds or using the real taskbar.</summary>
    /// <param name="directory">Owned fixture directory for dark/light verification images.</param>
    /// <returns>Number of geometry and native-window preservation assertions.</returns>
    private int VerifyTaskbarContentInsets(string directory)
    {
        if (!_preview) throw new InvalidOperationException("Taskbar content verification requires an owned preview.");
        var checks = 0;
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        UpdateLayout();
        var handle = new WindowInteropHelper(this).Handle;
        Native.GetWindowRect(handle, out var original);
        var originalRoot = Root.RenderSize;
        var originalWallpaper = Wallpaper.RenderSize;
        var area = new System.Drawing.Rectangle(-1280, -800, 1280, 800);
        var oldTheme = Document.Preferences.Theme; var oldLanguage = Document.Preferences.Language;
        var language = L.Language;
        var band = new Border
        {
            Height = 48,
            Background = new SolidColorBrush(Color.FromRgb(50, 57, 70)),
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
            Child = new TextBlock { Text = "Taskbar test area", Margin = new(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White }
        };
        try
        {
            foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
                foreach (var edge in new[] { 0, 1, 2, 3 })
                {
                    var safe = edge switch
                    {
                        0 => new System.Drawing.Rectangle(-1232, -800, 1232, 800),
                        1 => new System.Drawing.Rectangle(-1280, -752, 1280, 752),
                        2 => new System.Drawing.Rectangle(-1280, -800, 1232, 800),
                        _ => new System.Drawing.Rectangle(-1280, -800, 1280, 752)
                    };
                    ApplyTaskbarContentInsets(area, safe, new(scale, scale)); UpdateLayout();
                    var origin = ContentArea.TranslatePoint(new(), Root);
                    var safeRect = new Rect(origin.X, origin.Y,
                        ContentArea.ActualWidth, ContentArea.ActualHeight);
                    foreach (var expanded in new[] { false, true })
                    {
                        SetRailOpen(expanded); UpdateLayout();
                        var power = expanded ? ExpandedPowerNav : CollapsedPowerNav;
                        var point = power.TranslatePoint(new(), Root);
                        Check(point.X >= safeRect.Left - .1 && point.Y >= safeRect.Top - .1
                            && point.X + power.ActualWidth <= safeRect.Right + .1 && point.Y + power.ActualHeight <= safeRect.Bottom + .1,
                            $"A power button is covered by the taskbar content reservation (edge {edge}, scale {scale}, expanded {expanded}, point {point}, size {power.RenderSize}, safe {safeRect}).");
                    }
                    Native.GetWindowRect(handle, out var after);
                    Check(after.Left == original.Left && after.Top == original.Top && after.Right == original.Right && after.Bottom == original.Bottom
                        && Root.RenderSize == originalRoot && Wallpaper.RenderSize == originalWallpaper && Tint.RenderSize == originalRoot,
                        "Taskbar content reservation resized the native launcher or wallpaper.");
                }
            ApplyTaskbarContentInsets(area, new(-1280, -800, 1280, 752), new(1, 1));
            Root.Children.Add(band);
            foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
            {
                Document.Preferences.Theme = theme; Document.Preferences.Language = "en"; ApplyAppearance();
                SetRailOpen(true); UpdateLayout();
                SaveStartupScreenshot(this, Path.Combine(directory, $"taskbar-content-{theme}.png"));
            }
            ClearTaskbarContentInsets(); UpdateLayout();
            Check(ContentArea.Margin == new Thickness(0) && ContentArea.RenderSize == originalRoot,
                "Releasing taskbar integration left a content reservation behind.");
            return checks;
        }
        finally
        {
            Root.Children.Remove(band); ClearTaskbarContentInsets(); SetRailOpen(false);
            Document.Preferences.Theme = oldTheme; Document.Preferences.Language = oldLanguage; ApplyAppearance(); L.SetLanguage(language);
        }
    }
}
