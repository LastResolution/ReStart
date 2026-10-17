using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace ReStart.App.Controls;

/// <summary>Crossfades decoded host images behind native tile text without giving scripts file or URL access.</summary>
internal sealed class SlideshowTileView : FrameworkElement
{
    private BitmapSource? _current, _previous;
    private static readonly DependencyProperty TransitionProperty = DependencyProperty.Register("Transition", typeof(double), typeof(SlideshowTileView),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    internal void SetSource(BitmapSource? image, bool animate)
    {
        if (_current == image) return;
        _previous = _current; _current = image;
        BeginAnimation(TransitionProperty, null);
        if (animate && image != null && !SystemParameters.HighContrast && SystemParameters.ClientAreaAnimation)
        {
            var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450));
            animation.Completed += (_, _) => { if (_current == image) _previous = null; };
            BeginAnimation(TransitionProperty, animation);
        }
        else { _previous = null; SetValue(TransitionProperty, 1d); }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_current == null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var bounds = new Rect(RenderSize);
        dc.PushClip(new RectangleGeometry(bounds));
        void Draw(BitmapSource source)
        {
            var scale = Math.Max(ActualWidth / source.PixelWidth, ActualHeight / source.PixelHeight);
            var width = source.PixelWidth * scale; var height = source.PixelHeight * scale;
            dc.DrawImage(source, new((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height));
        }
        if (_previous != null) Draw(_previous);
        dc.PushOpacity((double)GetValue(TransitionProperty)); Draw(_current); dc.Pop();
        dc.DrawRectangle(new LinearGradientBrush(Color.FromArgb(30, 0, 0, 0), Color.FromArgb(200, 0, 0, 0), 90), null, bounds);
        dc.Pop();
    }
}
