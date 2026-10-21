using System.Windows.Media;

namespace ReStart.App;

public partial class MainWindow
{
    internal static SolidColorBrush ReadableInk(Color background)
    {
        static double Linear(byte value) => value / 255d <= .04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + .055) / 1.055, 2.4);
        var luminance = .2126 * Linear(background.R) + .7152 * Linear(background.G) + .0722 * Linear(background.B);
        return luminance > .179 ? Brushes.Black : Brushes.White;
    }
    private static Color Mix(Color background, Color ink, double amount) => Color.FromRgb(
        (byte)Math.Round(background.R * (1 - amount) + ink.R * amount), (byte)Math.Round(background.G * (1 - amount) + ink.G * amount), (byte)Math.Round(background.B * (1 - amount) + ink.B * amount));
    internal static double Contrast(Color first, Color second)
    {
        static double Luma(Color c)
        {
            static double Linear(byte value) => value / 255d <= .04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + .055) / 1.055, 2.4);
            return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        }
        var a = Luma(first); var b = Luma(second); return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
    private static Color VisibleAccent(Color accent, Color background, Color foreground)
        => EnsureContrast(accent, background, foreground, 3);
    private static Color EnsureContrast(Color accent, Color background, Color foreground, double ratio)
    {
        var visible = accent;
        for (var step = 1; step <= 10 && Contrast(visible, background) < ratio; step++) visible = Mix(accent, foreground, step / 10d);
        return visible;
    }
}
