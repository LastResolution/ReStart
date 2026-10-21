using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReStart.App.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private int VerifyPresentationAssets(string directory)
    {
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        var pictures = Path.Combine(directory, "synthetic-pictures"); Directory.CreateDirectory(pictures);
        string Picture(string name, int size)
        {
            var path = Path.Combine(pictures, name + ".png");
            var pixels = new byte[size * size * 4];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                { var i = (y * size + x) * 4; pixels[i] = (byte)(x * 255 / size); pixels[i + 1] = (byte)(y * 255 / size); pixels[i + 2] = 90; pixels[i + 3] = 255; }
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4)));
            using var output = File.Create(path); encoder.Save(output); return path;
        }
        var small = Picture("small", 32); var large = Picture("large", 512);
        var selected = AccountPictureSource.FromCandidates([(96, small), (512, large)]);
        Check(selected is { PixelWidth: 384, IsFrozen: true }, $"The account picture preferred a low-resolution candidate or failed to bound its decode size ({selected?.PixelWidth}, frozen={selected?.IsFrozen}).");
        Check(AccountPictureSource.FromCandidates([(512, large + ".missing"), (32, small)]) is { PixelWidth: 32 }, "A missing high-resolution portrait prevented fallback or upscaled a tiny source.");
        using (File.Open(large, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { checks++; }
        var rail = new RailLabel { Picture = selected, Width = 44 };
        Check(RenderOptions.GetBitmapScalingMode(rail.Portrait) == BitmapScalingMode.HighQuality, "The account portrait did not use high-quality downsampling.");
        var original = Document; Content = null;
        Root.Width = 1280; Root.Height = 800;
        using var host = HiddenRenderHost(Root, 1280, 800);
        _catalog.Icons["preview-edge"] = selected!;
        try
        {
            foreach (var scale in new[] { .7, 1, 1.5 })
                foreach (var size in Enum.GetValues<TileSize>())
                {
                    var folder = new Tile
                    {
                        Size = size,
                        Label = "Icon previews",
                        Children = Enum.GetValues<TileSize>().Select(childSize => new Tile
                        { AppId = "preview-edge", Label = "Synthetic icon", Size = childSize }).ToList()
                    };
                    Document = new() { Preferences = new() { TileScale = scale, Animate = false, ShowWallpaper = false }, Groups = [new() { Name = "Preview bounds", Tiles = [folder] }] };
                    ApplyAppearance(); RenderGroups(); Root.Measure(new(1280, 800)); Root.Arrange(new Rect(0, 0, 1280, 800)); FitPanels(); UpdateLayout();
                    var button = _selectionViews[folder.Id].Button;
                    var mini = VisualChildren<UniformGridProxy>(button).Single();
                    var boundary = new Rect(button.RenderSize);
                    Check(boundary.Contains(mini.TransformToAncestor(button).TransformBounds(new Rect(mini.RenderSize))), "A folder preview exceeded the visible tile at " + size + "/" + scale);
                    foreach (var image in VisualChildren<Image>(mini))
                    {
                        var slot = (Border)image.Parent;
                        Check(new Rect(slot.RenderSize).Contains(image.TransformToAncestor(slot).TransformBounds(new Rect(image.RenderSize))), "A miniature icon was clipped by its cell.");
                    }
                }
        }
        finally { host.RootVisual = null; Content = Root; Document = original; RenderGroups(); _catalog.Icons.TryRemove("preview-edge", out _); }
        return checks;
    }
}
