using System.Windows;
using ReStart.App.Tablet.Gestures;
using static ReStart.App.Tablet.Gestures.NavNative;

namespace ReStart.App.Tablet.Switcher;

/// <summary>
/// A live DWM thumbnail of an app window drawn into one of our windows. DWM composes it on
/// top of the destination's WPF content, so it must be moved every frame with its card.
/// </summary>
internal sealed class Thumbnail : IDisposable
{
    private nint _id;
    private NavNative.NRect _lastDest;
    private byte _lastOpacity;
    private bool _lastVisible;

    /// <summary>The window shown.</summary>
    internal nint Source { get; }
    /// <summary>Visible frame of the source in its own window coordinates (crops the invisible resize borders).</summary>
    internal NavNative.NRect Crop { get; private set; }
    /// <summary>Width/height of the visible frame.</summary>
    internal double Aspect => Crop.Height == 0 ? 1.6 : Crop.Width / (double)Crop.Height;

    private Thumbnail(nint id, nint source) { _id = id; Source = source; RefreshCrop(); }

    internal static Thumbnail? Create(nint destination, nint source)
    {
        var hr = DwmRegisterThumbnail(destination, source, out var id);
        if (hr != 0 || id == 0) { TabletLog.Write("switcher", $"no thumbnail for {source:X} ({AppWindows.ClassName(source)}): 0x{hr:X8}"); return null; }
        return new Thumbnail(id, source);
    }

    /// <summary>Re-reads the source's frame (call when the window may have moved or resized).</summary>
    internal void RefreshCrop()
    {
        GetWindowRect(Source, out var window);
        var visible = VisibleBounds(Source);
        Crop = new(visible.Left - window.Left, visible.Top - window.Top, visible.Right - window.Left, visible.Bottom - window.Top);
    }

    /// <summary>Positions the thumbnail; <paramref name="destination"/> is in the destination window's client pixels.</summary>
    internal void Update(Rect destination, double opacity, bool visible = true)
    {
        if (_id == 0) return;
        var dest = new NavNative.NRect((int)Math.Round(destination.Left), (int)Math.Round(destination.Top), (int)Math.Round(destination.Right), (int)Math.Round(destination.Bottom));
        var alpha = (byte)Math.Clamp(Math.Round(opacity * 255), 0, 255);
        visible &= alpha > 0 && dest.Width > 0 && dest.Height > 0;
        if (dest.Equals(_lastDest) && alpha == _lastOpacity && visible == _lastVisible) return;
        _lastDest = dest; _lastOpacity = alpha; _lastVisible = visible;
        var properties = new ThumbnailProperties
        {
            Flags = TnpRectDestination | TnpRectSource | TnpOpacity | TnpVisible | TnpSourceClientAreaOnly,
            Destination = dest, Source = Crop, Opacity = alpha, Visible = visible, SourceClientAreaOnly = false,
        };
        DwmUpdateThumbnailProperties(_id, ref properties);
    }

    internal void Hide() => Update(default, 0, false);

    public void Dispose()
    {
        if (_id == 0) return;
        DwmUnregisterThumbnail(_id);
        _id = 0;
    }
}
