using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Checks real Shell icons across filesystem types, tile sizes, scales and folder previews without launching targets.</summary>
    internal async Task<IconSmokeResult> VerifyIconsAsync(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("Icon verification requires preview mode.");
        var original = Document;
        var directory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "icon-fixture");
        Directory.CreateDirectory(directory);
        var folder = Path.Combine(directory, "Folder sample"); Directory.CreateDirectory(folder);
        var text = Path.Combine(directory, "Text sample.txt"); File.WriteAllText(text, "Icon fixture.");
        var unknown = Path.Combine(directory, "Unknown sample.restartfixture"); File.WriteAllText(unknown, "Icon fixture.");
        var url = Path.Combine(directory, "Web sample.url"); File.WriteAllText(url, "[InternetShortcut]\r\nURL=https://example.com/\r\n");
        var image = Path.Combine(directory, "Image sample.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)255, 16 * 16 * 4).ToArray(), 64)));
        using (var output = File.Create(image)) encoder.Save(output);
        var executable = Environment.ProcessPath!;
        var entries = new List<AppEntry> { FilePinning.FromPath(folder), FilePinning.FromPath(text), FilePinning.FromPath(unknown),
            FilePinning.FromPath(url), FilePinning.FromPath(image), FilePinning.FromPath(executable) };
        var links = await CreateIconFixtureLinksAsync(directory, entries.Select(a => a.LaunchTarget).ToArray(), folder, text, executable);
        entries.AddRange(links.Select(FilePinning.FromPath));
        if (_apps.FirstOrDefault(a => a.IsShellApp) is { } shellApp) entries.Add(shellApp);
        await _catalog.LoadIconsAsync(entries);
        var rendered = 0; var folderPixels = 0;
        try
        {
            foreach (var entry in entries)
            {
                Require(_catalog.Icons.TryGetValue(entry.Id, out var source) && source is BitmapSource, $"A filesystem or Shell app icon is missing at fixture index {entries.IndexOf(entry)}.");
                var bitmap = new FormatConvertedBitmap((BitmapSource)source!, PixelFormats.Bgra32, null, 0);
                var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
                var xs = new List<int>(); var ys = new List<int>();
                for (var y = 0; y < bitmap.PixelHeight; y++)
                    for (var x = 0; x < bitmap.PixelWidth; x++)
                        if (pixels[(y * bitmap.PixelWidth + x) * 4 + 3] > 32) { xs.Add(x); ys.Add(y); }
                Require(xs.Count > 0, "An icon is fully transparent.");
                if (entry.LaunchTarget == folder)
                {
                    folderPixels = xs.Max() - xs.Min() + 1;
                    Require(folderPixels >= bitmap.PixelWidth * .5 && ys.Max() - ys.Min() + 1 >= bitmap.PixelHeight * .35,
                        "The folder icon is a tiny thumbnail badge instead of a full icon.");
                }
            }
            foreach (var scale in new[] { .7, 1, 1.5 })
            {
                Document = new()
                {
                    Preferences = new() { Animate = false, ShowWallpaper = false, TileScale = scale, Language = "en", Columns = 8 },
                    Groups = entries.Select((entry, i) => new TileGroup
                    {
                        Name = $"Icon sample {i + 1}",
                        Tiles = Enum.GetValues<TileSize>().Select(size =>
                        new Tile { Label = size.ToString(), AppId = entry.Id, Target = entry.LaunchTarget, IsShellApp = entry.IsShellApp, Size = size }).ToList()
                    }).ToList()
                };
                ApplyAppearance(); RenderGroups(); UpdateLayout();
                foreach (var button in VisualChildren<Button>(GroupsPanel).Where(b => b.Tag is Tile))
                {
                    var tile = (Tile)button.Tag;
                    var icon = VisualChildren<Image>(button).Single();
                    var expected = (double)(tile.Size == TileSize.Small ? 23 : tile.Size == TileSize.Large ? 60 : 40);
                    var (width, height) = TileLayout.Dimensions(tile.Size);
                    expected = Math.Min(expected, Math.Min(width * Cell - 4, height * Cell - 4) - 14 - (tile.Size == TileSize.Small ? 0 : 8));
                    Require(Math.Abs(icon.ActualWidth - expected) < .1 && Math.Abs(icon.ActualHeight - expected) < .1,
                        "A tile icon changes size unexpectedly for a filesystem type.");
                    rendered++;
                }
                SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"icons-{scale:0.0}.png"));
            }
            Document.Groups = [new() { Name = "Folder preview", Tiles = [new() { Label = "Tile folder", Children = entries.Take(4).Select(Tile.FromApp).ToList() }] }];
            RenderGroups(); UpdateLayout();
            Require(VisualChildren<Image>(GroupsPanel).Count() == 4, "Tile folder previews do not use the same Shell icons.");
            SetStatus("First operation");
            var timer = _statusTimer!;
            // Exercise the production timer with a shorter test-only interval.
            timer.Interval = TimeSpan.FromMilliseconds(300);
            await Task.Delay(180); SetStatus("Latest operation");
            await Task.Delay(180);
            Require(StatusText.Text == "Latest operation", "Replacing a status message did not reset its lifetime.");
            await Task.Delay(250);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Require(StatusText.Text.Length == 0 && !timer.IsEnabled, "The operation message did not expire.");
            timer.Interval = TimeSpan.FromSeconds(5);
            return new(entries.Count, rendered, folderPixels, 4, 2, links.Length + 6);
        }
        finally { SetStatus(""); Document = original; ApplyAppearance(); RenderGroups(); }
    }

    private static Task<string[]> CreateIconFixtureLinksAsync(string directory, string[] files, params string[] targets)
    {
        var completion = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            object? shellObject = null;
            try
            {
                shellObject = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
                dynamic shell = shellObject!;
                var paths = new List<string>();
                for (var i = 0; i < targets.Length; i++)
                {
                    var path = Path.Combine(directory, $"Shortcut sample {i + 1}.lnk");
                    object shortcut = shell.CreateShortcut(path);
                    try { ((dynamic)shortcut).TargetPath = targets[i]; ((dynamic)shortcut).Save(); }
                    finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut); }
                    paths.Add(path);
                }
                foreach (var path in files.Concat(paths))
                    Require(AppCatalog.GetFileIcon(path) is BitmapSource { IsFrozen: true }, "The legacy icon fallback failed for a filesystem fixture.");
                completion.SetResult(paths.ToArray());
            }
            catch (Exception ex) { completion.SetException(ex); }
            finally { if (shellObject != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shellObject); }
        })
        { IsBackground = true, Name = "ReStart icon fixture" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task;
    }
}

internal sealed record IconSmokeResult(int Types, int RenderedSizesAndScales, int FolderVisiblePixels, int FolderPreviewIcons, int StatusChecks, int FallbackTypes);
