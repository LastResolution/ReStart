using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ReStart.App.Tablet.Design;

namespace ReStart.App.Tablet.ControlCenter;

/// <summary>Underdamped spring as an easing curve (iOS-style settle with a small overshoot).</summary>
internal sealed class SpringEase : EasingFunctionBase
{
    private readonly double _damping, _omega;

    /// <param name="damping">Damping ratio; 1 = no overshoot.</param>
    /// <param name="omega">Natural frequency in units of 1/duration; damping * omega near 6 settles by the end.</param>
    internal SpringEase(double damping = 0.72, double omega = 8.5) { _damping = damping; _omega = omega; EasingMode = EasingMode.EaseIn; }

    protected override double EaseInCore(double t)
    {
        if (t >= 1) return 1;
        var z = _damping; var w = _omega;
        if (z >= 1) return 1 - Math.Exp(-w * t) * (1 + w * t);
        var wd = w * Math.Sqrt(1 - z * z);
        return 1 - Math.Exp(-z * w * t) * (Math.Cos(wd * t) + z * w / wd * Math.Sin(wd * t));
    }

    protected override Freezable CreateInstanceCore() => new SpringEase(_damping, _omega);
}

/// <summary>Control Center colors for one system theme, built from the shared tablet palette (Home's accent and tiles).</summary>
internal sealed record Palette(bool Dark, Color Scrim, Color Platter, Color Active, Color Text, Color Secondary, Color OnActive,
    Color ToggleOff, Color SliderTrack, Color SliderFill, Color SliderGlyph)
{
    internal static readonly Color Blue = Color.FromRgb(0x0A, 0x84, 0xFF), Orange = Color.FromRgb(0xFF, 0x9F, 0x0A),
        Red = Color.FromRgb(0xFF, 0x45, 0x3A), Green = Color.FromRgb(0x30, 0xD1, 0x58), Indigo = Color.FromRgb(0x5E, 0x5C, 0xE6);

    // "On" is Home's accent with its own ink, everywhere (Windows 10 quick actions); unlit tiles are Home's tile material.
    internal static Palette For(bool dark)
    {
        var accent = TabletStyle.Accent; var onAccent = TabletStyle.OnAccent;
        return dark
            ? new(true, Color.FromArgb(0x8C, 0x10, 0x10, 0x10), Color.FromArgb(0xCC, 0x2B, 0x2B, 0x2B), accent,
                Colors.White, Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF), onAccent, Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF),
                Color.FromArgb(0xCC, 0x2B, 0x2B, 0x2B), accent, onAccent)
            : new(false, Color.FromArgb(0x60, 0xF0, 0xF0, 0xF0), Color.FromArgb(0xCC, 0xF2, 0xF2, 0xF2), accent,
                Color.FromRgb(0x1A, 0x1A, 0x1A), Color.FromArgb(0xB0, 0x1A, 0x1A, 0x1A), onAccent, Color.FromArgb(0x24, 0, 0, 0),
                Color.FromArgb(0xCC, 0xF2, 0xF2, 0xF2), accent, onAccent);
    }
}

/// <summary>Shared look and motion helpers.</summary>
internal static class Ui
{
    internal static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    // Home's typeface (Segoe UI); big values use its Light weight like Metro live tiles.
    internal static readonly FontFamily Text = TabletStyle.Text;
    internal static readonly FontFamily Display = TabletStyle.Text;
    internal static readonly Duration Quick = new(TimeSpan.FromMilliseconds(160));

    /// <summary>
    /// Runs UI work without letting an exception escape: ReStart treats any unhandled UI-thread
    /// exception as fatal (message box + exit), and a flaky system API must not close the shell.
    /// </summary>
    internal static void Safe(string what, Action action)
    {
        try { action(); }
        catch (Exception ex) { TabletLog.Write("cc", $"{what} failed: {ex}"); }
    }

    internal static TextBlock Glyph(string glyph, double size) => new()
    {
        Text = glyph, FontFamily = Icons, FontSize = size, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        Foreground = new SolidColorBrush(Colors.White), IsHitTestVisible = false,
    };

    internal static TextBlock Label(double size, FontWeight weight) => new()
    {
        FontFamily = Text, FontSize = size, FontWeight = weight, TextTrimming = TextTrimming.CharacterEllipsis, IsHitTestVisible = false,
        Foreground = new SolidColorBrush(Colors.White),
    };

    /// <summary>Animates a brush to a color (brushes created here are never frozen).</summary>
    internal static void Tint(Brush? brush, Color to, bool animate = true)
    {
        if (brush is not SolidColorBrush solid || solid.IsFrozen) return;
        if (!animate) { solid.BeginAnimation(SolidColorBrush.ColorProperty, null); solid.Color = to; return; }
        solid.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, Quick) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
    }

    internal static void Tint(TextBlock text, Color to, bool animate = true) => Tint(text.Foreground, to, animate);

    internal static Border Platter() => new() { CornerRadius = new CornerRadius(TabletStyle.TileRadius), Background = new SolidColorBrush(Colors.Transparent), ClipToBounds = false };

    /// <summary>Makes an element a big touch target: press shrinks it on a spring, a tap without drag runs <paramref name="tap"/>.</summary>
    internal static void Tappable(FrameworkElement element, Action tap, double pressedScale = TabletMotion.PressScale)
    {
        var scale = new ScaleTransform(1, 1);
        element.RenderTransform = scale; element.RenderTransformOrigin = new Point(0.5, 0.5);
        Point? start = null;
        void Settle(double to, double ms) => Animate(scale, to, ms);
        element.MouseLeftButtonDown += (_, e) =>
        {
            start = e.GetPosition(element); element.CaptureMouse(); Settle(pressedScale, 120); e.Handled = true;
        };
        element.MouseMove += (_, e) =>
        {
            if (start is not { } origin || !element.IsMouseCaptured) return;
            var at = e.GetPosition(element);
            if ((at - origin).Length > 24) { start = null; element.ReleaseMouseCapture(); }
        };
        element.LostMouseCapture += (_, _) => { start = null; Settle(1, 380); };
        element.MouseLeftButtonUp += (_, e) =>
        {
            var tapped = start != null && element.IsMouseCaptured;
            start = null;
            if (element.IsMouseCaptured) element.ReleaseMouseCapture();
            Settle(1, 380);
            if (tapped) { e.Handled = true; Safe("tap", tap); }
        };
    }

    // The shared tile press (in fast, settle on the decelerate curve, no bounce).
    internal static void Animate(ScaleTransform scale, double to, double ms) => TabletMotion.Scale(scale, to, (int)ms);

    internal static string BatteryGlyph(int percent, bool charging) =>
        ((char)((charging ? 0xEBAB : 0xEBA0) + Math.Clamp((int)Math.Round(percent / 10.0), 0, 10))).ToString();

    internal static string FormatSpan(TimeSpan span) => span.TotalMinutes < 1 ? "<1 min" : span.TotalHours >= 1 ? $"{(int)span.TotalHours} h {span.Minutes} min" : $"{span.Minutes} min";
}

/// <summary>
/// iOS-style vertical slider pill: drag anywhere moves the value relative to the press,
/// a tap jumps to that point, and a tap on the bottom glyph runs <see cref="GlyphTapped"/> (mute).
/// </summary>
internal sealed class PillSlider : Border
{
    private readonly Border _fill = new() { VerticalAlignment = VerticalAlignment.Bottom, Background = new SolidColorBrush(Colors.White) };
    private readonly TextBlock _glyph = Ui.Glyph("", 26);
    private readonly ScaleTransform _scale = new(1, 1);
    private double _value;
    private Point? _press;
    private double _pressValue;
    private bool _dragged;
    private Palette? _palette;

    /// <summary>Raised while the user changes the value (0..1).</summary>
    internal event Action<double>? ValueChanged;
    /// <summary>Raised when the bottom glyph is tapped.</summary>
    internal event Action? GlyphTapped;
    /// <summary>Whether the user is dragging (external updates are ignored meanwhile).</summary>
    internal bool IsDragging => _press != null;

    internal PillSlider()
    {
        CornerRadius = new CornerRadius(TabletStyle.TileRadius);
        Background = new SolidColorBrush(Colors.Gray);
        _glyph.VerticalAlignment = VerticalAlignment.Bottom; _glyph.Margin = new Thickness(0, 0, 0, 20);
        Child = new Grid { Children = { _fill, _glyph } };
        RenderTransform = _scale; RenderTransformOrigin = new Point(0.5, 0.5);
        SizeChanged += (_, _) => { Clip = new RectangleGeometry(new Rect(RenderSize), CornerRadius.TopLeft, CornerRadius.TopLeft); Layout(); };
        MouseLeftButtonDown += (_, e) => { _press = e.GetPosition(this); _pressValue = _value; _dragged = false; CaptureMouse(); Ui.Animate(_scale, 1.04, 160); e.Handled = true; };
        MouseMove += (_, e) =>
        {
            if (_press is not { } origin || !IsMouseCaptured) return;
            var dy = e.GetPosition(this).Y - origin.Y;
            if (!_dragged && Math.Abs(dy) < 4) return;
            _dragged = true;
            SetLevel(_pressValue - dy / Math.Max(1, ActualHeight - 8), true);
        };
        MouseLeftButtonUp += (_, e) =>
        {
            if (_press is { } origin && !_dragged)
            {
                if (GlyphTapped is { } glyphTapped && origin.Y > ActualHeight - 64) Ui.Safe("slider glyph", glyphTapped);
                else SetLevel(1 - origin.Y / Math.Max(1, ActualHeight), true);
            }
            _press = null;
            if (IsMouseCaptured) ReleaseMouseCapture();
            Ui.Animate(_scale, 1, 380);
            e.Handled = true;
        };
        LostMouseCapture += (_, _) => { if (_press != null) { _press = null; Ui.Animate(_scale, 1, 380); } };
    }

    /// <summary>Current value 0..1.</summary>
    internal double Value => _value;

    /// <summary>Sets the value; <paramref name="user"/> raises <see cref="ValueChanged"/>.</summary>
    internal void SetLevel(double value, bool user = false)
    {
        if (!user && IsDragging) return;
        _value = Math.Clamp(value, 0, 1);
        Layout();
        if (user && ValueChanged is { } changed) Ui.Safe("slider", () => changed(_value));
    }

    /// <summary>Sets the bottom glyph (e.g. muted speaker).</summary>
    internal void SetGlyph(string glyph) => _glyph.Text = glyph;

    internal void Apply(Palette palette, bool animate)
    {
        _palette = palette;
        Ui.Tint(Background, palette.SliderTrack, animate);
        Ui.Tint(_fill.Background, palette.SliderFill, animate);
        Layout();
    }

    private void Layout()
    {
        _fill.Height = Math.Max(0, ActualHeight * _value);
        // The glyph sits on the fill once the fill covers it, so it switches to the fill's ink.
        if (_palette != null) Ui.Tint(_glyph, ActualHeight * _value > 48 ? _palette.SliderGlyph : _palette.Text, false);
    }
}
