using System.Windows.Media.Imaging;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Copies chosen background pixels into local app storage, without preserving source paths or image metadata.</summary>
internal sealed class BackgroundImageStore(string dataDirectory)
{
    internal string? Resolve(string? id) => Guid.TryParseExact(id, "N", out _)
        ? Path.Combine(dataDirectory, "Backgrounds", id + ".png") : null;

    /// <summary>Decodes a bounded local image and saves an independent PNG; the caller owns the returned identity.</summary>
    /// <param name="path">User-selected existing local image, at most 32 MiB.</param>
    /// <returns>A generated image identity containing no original filename or directory.</returns>
    internal string Import(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length > 32 * 1024 * 1024) throw new InvalidDataException(L.T(K.BackgroundInvalid));
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        image.DecodePixelWidth = 2560;
        image.UriSource = new Uri(file.FullName, UriKind.Absolute); image.EndInit(); image.Freeze();
        if (image.PixelHeight > 16384) throw new InvalidDataException(L.T(K.BackgroundInvalid));
        var id = Guid.NewGuid().ToString("N"); var destination = Resolve(id)!;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        // Creating a new frame from pixels deliberately drops EXIF/XMP metadata.
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image, null, null, null));
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        encoder.Save(output); return id;
    }
}
