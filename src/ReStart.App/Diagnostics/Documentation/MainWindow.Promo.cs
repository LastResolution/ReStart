using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private SystemMetricsSnapshot? _documentationMetrics;
    private long? _documentationMonotonicMilliseconds;

    /// <summary>Exports a silent English product demonstration from real WPF controls without opening a window.</summary>
    /// <param name="directory">Isolated output directory for the movie, poster, contact sheets and fictional fixture state.</param>
    /// <param name="encoderPath">Absolute path to a trusted local FFmpeg executable; arguments are passed separately.</param>
    internal async Task RenderPromoAsync(string directory, string encoderPath)
    {
        Require(_preview && !IsVisible, "Video rendering requires a hidden preview instance.");
        Require(File.Exists(encoderPath), "FFmpeg is required to export the demonstration.");
        await InitializeAsync(); _clock.Stop(); _startupPreparing = true;
        var localTime = new DateTime(2026, 10, 18, 9, 41, 0, DateTimeKind.Unspecified);
        var sample = new DateTimeOffset(localTime, TimeZoneInfo.Local.GetUtcOffset(localTime));
        _documentationNow = sample;
        _documentationMonotonicMilliseconds = 0;
        var frames = Path.Combine(directory, "review"); Directory.CreateDirectory(frames);
        Process? encoder = null;
        try
        {
            var missing = PreviewApps().Where(app => !_previewAppIcons.Contains(app.Id)).Select(app => app.Name).ToArray();
            Require(missing.Length == 0, "Install the showcase apps before rendering: " + string.Join(", ", missing));
            var tiles = ScriptTilePackages.BundledIds.Take(12).Select(id => _scriptPackages.Import(ScriptTilePackages.BundledManifestPath(id))).ToArray();
            var photos = Path.Combine(directory, "fixture-data", "photos"); Directory.CreateDirectory(photos);
            WriteFixtureImage(Path.Combine(photos, "a.png"), Colors.Teal);
            WriteFixtureImage(Path.Combine(photos, "b.png"), Colors.Coral);
            SlideshowSource.Bind(_scriptPackages.SlideshowBindingPath(tiles[11].ScriptId), photos);
            await SaveLauncherShowcaseAsync(tiles, this, directory);
            Document.Preferences.Theme = ReStart.Core.ThemeMode.Dark;
            Document.Preferences.SearchFiles = false;
            Document.Preferences.Animate = false;
            Root.Height = 900;
            var group = Document.Groups.Single(item => item.Name == "Create & work");
            group.Tiles.RemoveAll(tile => tile.AppId == "calc");
            var moving = group.Tiles.Single(tile => tile.AppId == "notepad");
            moving.Column = 2;
            var movingId = moving.Id;
            var folderId = group.Tiles.Single(tile => tile.IsFolder).Id;
            ApplyAppearance(); ResetToPinnedView(); RenderGroups();
            _layoutHistory.Reset(Document); _historyReady = true;
            Content = null;
            var surface = new AdornerDecorator { Child = Root };
            Content = surface;
            ArrangePromoSurface(surface);
            await WaitForDocumentationFramesAsync();
            var button = _selectionViews[movingId].Button;
            var from = button.TranslatePoint(new(button.ActualWidth / 2, button.ActualHeight / 2), Root);
            var to = new Point(from.X - 2 * Cell, from.Y);
            var initial = CapturePromoSurface(surface);
            var ghost = new CroppedBitmap(initial, new((int)(from.X - button.ActualWidth / 2), (int)(from.Y - button.ActualHeight / 2), (int)button.ActualWidth, (int)button.ActualHeight));
            var logo = new BitmapImage(new Uri("pack://application:,,,/Assets/ReStart.png"));
            var output = Path.Combine(directory, "ReStart-demo.mp4");
            var start = new ProcessStartInfo(encoderPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "-hide_banner", "-loglevel", "error", "-y", "-f", "rawvideo", "-pixel_format", "bgra", "-video_size", "1920x1080", "-framerate", "30", "-i", "pipe:0", "-an", "-c:v", "libx264", "-preset", "fast", "-crf", "21", "-maxrate", "2000k", "-bufsize", "4000k", "-threads", "2", "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-map_metadata", "-1", output }) start.ArgumentList.Add(argument);
            encoder = Process.Start(start) ?? throw new InvalidOperationException("Cannot start FFmpeg.");
            var errors = encoder.StandardError.ReadToEndAsync();
            var pixels = new byte[1920 * 1080 * 4];
            var lastStage = -1;
            var lastQuery = "";
            var snapshot = initial;
            var dragCommitted = false;
            var undone = false;
            for (var frame = 0; frame < 870; frame++)
            {
                var seconds = frame / 30d;
                Require(!IsVisible, "The product demonstration unexpectedly opened a window.");
                var stage = seconds < 2 ? 0 : seconds < 5 ? 1 : seconds < 9 ? 2 : seconds < 12 ? 3 : seconds < 15.5 ? 4 : seconds < 22 ? 5 : seconds < 26 ? 6 : 7;
                if (stage != lastStage)
                {
                    StopMotion(); ClearDropPreview(); StopFolderDwell(); _dragging = false;
                    if (stage == 1)
                    {
                        _verifyMotion = true; Document.Preferences.Animate = true;
                        BeginMotion(false, 0, 0.9);
                    }
                    if (stage == 3) OpenFolder(FindTile(folderId)!);
                    if (stage == 4) { _openFolder = null; RenderGroups(); }
                    if (stage == 5) { ResetToPinnedView(); RenderGroups(); }
                    if (stage == 6)
                    {
                        Document.Preferences.Theme = ReStart.Core.ThemeMode.Light;
                        ApplyAppearance(); RenderGroups();
                    }
                    lastStage = stage;
                }
                if (frame % 15 == 0)
                {
                    _documentationNow = sample.AddSeconds(Math.Floor(seconds));
                    _documentationMonotonicMilliseconds = (long)(seconds * 1000);
                    var cpu = 30 + 13 * Math.Sin(seconds * 1.4) + 5 * Math.Sin(seconds * 4.3);
                    _documentationMetrics = new(_documentationNow.Value, cpu, 16, 32UL << 30, 20UL << 30, 37.5, 91800 + seconds,
                        [new("C:\\", 512L << 30, 214L << 30, 58.2)], 200000 + 180000 * (1 + Math.Sin(seconds)), 40000 + cpu * 600, 74, false);
                    foreach (var runtime in _scriptSessions.Values) runtime.NextUpdate = DateTimeOffset.MinValue;
                    await UpdateScriptTilesAsync(); UpdateLiveTiles();
                }
                Point? cursor = null;
                var dragging = false;
                if (stage == 1)
                {
                    if (seconds < 2.367 && _motion != null) SeekMotion((seconds - 2) * 1000);
                    else StopMotion();
                }
                if (stage == 2)
                {
                    var progress = Math.Clamp((seconds - 5.55) / 1.2, 0, 1);
                    var eased = progress * progress * (3 - 2 * progress);
                    cursor = new(from.X + (to.X - from.X) * eased, from.Y - 24 * Math.Sin(progress * Math.PI));
                    if (seconds >= 5.55 && seconds < 7.1)
                    {
                        dragging = true; _dragging = true; _dragOffset = new(button.ActualWidth / 2, button.ActualHeight / 2);
                        button.Opacity = 0.3;
                        PreviewSingleTileDrop(moving, Root.TranslatePoint(cursor.Value, GroupsPanel), movingId);
                    }
                    else if (seconds >= 7.1 && !dragCommitted)
                    {
                        _dragging = false; button.Opacity = 1;
                        Require(CommitSingleTileDrop(moving, Root.TranslatePoint(to, GroupsPanel), movingId), "The demonstration tile drop was rejected.");
                        Require(FindTile(movingId)?.Column == 0 && FindTile(movingId)?.Row == 0, "The demonstrated move did not reach its visible destination.");
                        dragCommitted = true;
                    }
                    if (seconds >= 8.15 && !undone)
                    {
                        _dragging = false; RestoreLayoutHistory(false); undone = true;
                        Require(FindTile(movingId)?.Column == 2, "Undo did not restore the demonstration tile.");
                    }
                }
                if (stage == 4)
                {
                    var length = Math.Clamp((int)((seconds - 12.3) * 3), 0, 4);
                    var query = "Edge"[..length];
                    if (query != lastQuery) { SetSmokeSearch(query); lastQuery = query; }
                    if (length == 4) Require(SearchResultNames.Contains("Microsoft Edge"), "The demonstration search did not find Edge.");
                }
                if (frame % 3 == 0 || stage == 1 && seconds < 2.4) snapshot = CapturePromoSurface(surface);
                var composed = ComposePromoFrame(seconds, stage, snapshot, logo, cursor, dragging ? ghost : null);
                composed.CopyPixels(pixels, 1920 * 4, 0);
                await encoder.StandardInput.BaseStream.WriteAsync(pixels);
                if (frame % 90 == 0 || frame is 216 or 252 or 405 or 567 or 738 or 834)
                    SaveBitmap(Path.Combine(frames, $"frame-{frame:D4}.png"), composed);
                if (frame == 105) SaveBitmap(Path.Combine(directory, "promo-poster.png"), composed);
                if (frame % 90 == 0) Console.Error.WriteLine($"Rendered {frame / 30}/29 seconds.");
            }
            encoder.StandardInput.Close();
            await encoder.WaitForExitAsync();
            var error = await errors;
            Require(encoder.ExitCode == 0, "FFmpeg failed: " + error);
            Require(new FileInfo(output).Length < 10_000_000, "The video exceeds GitHub's free attachment size limit.");
            File.WriteAllText(Path.Combine(directory, "promo.json"), JsonSerializer.Serialize(new
            {
                width = 1920,
                height = 1080,
                fps = 30,
                duration = 29,
                frames = 870,
                language = "en",
                sampleData = true,
                visibleWindows = 0,
                tileMove = dragCommitted,
                undo = undone,
                source = AppBuildInfo.Build,
                scenes = new[] { "Identity", "Opening", "Tile move and undo", "Inline folder", "App search", "JavaScript live tiles", "Light theme", "Download" }
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            if (encoder is { HasExited: false }) encoder.Kill(entireProcessTree: true);
            encoder?.Dispose(); StopMotion(); ClearDropPreview(); StopFolderDwell();
            _startupPreparing = false; _documentationNow = null; _documentationMetrics = null; _documentationMonotonicMilliseconds = null; _dragging = false;
        }
    }

    private void ArrangePromoSurface(AdornerDecorator surface)
    {
        surface.Measure(new(1920, 900)); surface.Arrange(new Rect(0, 0, 1920, 900));
        FitPanels(); surface.UpdateLayout();
    }

    private BitmapSource CapturePromoSurface(AdornerDecorator surface)
    {
        ArrangePromoSurface(surface);
        var bitmap = new RenderTargetBitmap(1920, 900, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(surface); bitmap.Freeze(); return bitmap;
    }

    private BitmapSource ComposePromoFrame(double seconds, int stage, BitmapSource surface, BitmapSource logo, Point? pointer, BitmapSource? ghost)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(10, 18, 32), Color.FromRgb(22, 38, 58), 30), null, new Rect(0, 0, 1920, 1080));
            dc.PushOpacity(0.13); dc.DrawEllipse(Brushes.DeepSkyBlue, null, new Point(1730, 40), 620, 620); dc.Pop();
            dc.PushOpacity(0.07); dc.DrawEllipse(Brushes.MediumTurquoise, null, new Point(60, 1030), 640, 480); dc.Pop();
            if (stage == 0)
            {
                dc.DrawImage(logo, new Rect(912, 38, 96, 96));
                PromoText(dc, "ReStart", 64, 960, 146, Brushes.White, true, true);
                PromoText(dc, "Full-screen Start. Yours again.", 44, 960, 241, Brushes.White, true);
                PromoText(dc, "Your apps. Your layout. Your own live tiles.", 27, 960, 308, PromoMuted, true);
                PromoText(dc, "WINDOWS 10 & 11  /  PORTABLE  /  OPEN SOURCE", 17, 960, 365, PromoAccent, true);
                dc.PushClip(new RectangleGeometry(new Rect(320, 414, 1280, 600), 12, 12));
                dc.DrawImage(surface, new Rect(320, 414, 1280, 600));
                dc.Pop();
            }
            else if (stage == 7)
            {
                var reveal = Math.Clamp((seconds - 26) * 2.5, 0, 1);
                dc.PushOpacity(reveal);
                dc.DrawImage(logo, new Rect(912, 180 + 20 * (1 - reveal), 96, 96));
                PromoText(dc, "ReStart", 100, 960, 305, Brushes.White, true, true);
                PromoText(dc, "Make room for what matters.", 52, 960, 460, Brushes.White, true);
                PromoText(dc, "Free. Open source. Ready for your desktop.", 28, 960, 550, PromoMuted, true);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(28, 112, 180)), null, new Rect(475, 650, 970, 80), 18, 18);
                PromoText(dc, "github.com/LastResolution/ReStart", 32, 960, 669, Brushes.White, true);
                dc.Pop();
            }
            else
            {
                var (heading, detail) = stage switch
                {
                    1 => ("A familiar place to begin.", "Your apps and live tiles, together in full screen."),
                    2 => (seconds >= 8.15 ? "Try a new layout. Undo in a moment." : "Arrange it your way.", "Drag tiles freely. Keep the layout that feels right."),
                    3 => ("Keep the small things together.", "Folders open right where you need them."),
                    4 => ("Open. Type. Find.", "Start typing to find your apps immediately."),
                    5 => ("Live tiles. Your code.", "JavaScript meets PC time, system metrics and your own data."),
                    _ => ("Your desktop. Your mood.", "Light, dark and custom colors throughout the launcher.")
                };
                dc.DrawImage(logo, new Rect(64, 39, 48, 48));
                PromoText(dc, "ReStart", 23, 127, 47, PromoMuted);
                PromoText(dc, heading, 44, 64, 95, Brushes.White, bold: true);
                PromoText(dc, detail, 22, 1856, 113, PromoMuted, right: true);
                var viewport = new Rect(64, 180, 1792, 840);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)), null, new Rect(58, 185, 1804, 845), 18, 18);
                var crop = stage == 5 ? new Rect(810, 0, 1050, 492.1875) : new Rect(0, 0, 1920, 900);
                if (stage is 2 or 3) crop = new Rect(310, 0, 1200, 562.5);
                dc.PushClip(new RectangleGeometry(viewport, 12, 12));
                dc.PushTransform(new TranslateTransform(viewport.X, viewport.Y));
                dc.PushTransform(new ScaleTransform(viewport.Width / crop.Width, viewport.Height / crop.Height));
                dc.PushTransform(new TranslateTransform(-crop.X, -crop.Y));
                dc.DrawImage(surface, new Rect(0, 0, 1920, 900));
                if (pointer is Point point)
                {
                    if (ghost != null)
                    {
                        dc.PushOpacity(0.9);
                        dc.DrawImage(ghost, new Rect(point.X - ghost.PixelWidth / 2d, point.Y - ghost.PixelHeight / 2d, ghost.PixelWidth, ghost.PixelHeight));
                        dc.Pop();
                    }
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(75, 75, 181, 255)), null, point, ghost == null ? 13 : 19, ghost == null ? 13 : 19);
                    var cursor = Geometry.Parse("M0,0 L0,23 L6,17 L11,28 L16,26 L11,16 L21,16 Z");
                    dc.PushTransform(new TranslateTransform(point.X, point.Y)); dc.DrawGeometry(Brushes.White, new Pen(Brushes.Black, 1), cursor); dc.Pop();
                }
                dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
                if (stage == 5)
                {
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(16, 29, 45)), new Pen(new SolidColorBrush(Color.FromRgb(38, 64, 85)), 1), new Rect(96, 850, 1728, 150), 12, 12);
                    PromoText(dc, "YOUR TILE  /  tile.js", 14, 125, 862, PromoAccent);
                    var code = new Typeface("Consolas");
                    foreach (var (line, y) in new[] { ("function render(ctx) {", 892d), ("  return { text: Math.round(ctx.system.cpuPercent) + \"%\" };", 922d), ("}", 952d) })
                        dc.DrawText(new FormattedText(line, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, code, 22, Brushes.White, 1), new Point(125, y));
                    PromoText(dc, "A little code. A personal dashboard.", 23, 1770, 925, PromoMuted, right: true);
                }
                PromoText(dc, "DEMO LAYOUT  /  SAMPLE DATA", 14, 64, 1044, PromoMuted);
                PromoText(dc, $"0{stage} / 06", 14, 1856, 1044, PromoMuted, right: true);
            }
            dc.DrawRectangle(PromoAccent, null, new Rect(0, 1076, 1920 * seconds / 29, 4));
        }
        var bitmap = new RenderTargetBitmap(1920, 1080, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); return bitmap;
    }

    private static readonly SolidColorBrush PromoMuted = new(Color.FromRgb(170, 193, 213));
    private static readonly SolidColorBrush PromoAccent = new(Color.FromRgb(72, 185, 238));

    private static void PromoText(DrawingContext drawing, string text, double size, double x, double y, Brush color, bool centered = false, bool bold = false, bool right = false)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, color, 1);
        drawing.DrawText(formatted, new Point(x - (centered ? formatted.Width / 2 : right ? formatted.Width : 0), y));
    }
}
