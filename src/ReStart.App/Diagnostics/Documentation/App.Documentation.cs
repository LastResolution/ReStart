using System.Windows;
using ReStart.Core;

namespace ReStart.App;

public partial class App
{
    // This development-only entry point never creates the tray, hooks, instance
    // channel or startup registration. Platform verification shows a fictional
    // window only when explicitly requested with --visible or --interactive.
    private async Task RenderDocumentationAsync(string[] arguments)
    {
        MainWindow? renderer = null;
        try
        {
            var promo = arguments.Contains("--render-promo");
            var studio = arguments.Contains("--render-studio");
            var platform = arguments.Contains("--verify-platform");
            var index = Array.IndexOf(arguments, platform ? "--verify-platform" : promo ? "--render-promo" : studio ? "--render-studio" : "--render-docs");
            if (index + 1 >= arguments.Length) throw new ArgumentException("Supply a documentation output directory.");
            var directory = Path.GetFullPath(arguments[index + 1]);
            Directory.CreateDirectory(directory);
            var store = new LayoutStore(Path.Combine(directory, "fixture-data", "layout.json"));
            store.Save(new() { Preferences = new() { Language = "en", Animate = false, ShowWallpaper = false } });
            renderer = new(store, true);
            if (platform) await renderer.VerifyPlatformAsync(directory, arguments.Contains("--visible") || arguments.Contains("--interactive"), arguments.Contains("--interactive"));
            else if (studio && arguments.Contains("--recovery-only")) await renderer.VerifyWebTileRecoveryAsync(directory);
            else if (studio) await renderer.RenderStudioDocumentationAsync(directory);
            else if (promo)
            {
                if (index + 2 >= arguments.Length) throw new ArgumentException("Supply the FFmpeg executable after the output directory.");
                await renderer.RenderPromoAsync(directory, Path.GetFullPath(arguments[index + 2]));
            }
            else await renderer.RenderDocumentationAsync(directory);
            Shutdown(0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Documentation rendering failed: " + ex.Message);
            Shutdown(1);
        }
        finally { renderer?.Exit(); }
    }
}
