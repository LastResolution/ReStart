using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Exercises real WPF clocks, interrupted dismissals, reduced motion and input against an isolated fictional app catalog.</summary>
    internal async Task<MotionSmokeResult> VerifyMotionAsync(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("Motion verification requires preview mode.");
        var directory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "fixture-" + Guid.NewGuid().ToString("N"));
        var fixture = new MainWindow(new LayoutStore(Path.Combine(directory, "layout.json")), true,
            () => Task.FromResult(PreviewApps()))
        {
            Owner = this,
            Width = 1280,
            Height = 800,
            Topmost = false,
            ShowInTaskbar = false,
            _verifyMotion = true
        };
        try
        {
            await fixture.InitializeAsync();
            fixture.Document.Preferences.Animate = true;
            fixture.Document.Preferences.CloseOnDeactivate = true;
            fixture.Present();
            Require(fixture._motion != null && fixture.IsVisible && fixture.SearchBox.IsKeyboardFocused,
                "Entrance failed to present and focus Start immediately.");
            fixture.SeekMotion(50);
            Require(fixture.Opacity > 0 && fixture.Opacity < 1 && fixture.ContentScale.ScaleX > 0.9 && fixture.ContentScale.ScaleX < 1,
                 $"Entrance lacks independent window fade and content zoom: opacity={fixture.Opacity}, scale={fixture.ContentScale.ScaleX}, clock={fixture._motion?.GetCurrentTime(fixture)}, visible={fixture.IsVisible}.");
            var targets = fixture.VisibleMotionTiles().OrderBy(item => item.Bounds.Bottom).ToArray();
            Require(targets.Length > 2 && targets[^1].Element.Opacity > targets[0].Element.Opacity,
                "Tiles do not reveal from the lower rows before the upper rows.");
            fixture.SaveMotionFrame(Path.Combine(directory, "entrance-050.png"));
            var scale = fixture.ContentScale.ScaleX;
            fixture.RenderGroups();
            Require(Math.Abs(scale - fixture.ContentScale.ScaleX) < 0.005,
                "Catalog/icon refresh restarted the entrance clock.");
            fixture.ComposeFixtureText("E");
            Require(fixture.SearchResultNames.Contains("Microsoft Edge") && fixture._motion == null && fixture.ContentScale.ScaleX == 1,
                $"Typing during entrance lost input or retained a moving search surface: query={fixture.SearchBox.Text}, results={fixture.SearchResultNames.Length}, moving={fixture._motion != null}, scale={fixture.ContentScale.ScaleX}.");

            fixture.Hide(); fixture.Present();
            await fixture.WaitForMotionAsync();
            Require(fixture.IsVisible && fixture.Opacity == 1 && fixture.ContentScale.ScaleX == 1 && fixture._motionTiles.Count == 0,
                "Completed entrance retained animation clocks or transient tile opacity.");
            fixture.Dismiss();
            Require(fixture.IsVisible && fixture._dismissing && !fixture.Root.IsHitTestVisible,
                "Dismissal hid immediately or still accepts clicks during exit.");
            fixture.SeekMotion(80);
            Require(fixture.Opacity > 0 && fixture.Opacity < 1 && fixture.ContentScale.ScaleX < 1,
                "Exit lacks fade and receding content.");
            fixture.SaveMotionFrame(Path.Combine(directory, "exit-080.png"));
            var closing = fixture._motion;
            fixture.Dismiss();
            Require(ReferenceEquals(fixture._motion, closing), "Repeated dismissals restarted the exit clock.");
            var opacity = fixture.Opacity;
            fixture.Present();
            fixture.SeekMotion(0);
            Require(!fixture._dismissing && fixture.Root.IsHitTestVisible && Math.Abs(opacity - fixture.Opacity) < 0.005,
                "Reopening an in-flight exit flashed or retained disabled input.");
            fixture.SeekMotion(50);
            opacity = fixture.Opacity; scale = fixture.ContentScale.ScaleX;
            fixture.RenderGroups();
            Require(Math.Abs(opacity - fixture.Opacity) < 0.005 && Math.Abs(scale - fixture.ContentScale.ScaleX) < 0.005,
                "A catalog refresh restarted an interrupted reopen from its default geometry.");
            await fixture.WaitForMotionAsync();
            Require(fixture.IsVisible && fixture.SearchBox.IsKeyboardFocused,
                "A stale exit callback hid or defocused the reopened launcher.");

            fixture.DismissFromTaskbar();
            await fixture.WaitForMotionAsync();
            Require(!fixture.IsVisible && fixture.Opacity == 1 && fixture._motionTiles.Count == 0,
                "Taskbar dismissal did not hide after its exit or reset animation state.");
            fixture.Present();
            fixture.SeekMotion(50);
            fixture.Dismiss();
            await fixture.WaitForMotionAsync();
            Require(!fixture.IsVisible, "Closing an unfinished entrance left Start visible.");

            fixture.Document.Preferences.Animate = false;
            fixture.Present();
            Require(fixture._motion == null && fixture.Opacity == 1 && fixture.ContentScale.ScaleX == 1,
                "Disabled motion still animates entrance.");
            fixture.Dismiss();
            Require(!fixture.IsVisible && fixture._motion == null, "Disabled motion still delays dismissal.");
            fixture.Document.Preferences.Animate = true;
            fixture.Present(); fixture.Dismiss(immediately: true);
            Require(!fixture.IsVisible && fixture._motion == null, "Session dismissal retained an animation.");
            fixture.Present();
            fixture.Document.Preferences.Animate = false; fixture.ApplyAppearance();
            Require(fixture.IsVisible && fixture._motion == null && fixture.Opacity == 1,
                "Disabling motion during entrance did not finish the visible frame.");
            fixture.Document.Preferences.Animate = true;
            fixture.Dismiss();
            fixture.Document.Preferences.Animate = false; fixture.ApplyAppearance();
            Require(!fixture.IsVisible && fixture._motion == null, "Disabling motion during exit did not hide Start.");
            fixture.Document.Preferences.Animate = true;
            fixture.Present(); fixture.Exit();
            Require(fixture._motion == null && fixture._motionTiles.Count == 0, "Shutdown retained motion clocks.");
            return new(5, 4, 5, 5, 1, directory);
        }
        finally
        {
            if (!fixture._exit) fixture.Exit();
            Activate();
        }
    }

    private async Task WaitForMotionAsync()
    {
        var deadline = Environment.TickCount64 + 2000;
        while (_motion != null && Environment.TickCount64 < deadline)
        {
            await Task.Delay(20);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        }
        Require(_motion == null, "The WPF transition clock failed to complete.");
    }

    private void SeekMotion(double milliseconds)
    {
        if (_motion == null) throw new InvalidOperationException("No transition is running.");
        _motion.SeekAlignedToLastTick(this, TimeSpan.FromMilliseconds(milliseconds), TimeSeekOrigin.BeginTime);
    }

    // RenderTargetBitmap excludes HWND opacity. Composite the same alpha over a
    // generated, nonpersonal backdrop after validating the complete root render.
    private void SaveMotionFrame(string path)
    {
        var root = RenderScreenshot();
        var visual = new DrawingVisual();
        var rect = new Rect(0, 0, root.PixelWidth, root.PixelHeight);
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(new LinearGradientBrush(Color.FromRgb(23, 32, 51), Color.FromRgb(36, 63, 86), 45), null, rect);
            drawing.PushOpacity(Opacity);
            drawing.DrawImage(root, rect);
            drawing.Pop();
        }
        var bitmap = new RenderTargetBitmap(root.PixelWidth, root.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        SaveBitmap(path, bitmap);
    }

    private void SaveMotionShowcase(string directory)
    {
        if (!_preview) throw new InvalidOperationException("Motion showcase requires preview mode.");
        var animate = Document.Preferences.Animate;
        var verify = _verifyMotion;
        var frames = Path.Combine(directory, "motion-frames");
        Directory.CreateDirectory(frames);
        try
        {
            _verifyMotion = true;
            Document.Preferences.Animate = true;
            BeginMotion(false, 0, 0.9);
            for (var frame = 0; frame <= 22; frame++)
            {
                SeekMotion(Math.Min(366, frame * 1000d / 60));
                SaveMotionFrame(Path.Combine(frames, $"open-{frame:D3}.png"));
            }
            StopMotion();
            SaveScreenshot(Path.Combine(frames, "open-complete.png"));
            BeginMotion(true);
            for (var frame = 0; frame <= 10; frame++)
            {
                SeekMotion(frame * 1000d / 60);
                SaveMotionFrame(Path.Combine(frames, $"close-{frame:D3}.png"));
            }
        }
        finally
        {
            StopMotion();
            Document.Preferences.Animate = animate;
            _verifyMotion = verify;
        }
    }
}

internal sealed record MotionSmokeResult(int EntranceChecks, int ExitChecks, int InterruptionChecks, int ReducedMotionChecks,
    int ShutdownChecks, string Frames);
