using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using ReStart.Core;
using Windows.Management.Deployment;

namespace ReStart.App.Services;

/// <summary>Loads bounded frozen raster assets only from an owning package or its LocalState; never downloads images or opens arbitrary file paths.</summary>
internal sealed class LegacyTileAssets
{
    private sealed record Cached(long Stamp, long Length, BitmapSource Bitmap);
    private readonly Dictionary<string, Cached> _images = new(StringComparer.OrdinalIgnoreCase);

    internal IReadOnlyDictionary<(string App, string Source), BitmapSource> Read(LegacyNotificationStore.Snapshot snapshot, CancellationToken token)
    {
        var result = new Dictionary<(string, string), BitmapSource>(AssetKeyComparer.Instance); var retained = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manager = new PackageManager();
        foreach (var (appId, frame) in snapshot.Apps)
        {
            token.ThrowIfCancellationRequested();
            if (!LegacyNotificationStore.IsPackageAppId(appId)) continue;
            try
            {
                var family = appId[..appId.IndexOf('!')];
                var package = manager.FindPackagesForUser("", family).FirstOrDefault();
                if (package == null) continue;
                var root = package.InstalledLocation.Path;
                var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", family, "LocalState");
                foreach (var source in frame.Bindings.SelectMany(b => Sources(b.Elements)).Distinct(StringComparer.Ordinal).Take(16))
                {
                    token.ThrowIfCancellationRequested();
                    if (LegacyTileAssetPath.Resolve(source, root, local) is not { } path) continue;
                    var approvedRoot = path.StartsWith(Path.GetFullPath(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) ? root : local;
                    if (ResolveExistingAsset(path, approvedRoot, approvedRoot == root) is not { } found) continue;
                    path = found;
                    var info = new FileInfo(path);
                    if (info.Length is <= 0 or > 3_000_000) continue;
                    if (!_images.TryGetValue(path, out var cached) || cached.Stamp != info.LastWriteTimeUtc.Ticks || cached.Length != info.Length)
                    {
                        if (LoadRaster(path, approvedRoot) is not { } image) continue;
                        cached = new(info.LastWriteTimeUtc.Ticks, info.Length, image); _images[path] = cached;
                    }
                    retained.Add(path); result[(appId, source)] = cached.Bitmap;
                    if (retained.Count >= 64) break;
                }
            }
            catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or System.Security.SecurityException
                or ArgumentException or NotSupportedException or InvalidOperationException or System.IO.FileFormatException)
            { }
            if (retained.Count >= 64) break;
        }
        foreach (var path in _images.Keys.Where(path => !retained.Contains(path)).ToArray()) _images.Remove(path);
        return result;
    }

    internal static string? ResolveExistingAsset(string candidate, string root, bool resourceVariants)
    {
        var prefix = Path.GetFullPath(root).TrimEnd('\\') + "\\";
        if (!Path.GetFullPath(candidate).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        if (File.Exists(candidate)) return SafeFile(candidate, root) ? candidate : null;
        var directory = Path.GetDirectoryName(candidate)!;
        if (!resourceVariants || !SafeFile(directory, root)) return null;
        var stem = Path.GetFileNameWithoutExtension(candidate); var extension = Path.GetExtension(candidate);
        var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(stem) + @"\.(?:scale|targetsize)-[0-9]{1,4}(?:_[a-z0-9-]+)?" + System.Text.RegularExpressions.Regex.Escape(extension) + "$";
        return Directory.EnumerateFiles(directory, stem + ".*" + extension).Take(32)
            .Where(path => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).FirstOrDefault(path => SafeFile(path, root));
    }

    internal static BitmapSource? LoadRaster(string path, string root)
    {
        if (!SafeFile(path, root) || new FileInfo(path).Length is <= 0 or > 3_000_000) return null;
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        var size = decoder.Frames[0]; var width = size.PixelWidth; var height = size.PixelHeight;
        if (width <= 0 || height <= 0 || (long)width * height > 16_777_216) return null;
        stream.Position = 0;
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        if (width >= height) image.DecodePixelWidth = Math.Min(width, 256); else image.DecodePixelHeight = Math.Min(height, 256);
        image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
    }

    private static IEnumerable<string> Sources(IEnumerable<LegacyTileElement> elements) => elements.SelectMany(e =>
        (e.Kind == "image" ? new[] { e.Source } : []).Concat(Sources(e.Children)));

    private static bool SafeFile(string path, string root)
    {
        var current = Path.GetFullPath(path); var boundary = Path.GetFullPath(root).TrimEnd('\\');
        if (!current.Equals(boundary, StringComparison.OrdinalIgnoreCase) && !current.StartsWith(boundary + "\\", StringComparison.OrdinalIgnoreCase)) return false;
        while (current.Length >= boundary.Length)
        {
            if (!File.Exists(current) && !Directory.Exists(current) || (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
            if (current.Equals(boundary, StringComparison.OrdinalIgnoreCase)) return true;
            current = Path.GetDirectoryName(current)!;
        }
        return false;
    }

    private sealed class AssetKeyComparer : IEqualityComparer<(string App, string Source)>
    {
        internal static readonly AssetKeyComparer Instance = new();
        public bool Equals((string App, string Source) x, (string App, string Source) y) => StringComparer.OrdinalIgnoreCase.Equals(x.App, y.App) && StringComparer.Ordinal.Equals(x.Source, y.Source);
        public int GetHashCode((string App, string Source) value) => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(value.App), StringComparer.Ordinal.GetHashCode(value.Source));
    }
}
