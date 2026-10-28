using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ReStart.App.Tablet.Design;

/// <summary>
/// The one look of the tablet chrome (shelf, Control Center, switcher, home bar, divider), taken
/// from Home's own theme resources so every surface reads as part of the same tile board:
/// flat, square, accent-aware Metro tiles on Home's panel material, Segoe UI, an 8 DIP grid.
/// Colors are read when a surface is shown, so theme and accent changes follow Home.
/// </summary>
internal static class TabletStyle
{
    // ---- grid (DIP) -----------------------------------------------------------------
    /// <summary>Home's tile gap; every tablet surface spaces its modules with it.</summary>
    internal const double Gap = 8;
    /// <summary>Outer margin of panels and the screen edge inset.</summary>
    internal const double Gutter = 16;
    /// <summary>Metro tiles are square; only the home-bar pill and running marks are rounded.</summary>
    internal const double TileRadius = 0;
    /// <summary>Small controls (slider knobs, marks): Windows 10's 2 DIP.</summary>
    internal const double ControlRadius = 2;

    // ---- shelf (the dock) -----------------------------------------------------------
    /// <summary>Height of the shelf above the home bar.</summary>
    internal const double ShelfHeight = 68;
    /// <summary>Square touch cell of one shelf app.</summary>
    internal const double ShelfCell = 60;
    /// <summary>App icon inside a shelf cell.</summary>
    internal const double ShelfIcon = 38;

    // ---- type ramp (Home uses Segoe UI) ---------------------------------------------
    internal static readonly FontFamily Text = new("Segoe UI, Yu Gothic UI");
    internal static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    /// <summary>Tile labels (Home tiles use 12-13).</summary>
    internal const double Caption = 12.5;
    internal const double Body = 14;
    internal const double Subtitle = 15;
    /// <summary>Big tile values (battery %, refresh rate): Segoe UI Light like Metro live tiles.</summary>
    internal const double Hero = 30;

    // ---- palette (from Home's resources) ----------------------------------------------
    /// <summary>The user's accent, as Home's tiles and selection use it.</summary>
    internal static Color Accent => Resource("AccentBrush", Color.FromRgb(0x00, 0x78, 0xD7));
    /// <summary>Ink on the accent.</summary>
    internal static Color OnAccent => Resource("AccentTextBrush", Colors.White);
    /// <summary>Primary ink.</summary>
    internal static Color Ink => Resource("TextBrush", Colors.White);
    /// <summary>Secondary ink.</summary>
    internal static Color MutedInk => Resource("MutedBrush", Color.FromRgb(0xC3, 0xC3, 0xC3));
    /// <summary>Home's panel material (rail flyouts, edit bar), opaque: the shelf and the switcher's chrome.</summary>
    internal static Color Panel => Opaque(Resource("PanelBrush", Color.FromRgb(0x23, 0x23, 0x23)));
    /// <summary>Whether the theme is dark (judged from Home's panel, so it follows ReStart's appearance setting).</summary>
    internal static bool Dark => Luminance(Panel) < 0.5;
    /// <summary>An unlit tile over a frosted backdrop (Control Center modules).</summary>
    internal static Color Tile => Dark ? Color.FromArgb(0xCC, 0x2B, 0x2B, 0x2B) : Color.FromArgb(0xCC, 0xF2, 0xF2, 0xF2);
    /// <summary>A track or an "off" control on a tile.</summary>
    internal static Color Track => Dark ? Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x24, 0x00, 0x00, 0x00);
    /// <summary>One-pixel separators and tile outlines.</summary>
    internal static Color Hairline => WithAlpha(Ink, 0x22);
    /// <summary>The press highlight on a shelf cell (Windows 10 taskbar style).</summary>
    internal static Color Pressed => WithAlpha(Ink, 0x26);
    /// <summary>Scrim over a frozen backdrop, tinted like Home's own tint layer.</summary>
    internal static Color Scrim => Dark ? Color.FromArgb(0x8C, 0x10, 0x10, 0x10) : Color.FromArgb(0x60, 0xF0, 0xF0, 0xF0);
    // Status colors keep their meaning across themes.
    internal static readonly Color Good = Color.FromRgb(0x10, 0xC0, 0x4C), Warning = Color.FromRgb(0xF7, 0x9A, 0x1E), Critical = Color.FromRgb(0xE8, 0x11, 0x23);

    internal static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
    internal static Color Opaque(Color color) => Color.FromRgb(color.R, color.G, color.B);
    internal static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255;
    /// <summary>Readable ink on <paramref name="background"/>.</summary>
    internal static Color InkOn(Color background) => Luminance(background) > 0.6 ? Color.FromRgb(0x1A, 0x1A, 0x1A) : Colors.White;

    internal static SolidColorBrush Brush(Color color) => new(color);

    private static Color Resource(string key, Color fallback) =>
        Application.Current?.TryFindResource(key) is SolidColorBrush brush ? brush.Color : fallback;

}

/// <summary>
/// One set of motion for every tablet surface: Windows 10 / Fluent curves for timed
/// transitions (decelerate in, accelerate out, no bounce: Metro is crisp) and critically
/// damped springs for finger-driven ones. Animations ask for the panel's refresh rate.
/// </summary>
internal static class TabletMotion
{
    /// <summary>Press in.</summary>
    internal const int Fast = 90;
    /// <summary>Release, small state changes.</summary>
    internal const int Short = 200;
    /// <summary>Panels entering.</summary>
    internal const int Enter = 300;
    /// <summary>Panels leaving (leaving is always quicker than entering).</summary>
    internal const int Exit = 170;

    /// <summary>Fluent "decelerate": things arriving.</summary>
    internal static readonly IEasingFunction Decelerate = Frozen(new CubicBezierEase(0.1, 0.9, 0.2, 1));
    /// <summary>Fluent "accelerate": things leaving.</summary>
    internal static readonly IEasingFunction Accelerate = Frozen(new CubicBezierEase(0.7, 0, 1, 0.5));
    /// <summary>Fluent "standard": moves that start and end on screen.</summary>
    internal static readonly IEasingFunction Standard = Frozen(new CubicBezierEase(0.8, 0, 0.2, 1));

    /// <summary>Spring response (s) of surfaces that follow a finger and settle (shelf, cards).</summary>
    internal const double SurfaceResponse = 0.3;
    /// <summary>Scale of a pressed tile or shelf cell (Windows 10 tile press).</summary>
    internal const double PressScale = 0.94;

    /// <summary>Frame rate animations request: the panel's refresh rate (WPF otherwise caps timelines at 60).</summary>
    internal static int FrameRate { get; private set; } = 60;

    /// <summary>Reads the panel's refresh rate once (and again after display changes).</summary>
    internal static void UpdateFrameRate()
    {
        var hz = ControlCenter.RefreshRate.Read().Current;
        FrameRate = hz > 0 ? Math.Clamp(hz, 60, 240) : 60;
    }

    /// <summary>A timed animation on the shared curves, at the panel's frame rate.</summary>
    internal static DoubleAnimation To(double to, int ms, IEasingFunction? ease)
    {
        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
        Timeline.SetDesiredFrameRate(animation, FrameRate);
        return animation;
    }

    /// <summary>Same, from an explicit start value.</summary>
    internal static DoubleAnimation FromTo(double from, double to, int ms, IEasingFunction? ease)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
        Timeline.SetDesiredFrameRate(animation, FrameRate);
        return animation;
    }

    /// <summary>Animates a uniform scale (press feedback): in fast, out on the decelerate curve.</summary>
    internal static void Scale(ScaleTransform scale, double to, int? ms = null)
    {
        var animation = To(to, ms ?? (to < 1 ? Fast : Short), to < 1 ? Standard : Decelerate);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private static T Frozen<T>(T value) where T : Freezable { value.Freeze(); return value; }
}

/// <summary>CSS-style cubic Bézier easing (the curves Windows' own motion is specified in).</summary>
internal sealed class CubicBezierEase : EasingFunctionBase
{
    private readonly double _x1, _y1, _x2, _y2;

    internal CubicBezierEase(double x1, double y1, double x2, double y2)
    {
        _x1 = x1; _y1 = y1; _x2 = x2; _y2 = y2;
        EasingMode = EasingMode.EaseIn; // EaseInCore is the curve itself
    }

    protected override double EaseInCore(double normalizedTime)
    {
        var x = Math.Clamp(normalizedTime, 0, 1);
        if (x is 0 or 1) return x;
        // Solve Bx(t) = x (Newton, then bisection as a fallback), return By(t).
        var t = x;
        for (var i = 0; i < 6; i++)
        {
            var dx = Bezier(t, _x1, _x2) - x;
            var slope = Slope(t, _x1, _x2);
            if (Math.Abs(dx) < 1e-5) return Bezier(t, _y1, _y2);
            if (Math.Abs(slope) < 1e-6) break;
            t -= dx / slope;
        }
        double lo = 0, hi = 1; t = x;
        for (var i = 0; i < 24; i++)
        {
            var value = Bezier(t, _x1, _x2);
            if (Math.Abs(value - x) < 1e-5) break;
            if (value < x) lo = t; else hi = t;
            t = (lo + hi) / 2;
        }
        return Bezier(t, _y1, _y2);
    }

    private static double Bezier(double t, double p1, double p2) => ((1 - 3 * p2 + 3 * p1) * t + (3 * p2 - 6 * p1)) * t * t + 3 * p1 * t;
    private static double Slope(double t, double p1, double p2) => 3 * (1 - 3 * p2 + 3 * p1) * t * t + 2 * (3 * p2 - 6 * p1) * t + 3 * p1;

    protected override Freezable CreateInstanceCore() => new CubicBezierEase(_x1, _y1, _x2, _y2);
}
