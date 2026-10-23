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
    private bool _documentationSampleConnections;

    /// <summary>Exports a silent English product demonstration from real WPF controls without opening a window.</summary>
    /// <param name="directory">Isolated output directory for the movie, poster, contact sheets and fictional fixture state.</param>
    /// <param name="encoderPath">Absolute path to a trusted local FFmpeg executable; arguments are passed separately.</param>
    internal async Task RenderPromoAsync(string directory, string encoderPath)
    {
        _documentationHtmlRendering = true;
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
            var studioScenes = await RenderStudioPromoScenesAsync(directory);
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
            var logo = new BitmapImage(new Uri("pack://application:,,,/Assets/ReStart.png"));
            var output = Path.Combine(directory, "ReStart-demo.mp4");
            var start = new ProcessStartInfo(encoderPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "-hide_banner", "-loglevel", "error", "-y", "-f", "rawvideo", "-pixel_format", "bgra", "-video_size", "1920x1080", "-framerate", "30", "-i", "pipe:0", "-an", "-c:v", "libx264", "-preset", "fast", "-crf", "21", "-maxrate", "1000k", "-bufsize", "2000k", "-threads", "2", "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-map_metadata", "-1", output }) start.ArgumentList.Add(argument);
            encoder = Process.Start(start) ?? throw new InvalidOperationException("Cannot start FFmpeg.");
            var errors = encoder.StandardError.ReadToEndAsync();
            var pixels = new byte[1920 * 1080 * 4];
            var lastStage = -1;
            var lastQuery = "";
            var snapshot = initial;
            var dragCommitted = false;
            var undone = false;
            var keyboardMoved = false;
            var locked = false;
            var creative = false;
            for (var frame = 0; frame < 1800; frame++)
            {
                var seconds = frame / 30d;
                Require(!IsVisible, "The product demonstration unexpectedly opened a window.");
                var stage = seconds < 3 ? 0 : seconds < 8 ? 1 : seconds < 20 ? 2 : seconds < 25 ? 3 : seconds < 31 ? 4 : seconds < 43 ? 5 : seconds < 55 ? 6 : 7;
                if (stage != lastStage)
                {
                    StopMotion(); StopDragGhost(); ClearDropPreview(); StopFolderDwell(); _dragging = false;
                    if (stage == 1)
                    {
                        _verifyMotion = true; Document.Preferences.Animate = true;
                        BeginMotion(false, 0, 0.9);
                    }
                    if (stage == 3) { SetLayoutLocked(false); SetStatus(""); OpenFolder(FindTile(folderId)!); }
                    if (stage == 4) { _openFolder = null; RenderGroups(); }
                    if (stage == 5) { ResetToPinnedView(); RenderGroups(); }
                    lastStage = stage;
                }
                if (stage == 5 && seconds >= 37 && !creative)
                {
                    await RenderCreativeGalleryAsync(directory); creative = true;
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
                    await Task.WhenAll(_htmlTiles.Values.Select(view => view.FreezeForDocumentationAsync()));
                }
                Point? cursor = null;
                if (stage == 1)
                {
                    if (seconds < 3.367 && _motion != null) SeekMotion((seconds - 3) * 1000);
                    else StopMotion();
                }
                if (stage == 2)
                {
                    var progress = Math.Clamp((seconds - 9) / 2, 0, 1);
                    var eased = progress * progress * (3 - 2 * progress);
                    cursor = new(from.X + (to.X - from.X) * eased, from.Y - 24 * Math.Sin(progress * Math.PI));
                    if (seconds >= 9 && seconds < 12)
                    {
                        _dragging = true; _dragOffset = new(button.ActualWidth / 2, button.ActualHeight / 2);
                        if (_dragGhost == null) StartDragGhost(button, "ReStart.Tile", movingId, from);
                        StopDragFrames();
                        MoveDragGhost(cursor.Value);
                        PreviewSingleTileDrop(moving, Root.TranslatePoint(cursor.Value, GroupsPanel), movingId);
                    }
                    else if (seconds >= 12 && !dragCommitted)
                    {
                        _dragging = false; StopDragGhost();
                        Require(CommitSingleTileDrop(moving, Root.TranslatePoint(to, GroupsPanel), movingId), "The demonstration tile drop was rejected.");
                        Require(FindTile(movingId)?.Column == 0 && FindTile(movingId)?.Row == 0, "The demonstrated move did not reach its visible destination.");
                        dragCommitted = true;
                    }
                    if (seconds >= 14 && !undone)
                    {
                        _dragging = false; RestoreLayoutHistory(false); undone = true;
                        Require(FindTile(movingId)?.Column == 2, "Undo did not restore the demonstration tile.");
                    }
                    if (seconds >= 16 && !keyboardMoved)
                    {
                        SetLayoutEditing(true); ToggleTileSelection(movingId);
                        var plan = KeyboardLayoutEditor.Nudge(Document, [movingId], -1, 0, GroupsPanel.Columns);
                        Require(plan != null, "The demonstrated keyboard nudge was rejected.");
                        Document.Groups = plan!; CommitLayout(); keyboardMoved = true;
                    }
                    if (seconds >= 18 && !locked) { SetLayoutLocked(true); locked = true; }
                }
                if (stage == 4)
                {
                    var length = Math.Clamp((int)((seconds - 25.5) * 3), 0, 4);
                    var query = "Edge"[..length];
                    if (query != lastQuery) { SetSmokeSearch(query); lastQuery = query; }
                    if (length == 4) Require(SearchResultNames.Contains("Microsoft Edge"), "The demonstration search did not find Edge.");
                }
                if (frame % 3 == 0 || stage == 1 && seconds < 3.4) snapshot = CapturePromoSurface(surface);
                var scene = stage == 6 ? studioScenes[seconds < 47 ? 0 : seconds < 51 ? 1 : 2] : snapshot;
                var composed = ComposePromoFrame(seconds, stage, scene, logo, cursor);
                composed.CopyPixels(pixels, 1920 * 4, 0);
                await encoder.StandardInput.BaseStream.WriteAsync(pixels);
                if (frame % 90 == 0 || frame is 216 or 252 or 405 or 567 or 738 or 834)
                    SaveBitmap(Path.Combine(frames, $"frame-{frame:D4}.png"), composed);
                if (frame == 105) SaveBitmap(Path.Combine(directory, "promo-poster.png"), composed);
                if (frame % 90 == 0) Console.Error.WriteLine($"Rendered {frame / 30}/60 seconds.");
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
                duration = 60,
                frames = 1800,
                language = "en",
                sampleData = true,
                visibleWindows = 0,
                tileMove = dragCommitted,
                undo = undone,
                keyboardMove = keyboardMoved,
                layoutLock = locked,
                source = AppBuildInfo.Build,
                scenes = new[] { "Identity", "Opening", "Tile move, undo, keyboard editing and lock", "Inline folder", "App search", "HTML/CSS/SVG live tiles", "Tile Studio in dark and light", "Download" }
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            if (encoder is { HasExited: false }) encoder.Kill(entireProcessTree: true);
            encoder?.Dispose(); StopMotion(); StopDragGhost(); ClearDropPreview(); StopFolderDwell();
            _startupPreparing = false; _documentationNow = null; _documentationMetrics = null; _documentationMonotonicMilliseconds = null; _documentationSampleConnections = false; _dragging = false;
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

    private BitmapSource ComposePromoFrame(double seconds, int stage, BitmapSource surface, BitmapSource logo, Point? pointer)
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
                var reveal = Math.Clamp((seconds - 55) * 2.5, 0, 1);
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
                    2 => (seconds >= 18 ? "Keep it just right. Lock your layout." : seconds >= 16 ? "Move it with the keyboard, too." : seconds >= 14 ? "Try a new layout. Undo in a moment." : "Arrange it your way.", seconds >= 18 ? "Ctrl + Shift + L  /  Lock placement" : seconds >= 16 ? "Ctrl + Arrow  /  Move selected tiles" : "Drag tiles freely. Undo whenever you need."),
                    3 => ("Keep the small things together.", "Folders open right where you need them."),
                    4 => ("Open. Type. Find.", "Start typing to find your apps immediately."),
                    5 => seconds < 37 ? ("HTML. CSS. A little live magic.", "SVG clocks, custom layouts, PC activity and your own data.")
                        : ("Four more ways to make it yours.", "Weather scenes. Real PC gauges. RSS. Canvas art."),
                    6 => ("Create it in Tile Studio.", "Edit your HTML and CSS. Preview all four sizes."),
                    _ => ("Your desktop. Your mood.", "Light, dark and custom colors throughout the launcher.")
                };
                dc.DrawImage(logo, new Rect(64, 39, 48, 48));
                PromoText(dc, "ReStart", 23, 127, 47, PromoMuted);
                PromoText(dc, heading, 44, 64, 95, Brushes.White, bold: true);
                PromoText(dc, detail, 22, 1856, 113, PromoMuted, right: true);
                var viewport = new Rect(64, 180, 1792, 840);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)), null, new Rect(58, 185, 1804, 845), 18, 18);
                var crop = stage == 5 && seconds < 37 ? new Rect(810, 0, 1100, 515.625) : new Rect(0, 0, 1920, 900);
                if (stage is 2 or 3) crop = new Rect(310, 0, 1200, 562.5);
                dc.PushClip(new RectangleGeometry(viewport, 12, 12));
                dc.PushTransform(new TranslateTransform(viewport.X, viewport.Y));
                dc.PushTransform(new ScaleTransform(viewport.Width / crop.Width, viewport.Height / crop.Height));
                dc.PushTransform(new TranslateTransform(-crop.X, -crop.Y));
                dc.DrawImage(surface, new Rect(0, 0, 1920, 900));
                if (pointer is Point point)
                {
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(75, 75, 181, 255)), null, point, _dragGhost == null ? 13 : 19, _dragGhost == null ? 13 : 19);
                    var cursor = Geometry.Parse("M0,0 L0,23 L6,17 L11,28 L16,26 L11,16 L21,16 Z");
                    dc.PushTransform(new TranslateTransform(point.X, point.Y)); dc.DrawGeometry(Brushes.White, new Pen(Brushes.Black, 1), cursor); dc.Pop();
                }
                dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
                if (stage == 5 && seconds < 37)
                {
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(16, 29, 45)), new Pen(new SolidColorBrush(Color.FromRgb(38, 64, 85)), 1), new Rect(96, 850, 1728, 150), 12, 12);
                    PromoText(dc, "YOUR CANVAS  /  HTML + CSS + JAVASCRIPT", 14, 125, 862, PromoAccent);
                    var code = new Typeface("Consolas");
                    foreach (var (line, y) in new[] { (".clock { aspect-ratio: 1; border-radius: 50%; }", 892d), ("// SVG, Canvas, browser APIs. Four fixed tile sizes.", 922d), ("// PC time, optional data connections and metrics.", 952d) })
                        dc.DrawText(new FormattedText(line, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, code, 22, Brushes.White, 1), new Point(125, y));
                    PromoText(dc, "A little code. A personal dashboard.", 23, 1770, 925, PromoMuted, right: true);
                }
                PromoText(dc, "DEMO LAYOUT  /  SAMPLE DATA", 14, 64, 1044, PromoMuted);
                PromoText(dc, $"0{stage} / 06", 14, 1856, 1044, PromoMuted, right: true);
            }
            dc.DrawRectangle(PromoAccent, null, new Rect(0, 1076, 1920 * Math.Min(1, (seconds + 1 / 30d) / 60), 4));
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
