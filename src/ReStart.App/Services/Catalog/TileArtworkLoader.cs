using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using ReStart.Core;
using Windows.Management.Deployment;

namespace ReStart.App.Services;

/// <summary>Loads declared raster artwork for requested pins from installed package/desktop manifests, without network access or execution.</summary>
internal static class TileArtworkLoader
{
    internal sealed record Request(AppEntry App, TileSize Size, int Pixels, bool Light, string Language);
    internal sealed record Frame(BitmapSource Image, string Background, string Foreground, bool ShowName);

    internal static Task<IReadOnlyDictionary<(string App, TileSize Size), Frame>> ReadAsync(IReadOnlyList<Request> requests, CancellationToken token)
    {
        var completion = new TaskCompletionSource<IReadOnlyDictionary<(string App, TileSize Size), Frame>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(Read(requests, token)); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { completion.SetCanceled(token); }
            catch (Exception ex) { completion.SetException(ex); }
        })
        { IsBackground = true, Name = "ReStart tile artwork" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }

    internal static IReadOnlyDictionary<(string App, TileSize Size), Frame> Read(IReadOnlyList<Request> requests, CancellationToken token = default)
    {
        var result = new Dictionary<(string, TileSize), Frame>();
        var manifests = new Dictionary<string, (string Root, AppTileArtwork Artwork)?>(StringComparer.OrdinalIgnoreCase);
        var images = new Dictionary<(string Path, int Pixels), BitmapSource>();
        foreach (var request in requests.Take(256))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (!manifests.TryGetValue(request.App.Id, out var declaration)) manifests[request.App.Id] = declaration = ReadManifest(request.App);
                if (declaration is not { } metadata || !metadata.Artwork.Images.TryGetValue(request.Size, out var reference)) continue;
                if (LegacyTileAssetPath.Resolve(reference, metadata.Root, null) is not { } candidate) continue;
                var directory = Path.GetDirectoryName(candidate)!;
                if (!LegacyTileAssets.SafeFile(directory, metadata.Root)) continue;
                var basePixels = request.Size == TileSize.Small ? (metadata.Artwork.Images.GetValueOrDefault(TileSize.Medium) == reference ? 150 : 71)
                    : request.Size == TileSize.Medium ? 150 : 310;
                var files = Directory.EnumerateFiles(directory, Path.GetFileNameWithoutExtension(candidate) + "*" + Path.GetExtension(candidate)).Take(128).Select(Path.GetFileName).OfType<string>();
                foreach (var name in AppTileArtwork.RankVariants(Path.GetFileName(candidate), files.Where(n => n.Equals(Path.GetFileName(candidate), StringComparison.OrdinalIgnoreCase)
                    || n.StartsWith(Path.GetFileNameWithoutExtension(candidate) + ".", StringComparison.OrdinalIgnoreCase)), basePixels, request.Pixels, request.Light, request.Language))
                {
                    token.ThrowIfCancellationRequested();
                    var path = Path.Combine(directory, name); var pixels = Math.Clamp(request.Pixels, 1, 1024);
                    if (!images.TryGetValue((path, pixels), out var image))
                    {
                        image = LegacyTileAssets.LoadRaster(path, metadata.Root, pixels);
                        if (image == null) continue;
                        if (images.Values.Sum(i => (long)i.PixelWidth * i.PixelHeight) + (long)image.PixelWidth * image.PixelHeight > 16_777_216) continue;
                        images[(path, pixels)] = image;
                    }
                    result[(request.App.Id, request.Size)] = new(image, metadata.Artwork.Background, metadata.Artwork.Foreground, metadata.Artwork.NamedSizes.Contains(request.Size));
                    break;
                }
            }
            catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or System.Security.SecurityException
                or ArgumentException or NotSupportedException or InvalidOperationException or System.IO.FileFormatException)
            { }
        }
        return result;
    }

    private static (string Root, AppTileArtwork Artwork)? ReadManifest(AppEntry app)
    {
        string root, manifest; string? application = null;
        if (app.IsShellApp && LegacyNotificationStore.IsPackageAppId(app.Id))
        {
            var split = app.Id.IndexOf('!'); application = app.Id[(split + 1)..];
            var package = new PackageManager().FindPackagesForUser("", app.Id[..split]).FirstOrDefault();
            if (package == null) return null;
            root = package.InstalledLocation.Path; manifest = Path.Combine(root, "AppxManifest.xml");
        }
        else
        {
            var executable = app.ExecutablePath;
            if (executable.Length == 0 && app.ShortcutPath.Length > 0) executable = AppCatalog.ReadShortcutMetadata(app.ShortcutPath).TargetPath;
            if (executable.Length == 0 && !app.IsShellApp)
                executable = app.LaunchTarget.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? AppCatalog.ReadShortcutMetadata(app.LaunchTarget).TargetPath : app.LaunchTarget;
            if (!Path.IsPathFullyQualified(executable) || executable.StartsWith(@"\\", StringComparison.Ordinal) || !Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase)) return null;
            root = Path.GetDirectoryName(executable)!;
            if (!LocalRoot(root)) return null;
            if (!LegacyTileAssets.SafeFile(executable, root)) return null;
            manifest = Path.ChangeExtension(executable, ".VisualElementsManifest.xml");
        }
        if (!LocalRoot(root) || !LegacyTileAssets.SafeFile(manifest, root) || new FileInfo(manifest).Length is <= 0 or > 2_000_000) return null;
        using var stream = File.Open(manifest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return AppTileArtwork.Read(stream, application) is { } artwork ? (root, artwork) : null;
    }

    private static bool LocalRoot(string root)
    {
        var drive = Path.GetPathRoot(root);
        return drive is { Length: 3 } && drive[1] == ':' && new DriveInfo(drive).DriveType is DriveType.Fixed or DriveType.Removable or DriveType.CDRom or DriveType.Ram
            && LegacyTileAssets.SafeFile(root, drive);
    }
}
