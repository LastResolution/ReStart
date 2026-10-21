using System.Windows;
using ReStart.Core;

namespace ReStart.App;

public partial class App
{
    // This development-only entry point never creates the tray, hooks, instance
    // channel, startup registration or visible windows.
    private async Task RenderDocumentationAsync(string[] arguments)
    {
        MainWindow? renderer = null;
        try
        {
            var promo = arguments.Contains("--render-promo");
            var index = Array.IndexOf(arguments, promo ? "--render-promo" : "--render-docs");
            if (index + 1 >= arguments.Length) throw new ArgumentException("Supply a documentation output directory.");
            var directory = Path.GetFullPath(arguments[index + 1]);
            Directory.CreateDirectory(directory);
            var store = new LayoutStore(Path.Combine(directory, "fixture-data", "layout.json"));
            store.Save(new() { Preferences = new() { Language = "en", Animate = false, ShowWallpaper = false } });
            renderer = new(store, true);
            if (promo)
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
