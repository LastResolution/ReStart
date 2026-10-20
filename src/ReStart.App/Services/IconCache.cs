using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReStart.App.Services;

/// <summary>Caches only bounded icon pixels under hashed local identities; corrupt or outdated entries are ordinary misses.</summary>
internal sealed class IconCache(string directory)
{
    private int _hits, _misses;
    internal int Hits => Volatile.Read(ref _hits);
    internal int Misses => Volatile.Read(ref _misses);

    internal ImageSource? Load(string target, Func<string, ImageSource?> readShell)
    {
        var key = target.ToUpperInvariant();
        try
        {
            if (File.Exists(target))
            {
                var file = new FileInfo(target);
                key += $"|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        var path = Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".png");
        try
        {
            var file = new FileInfo(path);
            if (file.Exists && file.Length <= 65536 && DateTime.UtcNow - file.LastWriteTimeUtc < TimeSpan.FromDays(1))
            {
                using var stream = File.OpenRead(path);
                var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var image = decoder.Frames[0];
                if (image.PixelWidth is > 0 and <= 256 && image.PixelHeight is > 0 and <= 256)
                { image.Freeze(); Interlocked.Increment(ref _hits); return image; }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileFormatException or NotSupportedException or ArgumentException) { }
        Interlocked.Increment(ref _misses);
        var result = readShell(target);
        if (result is BitmapSource bitmap)
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(directory);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) encoder.Save(stream);
                File.Move(temporary, path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException) { }
            finally { try { File.Delete(temporary); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } }
        }
        return result;
    }

    internal void Trim()
    {
        try
        {
            if (!Directory.Exists(directory)) return;
            foreach (var file in new DirectoryInfo(directory).EnumerateFiles("*.png").OrderByDescending(f => f.LastWriteTimeUtc).Skip(1024)) file.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
