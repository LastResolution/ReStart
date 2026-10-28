using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReStart.App.Tablet.Design;
using System.Windows.Shapes;
using ReStart.App.Tablet.Gestures;

namespace ReStart.App.Tablet.Switcher;

/// <summary>One app in the switcher: a live thumbnail framed like an iPad card, with icon and name above it.</summary>
internal sealed class SwitcherCard : IDisposable
{
    internal const double HeaderHeight = 40;
    internal AppWindow Window { get; }
    internal Thumbnail? Thumb { get; private set; }
    internal Canvas Root { get; } = new() { IsHitTestVisible = false };
    private readonly Border _frame;
    private readonly Grid _header;
    private readonly Border _split;
    private readonly Image _placeholderIcon;

    // Center of the frame (DIP), frame width (DIP), opacity, vertical lift (flick-to-close), header opacity.
    internal Spring X { get; } = new(0);
    internal Spring Y { get; } = new(0);
    internal Spring W { get; } = new(0);
    internal Spring Alpha { get; } = new(0, 0.3, 1);
    internal Spring Lift { get; } = new(0, 0.42, 0.9);
    internal Spring Chrome { get; } = new(0, 0.3, 1);
    internal bool Closing { get; set; }
    internal double Aspect { get; private set; } = 1.6;
    /// <summary>Frame rectangle in overlay DIPs, as last drawn.</summary>
    internal Rect Frame { get; private set; }

    internal SwitcherCard(AppWindow window, nint overlay, double aspect)
    {
        Window = window;
        Aspect = aspect;
        if (!window.IsMinimized)
        {
            Thumb = Thumbnail.Create(overlay, window.Handle);
            if (Thumb != null && Thumb.Aspect is > 0.3 and < 4) Aspect = Thumb.Aspect;
        }
        var icon = AppInfo.Icon(window, 96);
        _placeholderIcon = new Image { Source = icon, Width = 72, Height = 72, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _frame = new Border
        {
            // A flat Home tile with a hairline (no blur shadow: the frame is resized every frame).
            CornerRadius = new CornerRadius(TabletStyle.TileRadius),
            Background = new SolidColorBrush(Color.FromArgb(0x66, 0x30, 0x30, 0x36)),
            BorderBrush = Hairline, BorderThickness = new Thickness(1),
            Child = Thumb == null ? _placeholderIcon : null,
        };
        _split = new Border
        {
            Width = 40, Height = 30, CornerRadius = new CornerRadius(TabletStyle.ControlRadius), Background = new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0)),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 4),
            Child = SplitGlyph(),
        };
        var name = new TextBlock
        {
            Text = AppInfo.Name(window), Foreground = Brushes.White, FontFamily = TabletStyle.Text, FontSize = TabletStyle.Body,
            VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(8, 0, 48, 0),
        };
        _header = new Grid { Height = HeaderHeight };
        _header.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 4),
            Children = { new Image { Source = icon, Width = 26, Height = 26 }, name },
        });
        _header.Children.Add(_split);
        Root.Children.Add(_header);
        Root.Children.Add(_frame);
    }

    private static UIElement SplitGlyph()
    {
        // One screen with a divider (two separate panes read as "00" at this size); the left pane is filled.
        var glyph = new Grid { Width = 22, Height = 15 };
        glyph.Children.Add(new Rectangle { Width = 10, Height = 15, RadiusX = 1.5, RadiusY = 1.5, Fill = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left });
        glyph.Children.Add(new Rectangle { Width = 22, Height = 15, RadiusX = 1.5, RadiusY = 1.5, Stroke = Brushes.White, StrokeThickness = 1.6 });
        return glyph;
    }

    /// <summary>Whether Split View may use this window (it must be resizable).</summary>
    internal bool CanSplit => AppWindows.CanMaximize(Window.Handle);

    /// <summary>Hit area of the split button in overlay DIPs.</summary>
    internal Rect SplitButton => new(Frame.Right - 44, Frame.Top - HeaderHeight, 48, HeaderHeight);
    /// <summary>Hit area of the whole card (header and frame).</summary>
    internal Rect HitArea => new(Frame.Left, Frame.Top - HeaderHeight, Frame.Width, Frame.Height + HeaderHeight);

    private static readonly SolidColorBrush Hairline = Frozen(new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)));
    private static SolidColorBrush Frozen(SolidColorBrush brush) { brush.Freeze(); return brush; }

    // The Split View pick is outlined in Home's accent, like a selected tile.
    internal bool Highlighted { set { _frame.BorderBrush = value ? new SolidColorBrush(TabletStyle.Accent) : Hairline; _frame.BorderThickness = new Thickness(value ? 2 : 1); } }
    internal bool ShowSplit { set => _split.Visibility = value && CanSplit ? Visibility.Visible : Visibility.Collapsed; }

    internal bool Step(double dt)
    {
        X.Step(dt); Y.Step(dt); W.Step(dt); Alpha.Step(dt); Lift.Step(dt); Chrome.Step(dt);
        return !(X.Settled && Y.Settled && W.Settled && Alpha.Settled && Lift.Settled && Chrome.Settled);
    }

    /// <summary>Applies the springs to the WPF visuals and the DWM thumbnail (<paramref name="scale"/> = pixels per DIP).</summary>
    internal void Render(double scale, double offsetX)
    {
        var w = Math.Max(1, W.Value);
        var h = w / Aspect;
        Frame = new Rect(X.Value + offsetX - w / 2, Y.Value + Lift.Value - h / 2, w, h);
        var alpha = Math.Clamp(Alpha.Value, 0, 1);
        Canvas.SetLeft(_frame, Frame.Left); Canvas.SetTop(_frame, Frame.Top);
        _frame.Width = w; _frame.Height = h;
        _frame.Opacity = alpha;
        Canvas.SetLeft(_header, Frame.Left); Canvas.SetTop(_header, Frame.Top - HeaderHeight);
        _header.Width = w;
        _header.Opacity = alpha * Math.Clamp(Chrome.Value, 0, 1);
        _placeholderIcon.Width = _placeholderIcon.Height = Math.Clamp(w * 0.18, 32, 96);
        Thumb?.Update(new Rect(Frame.Left * scale, Frame.Top * scale, Frame.Width * scale, Frame.Height * scale), alpha);
    }

    public void Dispose() { Thumb?.Dispose(); Thumb = null; }
}
