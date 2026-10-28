using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReStart.App.Tablet.ControlCenter;

/// <summary>
/// Frosted backdrop from a screen snapshot. A live DWM acrylic backdrop cannot be faded in
/// or animated and turns solid when transparency or battery saver is off; a snapshot can
/// cross-fade from sharp to blurred like iPadOS, on an opaque (fast, non-layered) window.
/// </summary>
internal static class Backdrop
{
    private const int Downscale = 10;

    /// <summary>Captures <paramref name="bounds"/> (physical pixels). Returns the sharp frame and a blurred, saturated low-res copy.</summary>
    internal static (BitmapSource Sharp, BitmapSource Blurred)? Capture(System.Drawing.Rectangle bounds)
    {
        var screen = GetDC(0);
        if (screen == 0) return null;
        nint memory = 0, small = 0, full = 0, dib = 0;
        try
        {
            memory = CreateCompatibleDC(screen); small = CreateCompatibleDC(screen);
            full = CreateCompatibleBitmap(screen, bounds.Width, bounds.Height);
            var old = SelectObject(memory, full);
            BitBlt(memory, 0, 0, bounds.Width, bounds.Height, screen, bounds.X, bounds.Y, 0x00CC0020 /* SRCCOPY */);
            SelectObject(memory, old);
            var sharp = Imaging.CreateBitmapSourceFromHBitmap(full, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            sharp.Freeze();

            // Box-average down with HALFTONE, then blur the small copy on the CPU (microseconds at this size).
            int w = Math.Max(1, bounds.Width / Downscale), h = Math.Max(1, bounds.Height / Downscale);
            var info = new BitmapInfo { Size = 40 /* BITMAPINFOHEADER */, Width = w, Height = -h, Planes = 1, BitCount = 32 };
            dib = CreateDIBSection(small, ref info, 0, out var bits, 0, 0);
            var oldSmall = SelectObject(small, dib);
            old = SelectObject(memory, full);
            SetStretchBltMode(small, 4 /* HALFTONE */); SetBrushOrgEx(small, 0, 0, 0);
            StretchBlt(small, 0, 0, w, h, memory, 0, 0, bounds.Width, bounds.Height, 0x00CC0020);
            SelectObject(memory, old); SelectObject(small, oldSmall);
            GdiFlush();
            var pixels = new byte[w * h * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            for (var pass = 0; pass < 3; pass++) { BoxBlur(pixels, w, h, 4, true); BoxBlur(pixels, w, h, 4, false); }
            Saturate(pixels, 1.4);
            var blurred = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr32, null, pixels, w * 4);
            blurred.Freeze();
            return (sharp, blurred);
        }
        catch (Exception ex) when (ex is ExternalException or ArgumentException or OutOfMemoryException)
        { TabletLog.Write("cc", "backdrop capture failed: " + ex.Message); return null; }
        finally
        {
            if (dib != 0) DeleteObject(dib);
            if (full != 0) DeleteObject(full);
            if (small != 0) DeleteDC(small);
            if (memory != 0) DeleteDC(memory);
            ReleaseDC(0, screen);
        }
    }

    private static void BoxBlur(byte[] p, int w, int h, int r, bool horizontal)
    {
        int lines = horizontal ? h : w, length = horizontal ? w : h, step = horizontal ? 4 : w * 4;
        var line = new byte[length * 4];
        for (var l = 0; l < lines; l++)
        {
            var origin = horizontal ? l * w * 4 : l * 4;
            for (var i = 0; i < length; i++) Buffer.BlockCopy(p, origin + i * step, line, i * 4, 4);
            for (var c = 0; c < 3; c++)
            {
                int sum = 0, window = 2 * r + 1;
                for (var k = -r; k <= r; k++) sum += line[Math.Clamp(k, 0, length - 1) * 4 + c];
                for (var i = 0; i < length; i++)
                {
                    p[origin + i * step + c] = (byte)(sum / window);
                    sum += line[Math.Min(i + r + 1, length - 1) * 4 + c] - line[Math.Max(i - r, 0) * 4 + c];
                }
            }
        }
    }

    private static void Saturate(byte[] p, double amount)
    {
        for (var i = 0; i < p.Length; i += 4)
        {
            double b = p[i], g = p[i + 1], r = p[i + 2], gray = 0.299 * r + 0.587 * g + 0.114 * b;
            p[i] = (byte)Math.Clamp(gray + (b - gray) * amount, 0, 255);
            p[i + 1] = (byte)Math.Clamp(gray + (g - gray) * amount, 0, 255);
            p[i + 2] = (byte)Math.Clamp(gray + (r - gray) * amount, 0, 255);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo { public int Size, Width, Height; public short Planes, BitCount; public int Compression, SizeImage, XPelsPerMeter, YPelsPerMeter, ClrUsed, ClrImportant, Colors; }

    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint item);
    [DllImport("gdi32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(nint item);
    [DllImport("gdi32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool BitBlt(nint dest, int x, int y, int width, int height, nint source, int sx, int sy, uint rop);
    [DllImport("gdi32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool StretchBlt(nint dest, int x, int y, int width, int height, nint source, int sx, int sy, int sw, int sh, uint rop);
    [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(nint dc, int mode);
    [DllImport("gdi32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetBrushOrgEx(nint dc, int x, int y, nint previous);
    [DllImport("gdi32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GdiFlush();
}
