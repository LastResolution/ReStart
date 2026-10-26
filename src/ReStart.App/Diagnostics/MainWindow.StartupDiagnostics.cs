using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    /// <summary>Verifies cold-start presentation, cancellation, native typeahead, initial frames and recoverable icon caching.</summary>
    internal async Task<StartupSmokeResult> VerifyStartupAsync(string snapshotPath)
    {
        if (!_preview) throw new InvalidOperationException("Startup verification requires preview mode.");
        var directory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var store = new LayoutStore(Path.Combine(directory, "layout.json"));
        var packages = new ScriptTilePackages(directory);
        var tiles = ScriptTilePackages.BundledIds.Select(id => packages.Import(ScriptTilePackages.BundledManifestPath(id))).ToList();
        var photo = tiles[ScriptTilePackages.BundledIds.ToList().IndexOf("slideshow")];
        var images = Path.Combine(directory, "images"); Directory.CreateDirectory(images);
        WriteFixtureImage(Path.Combine(images, "sample.png"), Colors.Teal);
        SlideshowSource.Bind(packages.SlideshowBindingPath(photo.ScriptId), images);
        var broken = packages.Import(ScriptTilePackages.BundledManifestPath("clock"));
        File.WriteAllText(Path.Combine(packages.Folder(broken.ScriptId), "tile.js"), "function render() { throw Error('Sample failure'); }");
        tiles.Add(broken);
        store.Save(new() { Preferences = new() { Language = "en", ShowWallpaper = false, Animate = false, Columns = 8 }, Groups = [new() { Name = "Sample tiles", Tiles = tiles }] });
        var catalog = new TaskCompletionSource<List<AppEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var launched = new List<string>();
        var fixture = new MainWindow(store, true, () => catalog.Task, app => launched.Add(app.Id))
        { Owner = this, Width = 1280, Height = 800, Topmost = false, _startupComplete = false };
        var language = L.Language; var checks = 0; var languageCases = 0;
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        async Task Until(Func<bool> done)
        {
            var watch = Stopwatch.StartNew();
            while (!done()) { if (watch.Elapsed > TimeSpan.FromSeconds(25)) throw new InvalidOperationException("Startup fixture timed out."); await Task.Delay(50); }
        }
        try
        {
            fixture.Present();
            Check(!fixture.IsVisible && fixture._startupWindow?.IsVisible == true && fixture._startupWindow.Query.IsKeyboardFocused,
                "Cold startup exposed an unprepared main window or failed to focus splash input.");
            var splash = fixture._startupWindow!;
            splash.UpdateLayout();
            CheckSplashCenter(splash);
            fixture.ComposeFixtureText("E"); fixture.Present();
            Check(fixture._startupWindow == splash && splash.Query.Text == "E", "Repeated activation reset the startup query or opened another splash.");
            foreach (var tag in L.SupportedLanguages)
                foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light, ReStart.Core.ThemeMode.System })
                {
                    fixture.Document.Preferences.Language = tag; fixture.Document.Preferences.Theme = theme;
                    fixture.Document.Preferences.Accent = theme switch { ReStart.Core.ThemeMode.Light => "#D83B01", ReStart.Core.ThemeMode.System => "#008272", _ => "#0078D7" };
                    fixture.ApplyAppearance();
                    splash.UpdateProgress(K.StartupApps, .25); splash.UpdateLayout();
                    CheckSplashCenter(splash);
                    Check(splash.Stage.Text == L.T(K.StartupApps) && splash.Query.Text == "E" && splash.ActualHeight > 200 && splash.ActualHeight < 500,
                        "The localized splash lost its input, progress or bounded layout.");
                    Check(splash.StartupTitle.Text == L.T(K.StartupTitle) && (string)splash.CancelButton.Content == L.T(K.CommonCancel)
                        && splash.Query.Parent is Grid { ActualHeight: 0 } && splash.AppLogo.Source is BitmapSource { PixelWidth: 256, PixelHeight: 256 },
                        "The splash did not retain its translations, hidden typeahead or actual application logo.");
                    using var systemTheme = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    var light = theme == ReStart.Core.ThemeMode.Light || theme == ReStart.Core.ThemeMode.System && systemTheme?.GetValue("SystemUsesLightTheme") is int value && value != 0;
                    var expectedBackground = SystemParameters.HighContrast ? SystemColors.WindowColor : light ? Color.FromRgb(238, 238, 238) : Color.FromRgb(31, 31, 31);
                    var expectedText = SystemParameters.HighContrast ? SystemColors.WindowTextColor : light ? Colors.Black : Colors.White;
                    Check(((SolidColorBrush)((Border)splash.Content).Background).Color == expectedBackground && ((SolidColorBrush)splash.Stage.Foreground).Color == expectedText
                        && ((SolidColorBrush)splash.CancelButton.Foreground).Color == expectedText && ((SolidColorBrush)splash.Progress.Background).Color == (SystemParameters.HighContrast ? SystemColors.HighlightColor : (Color)ColorConverter.ConvertFromString(fixture.Document.Preferences.Accent)),
                        "The splash did not follow saved/system theme colors or the selected accent.");
                    if (tag is "en" or "ja") SaveStartupScreenshot(splash, Path.Combine(Path.GetDirectoryName(snapshotPath)!, $"startup-{tag}-{theme}.png"));
                    languageCases++;
                }
            var initialHeight = splash.ActualHeight;
            splash.Width = 320;
            splash.Stage.Text = string.Join(" ", Enumerable.Repeat("Fictional startup progress with wrapped text.", 8)); splash.UpdateLayout();
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            Check(splash.ActualHeight > initialHeight, "The splash size-change case did not exercise SizeToContent.");
            CheckSplashCenter(splash);
            splash.Width = 440; splash.UpdateProgress(K.StartupReady, 1); splash.UpdateLayout();
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            CheckSplashCenter(splash);
            fixture.Toggle(); Check(fixture._startupWindow == null && !fixture.IsVisible, "Toggling during startup did not cancel presentation.");
            fixture.Present();
            var cancel = fixture._startupWindow!.CancelButton; cancel.Focus();
            var cancelKey = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(cancel)!, Environment.TickCount, Key.Enter)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            cancel.RaiseEvent(cancelKey);
            if (!cancelKey.Handled) { cancelKey.RoutedEvent = Keyboard.KeyDownEvent; cancel.RaiseEvent(cancelKey); }
            Check(fixture._startupWindow == null, "Enter on the focused Cancel button did not cancel startup.");
            catalog.SetResult([new("sample-edge", "Microsoft Edge", ""), new("sample-editor", "Editor sample", "")]);
            await fixture.InitializeAsync(); await Until(() => fixture._startupPresentation == null);
            Check(!fixture.IsVisible && fixture._scriptWorkerPool == null, "Cancelled startup opened later or retained hidden tile workers.");
            fixture.Document.Preferences.Language = "en"; fixture.Document.Preferences.Theme = ReStart.Core.ThemeMode.Dark; fixture.ApplyAppearance();
            fixture.Present(); fixture.ComposeFixtureText("Edge");
            fixture._startupWindow!.Query.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(fixture._startupWindow)!, Environment.TickCount, Key.Enter)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            var pending = fixture._startupPresentation!;
            await pending; fixture.UpdateLayout();
            Check(fixture.IsVisible && fixture._startupWindow == null && fixture._startupComplete && fixture.IconsReady,
                "Startup did not exchange the splash for a fully prepared main window.");
            Check(fixture.ScriptFrameCount == ScriptTilePackages.BundledIds.Count && fixture._scriptAttempts.Contains(broken.Id) && fixture._scriptViews.Single(v => v.Tile.Id == broken.Id).Body.Children.Count > 0,
                "Initial live tiles or a broken tile's error were missing at presentation.");
            Check(fixture._scriptSessions[broken.Id].Attempts is >= 1 and <= 3 && !fixture._liveSessions.Budget(broken.Id).CanRun(Environment.TickCount64),
                "A failed initial frame bypassed the recovery backoff or exceeded its attempt budget.");
            Check(fixture._slideshowImages.ContainsKey(photo.Id) && fixture._scriptViews.All(v => v.Button.ActualWidth > 0),
                "Startup frames were rendered before photo decoding or tile layout.");
            Check(fixture.SearchBox.Text == "Edge" && fixture.SearchBox.IsKeyboardFocused && fixture.SearchResults.SelectedItem is Button && launched.SequenceEqual(new[] { "sample-edge" }),
                "The splash query did not transfer to a visibly selected search result with keyboard focus.");
            fixture.SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "startup-search-ready.png"));
            fixture.Hide(); fixture.Present(); fixture.UpdateLayout();
            Check(fixture._startupWindow == null && fixture.SearchBox.Text.Length == 0 && fixture.IsVisible, "Warm presentation repeated startup preparation or retained search.");
            fixture.SaveScreenshot(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "startup-tiles-ready.png"));
            var deferred = new TaskCompletionSource<List<AppEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var other = new MainWindow(new LayoutStore(Path.Combine(directory, "deactivation", "layout.json")), true, () => deferred.Task)
            { Owner = this, Width = 1280, Height = 800, _startupComplete = false, Topmost = false };
            var foreign = new Window { Title = "Sample application", Width = 220, Height = 120, ShowInTaskbar = false };
            var settingsClosed = 0;
            var expectUpdates = false; var updateChoiceShown = false;
            var closeSettings = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            closeSettings.Tick += (_, _) =>
            {
                foreach (var settings in Application.Current.Windows.OfType<SettingsWindow>().Where(w => w.IsVisible).ToArray())
                {
                    settings.UpdateLayout();
                    if (expectUpdates) updateChoiceShown = VisualChildren<TextBox>(settings).Any(box => box.IsVisible && box.IsReadOnly && box.Text == "Fictional cold-start update notes.");
                    settings.DiscardAndClose(); settingsClosed++;
                }
            };
            closeSettings.Start();
            try
            {
                _ = other.InitializeAsync();
                Check(!other.IsPresenting && other._scriptWorkerPool == null, "Background initialization opened a splash or executed tiles.");
                other.Present(); foreign.Show(); foreign.Activate();
                await Until(() => other._startupWindow == null);
                deferred.SetResult([]); await other.InitializeAsync(); await Until(() => other._startupPresentation == null);
                Check(!other.IsPresenting && !other.IsVisible, "A foreign activation during startup caused a late popup.");
                other.PresentSettings();
                if (other._startupPresentation is { } settingsPreparation) await settingsPreparation;
                await Until(() => other.IsVisible && settingsClosed == 1 && !other._startupSettings);
                Check(other.IsVisible && other._startupWindow == null && settingsClosed == 1 && !other._startupSettings,
                    "Background initialization did not prepare the default live tiles and open requested settings exactly once.");
            }
            finally { deferred.TrySetResult([]); other.Exit(); foreign.Close(); fixture.Activate(); }
            var cold = new MainWindow(store, true, () => Task.FromResult(new List<AppEntry>()))
            { Owner = this, Width = 1280, Height = 800, _startupComplete = false, Topmost = false };
            try
            {
                expectUpdates = true;
                cold.LastUpdateCheck = new(UpdateCheckStatus.Available, new("v9.0.0", ReleaseVersion.Parse("9.0.0")!, "Fictional cold-start update notes."));
                cold.ReviewAvailableUpdate();
                Check(cold._scriptWorkerPool == null && cold._startupWorkerWarmup == Task.CompletedTask && !cold.IsVisible,
                    "Web-only startup unnecessarily created a native worker warmup or exposed an unprepared window.");
                await cold._startupPresentation!;
                Check(cold.IsVisible && cold.ScriptFrameCount == ScriptTilePackages.BundledIds.Count && cold._startupWindow == null && settingsClosed == 2 && !cold._startupSettings,
                    $"Parallel worker warmup did not produce all frames or open pending settings: visible={cold.IsVisible}, frames={cold.ScriptFrameCount}, settings={settingsClosed}, pending={cold._startupSettings}.");
                Check(updateChoiceShown && cold.UpdatePreparation == PreparationState.Idle, "Cold preparation lost the update destination or started an unrequested download.");
            }
            finally { closeSettings.Stop(); cold.Exit(); fixture.Activate(); }
            var cacheChecks = VerifyIconCache(directory);
            return new(checks, languageCases, cacheChecks, fixture.StartupMetrics);

            void CheckSplashCenter(StartupWindow window)
            {
                var area = System.Windows.Forms.Screen.FromPoint(fixture._startupPoint).WorkingArea;
                Check(PlatformWindowRect(new System.Windows.Interop.WindowInteropHelper(window).Handle, out var bounds)
                    && Math.Abs((bounds.Left + bounds.Right) / 2d - (area.Left + area.Width / 2d)) <= 1
                    && Math.Abs((bounds.Top + bounds.Bottom) / 2d - (area.Top + area.Height / 2d)) <= 1,
                    $"The splash is not centered in its monitor working area: bounds={bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom}; area={area}.");
            }
        }
        finally { fixture.Exit(); L.SetLanguage(language); ApplyAppearance(); Activate(); }
    }

    private static int VerifyIconCache(string directory)
    {
        var source = Path.Combine(directory, "icon-source.txt"); File.WriteAllText(source, "Sample source");
        var cacheDirectory = Path.Combine(directory, "cache-fixture"); var cache = new IconCache(cacheDirectory);
        var reads = 0;
        ImageSource Read(string _) { reads++; var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 20, 60, 100, 255 }, 4); bitmap.Freeze(); return bitmap; }
        Require(cache.Load(source, Read) != null && reads == 1, "A cache miss did not read the Shell icon.");
        Require(cache.Load(source, Read)!.IsFrozen && reads == 1 && cache.Hits == 1, "A warm cache did not return frozen pixels without another Shell read.");
        var path = Directory.GetFiles(cacheDirectory, "*.png").Single();
        File.WriteAllText(path, "Damaged cache"); cache.Load(source, Read);
        Require(reads == 2, "A damaged cache did not recover through the Shell reader.");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2)); cache.Load(source, Read);
        Require(reads == 3, "An expired icon cache entry did not refresh.");
        File.AppendAllText(source, " changed"); cache.Load(source, Read);
        Require(reads == 4 && Directory.GetFiles(cacheDirectory, "*.png").All(p => Path.GetFileNameWithoutExtension(p).Length == 64),
            "A changed file reused stale pixels or cache names exposed local identities.");
        return 5;
    }

    private static void SaveStartupScreenshot(Window window, string path)
        => SaveFixtureElement(window, path);
}

internal sealed record StartupSmokeResult(int Checks, int LanguageThemeCases, int CacheChecks, object Timings);
