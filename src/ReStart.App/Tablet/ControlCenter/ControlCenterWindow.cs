using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ReStart.App.Services;
using ReStart.App.Tablet.Design;
using ReStart.App.Tablet.Media;

namespace ReStart.App.Tablet.ControlCenter;

/// <summary>
/// Control Center in Home's tile language: a full-screen topmost window over a frosted snapshot
/// of the screen, with a 4-column grid of square tiles (Home's 8 DIP gap, accent when on, labels
/// bottom-left like Home's tiles) pinned to the top-right where the gesture starts.
/// Dismissed by a tap outside the modules, a swipe up, Esc, losing focus or CloseOverlays.
/// </summary>
internal sealed class ControlCenterWindow : Window
{
    private const double Cell = 90, Gap = TabletStyle.Gap, GridWidth = 4 * Cell + 3 * Gap, GridHeight = 7 * Cell + 6 * Gap;
    private readonly Grid _root = new() { Background = Brushes.Transparent };
    private readonly Image _sharp = new() { Stretch = Stretch.Fill };
    private readonly Image _blurred = new() { Stretch = Stretch.Fill };
    private readonly Border _scrim = new() { Background = new SolidColorBrush(Colors.Transparent) };
    private readonly Grid _backdrop = new();
    private readonly Canvas _panel = new() { Width = GridWidth, Height = GridHeight, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly TranslateTransform _move = new();
    private readonly Grid _status = new() { VerticalAlignment = VerticalAlignment.Top, Height = 32, Margin = new Thickness(32, 14, 32, 0), IsHitTestVisible = false };
    private readonly TextBlock _clock = Ui.Label(16, FontWeights.SemiBold), _date = Ui.Label(16, FontWeights.Normal), _statusPercent = Ui.Label(15, FontWeights.SemiBold);
    private readonly TextBlock _statusBattery = Ui.Glyph("", 20), _statusWifi = Ui.Glyph("", 16);
    private readonly SystemAudio _audio = new();
    private readonly SystemRadios _radios = new();
    private readonly DispatcherTimer _tick;
    private readonly List<Action<bool>> _themed = new(); // re-applies colors; argument = animate
    private Palette _palette = Palette.For(true);
    private bool _open, _closing, _wasActive;
    private int _generation, _ticks;
    private nint _handle;
    private readonly FrameMeter _frames = new("cc");
    private TimeSpan _lastFrame;

    private void CountFrame(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs r || r.RenderingTime == _lastFrame) return;
        var dt = _lastFrame == TimeSpan.Zero ? 1 / 60.0 : (r.RenderingTime - _lastFrame).TotalSeconds;
        _lastFrame = r.RenderingTime;
        _frames.Frame(Math.Min(dt, 0.25));
    }

    // Modules
    private readonly Row _airplane, _wifi, _bluetooth;
    private readonly Tile _rotation, _theme, _refresh, _keyboard, _screenshot, _lock, _screenOff, _settings, _desktop;
    private readonly PillSlider _brightness = new(), _volume = new();
    private readonly Border _performance = Ui.Platter(), _battery = Ui.Platter(), _connectivity = Ui.Platter(), _media = Ui.Platter();
    private readonly Border _perfThumb = new() { Background = new SolidColorBrush(Colors.White), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TranslateTransform _perfThumbMove = new();
    private readonly (TextBlock Glyph, TextBlock Label, int Mode, Color Color)[] _perfSegments;
    private readonly TextBlock _batteryPercent = Ui.Label(30, FontWeights.SemiBold), _batteryDetail = Ui.Label(12.5, FontWeights.Normal), _batteryGlyph = Ui.Glyph("", 30);
    private readonly Border _art = new() { Width = 60, Height = 60, Background = new SolidColorBrush(Colors.Gray) };
    private readonly TextBlock _artGlyph = Ui.Glyph("", 26), _mediaTitle = Ui.Label(15, FontWeights.SemiBold), _mediaArtist = Ui.Label(13, FontWeights.Normal);
    private readonly TextBlock _prev = Ui.Glyph("", 24), _play = Ui.Glyph("", 32), _next = Ui.Glyph("", 24);
    private readonly Grid _transport = new();
    private int? _perfMode, _perfPending;
    private long _perfPendingUntil, _brightnessQuietUntil, _refreshPendingUntil;
    private (int Current, int Max, bool Has60) _rate;
    private string? _mediaKey;

    // Gesture state (swipe up to dismiss)
    private Point _pressAt;
    private bool _pressed, _dragging, _gestureBlocked, _backgroundPress;
    private readonly List<(long Time, double Y)> _samples = new();

    internal ControlCenterWindow()
    {
        Title = "Control Center";
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
        Background = Brushes.Black; FontFamily = Ui.Text; UseLayoutRounding = true; SnapsToDevicePixels = true;
        Left = -32000; Top = -32000; Width = 1; Height = 1;
        Stylus.SetIsPressAndHoldEnabled(this, false); Stylus.SetIsFlicksEnabled(this, false);
        Stylus.SetIsTapFeedbackEnabled(this, false); Stylus.SetIsTouchFeedbackEnabled(this, false);
        RenderOptions.SetBitmapScalingMode(_blurred, BitmapScalingMode.Linear);

        _backdrop.Children.Add(_blurred); _backdrop.Children.Add(_scrim);
        _panel.RenderTransform = new TransformGroup { Children = { _zoom, _move } };
        _panel.RenderTransformOrigin = new Point(1, 0);
        BuildStatus();

        _airplane = new Row(this, "", "Airplane Mode", Palette.Orange, ToggleAirplane);
        _wifi = new Row(this, "", "Wi-Fi", Palette.Blue, () => _ = ToggleWifiAsync());
        _bluetooth = new Row(this, "", "Bluetooth", Palette.Blue, () => _ = ToggleBluetoothAsync());
        var rows = new StackPanel { Margin = new Thickness(14, 10, 10, 10), VerticalAlignment = VerticalAlignment.Center };
        rows.Children.Add(_airplane.View); rows.Children.Add(_wifi.View); rows.Children.Add(_bluetooth.View);
        _connectivity.Child = rows;
        Place(_connectivity, 0, 0, 2, 2);
        BuildMedia(); Place(_media, 2, 0, 2, 2);

        _rotation = new Tile(this, "", "Rotation Lock", Palette.Red, ToggleRotation); Place(_rotation.View, 0, 2);
        _theme = new Tile(this, "", "Dark Mode", Palette.Indigo, ToggleTheme); Place(_theme.View, 1, 2);
        _refresh = new Tile(this, "", "Refresh Rate", Palette.Blue, ToggleRefresh, big: true); Place(_refresh.View, 0, 3);
        _keyboard = new Tile(this, "", "Keyboard", Palette.Blue, () => Dismiss(() => _ = SystemActions.ToggleTouchKeyboardAsync())); Place(_keyboard.View, 1, 3);
        _brightness.SetGlyph(""); Place(_brightness, 2, 2, 1, 2);
        _volume.SetGlyph(""); Place(_volume, 3, 2, 1, 2);
        _brightness.ValueChanged += value => { _brightnessQuietUntil = Environment.TickCount64 + 1500; Brightness.Set((int)Math.Round(value * 100)); };
        _volume.ValueChanged += value => { _audio.SetVolume((float)value); UpdateVolumeGlyph(); };
        _volume.GlyphTapped += () => { _audio.SetMuted(!_audio.Muted); UpdateVolume(); };
        _themed.Add(animate => { _brightness.Apply(_palette, animate); _volume.Apply(_palette, animate); });

        _perfSegments = [(Ui.Glyph("", 22), Ui.Label(11.5, FontWeights.SemiBold), GHelper.Silent, Palette.Green),
            (Ui.Glyph("", 22), Ui.Label(11.5, FontWeights.SemiBold), GHelper.Balanced, Palette.Blue),
            (Ui.Glyph("", 22), Ui.Label(11.5, FontWeights.SemiBold), GHelper.Turbo, Palette.Orange)];
        BuildPerformance(); Place(_performance, 0, 4, 2, 1);
        BuildBattery(); Place(_battery, 2, 4, 2, 1);

        _screenshot = new Tile(this, "", "Screenshot", Palette.Blue, () => Dismiss(SystemActions.Screenshot, 150)); Place(_screenshot.View, 0, 5);
        _lock = new Tile(this, "", "Lock", Palette.Blue, () => Dismiss(SystemActions.Lock)); Place(_lock.View, 1, 5);
        _screenOff = new Tile(this, "", "Screen Off", Palette.Blue, () => Dismiss(SystemActions.ScreenOff, 500)); Place(_screenOff.View, 2, 5);
        _settings = new Tile(this, "", "Settings", Palette.Blue, () => Dismiss(SystemActions.Settings)); Place(_settings.View, 3, 5);
        // Tablet mode has no Windows desktop of its own; this opens a desktop session until the next Home.
        _desktop = new Tile(this, "\uE7F4", "Show Desktop", Palette.Blue, () => Dismiss(() => TabletShell.ShowDesktop(), 150)); Place(_desktop.View, 0, 6, 2, 1);
        _themed.Add(animate =>
        {
            foreach (var platter in new[] { _connectivity, _media, _performance, _battery }) Ui.Tint(platter.Background, _palette.Platter, animate);
        });

        _root.Children.Add(_sharp); _root.Children.Add(_backdrop); _root.Children.Add(_status); _root.Children.Add(_panel);
        Content = _root;

        _tick = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Ui.Safe("tick", Tick), Dispatcher);
        _audio.ExternalChange += () => Dispatcher.BeginInvoke(() => Ui.Safe("volume", UpdateVolume));
        _radios.Changed += () => Dispatcher.BeginInvoke(() => { if (_open) Ui.Safe("radios", UpdateRadios); });
        MediaSessions.Shared.Changed += _ => Dispatcher.BeginInvoke(() => { if (_open) Ui.Safe("media", UpdateMedia); });

        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            int on = 1, square = 1;
            DwmSetWindowAttribute(_handle, 3 /* DWMWA_TRANSITIONS_FORCEDISABLED */, ref on, sizeof(int)); // our own motion only
            DwmSetWindowAttribute(_handle, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref square, sizeof(int)); // DWMWCP_DONOTROUND
            // Tool window: never in Alt+Tab, never an "app" for the tablet window manager.
            SetWindowLongPtr(_handle, -20, (nint)(GetWindowLongPtr(_handle, -20).ToInt64() | 0x80 /* WS_EX_TOOLWINDOW */));
        };
        Activated += (_, _) => _wasActive = true;
        Deactivated += (_, _) => { if (_open && !_closing && _wasActive) Dismiss(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Dismiss(); e.Handled = true; }
            // Synthesized G-Helper hotkeys include Alt; never let it start Win32 menu mode here.
            if (e.Key == Key.System) e.Handled = true;
        };
        PreviewKeyUp += (_, e) => { if (e.Key == Key.System) e.Handled = true; };
        _root.PreviewMouseLeftButtonDown += OnPress;
        _root.PreviewMouseMove += OnDrag;
        _root.PreviewMouseLeftButtonUp += OnRelease;
        // Reached only when no module handled the press; disabled modules pass presses through, so test their bounds too.
        _root.MouseLeftButtonDown += (_, e) => _backgroundPress = !HitsModule(e.GetPosition(_panel));
        _root.LostMouseCapture += (_, _) => { if (_dragging) EndDrag(0); };
        _ = _radios.StartAsync();
        _ = MediaSessions.Shared.StartAsync();
    }

    /// <summary>Diagnostics: saves what DWM shows and what WPF renders, and logs the animated values.</summary>
    internal void DebugSnapshot(string directory)
    {
        var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        if (Backdrop.Capture(bounds) is { } screen) Save(screen.Sharp, Path.Combine(directory, "cc-screen.png"));
        var dpi = VisualTreeHelper.GetDpi(this);
        var target = new RenderTargetBitmap((int)(ActualWidth * dpi.DpiScaleX / 2), (int)(ActualHeight * dpi.DpiScaleY / 2), dpi.PixelsPerInchX / 2, dpi.PixelsPerInchY / 2, PixelFormats.Pbgra32);
        target.Render(_root);
        Save(target, Path.Combine(directory, "cc-wpf.png"));
        TabletLog.Write("cc", $"debug: panel={_panel.Opacity:0.00} backdrop={_backdrop.Opacity:0.00} zoom={_zoom.ScaleX:0.00} move={_move.Y:0.0} size={ActualWidth}x{ActualHeight}");
        static void Save(BitmapSource source, string path)
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
            using var file = File.Create(path); encoder.Save(file);
        }
    }

    /// <summary>Whether Control Center is showing (or animating out).</summary>
    internal bool IsOpen => _open;

    /// <summary>Builds the native window and visual tree ahead of the first swipe.</summary>
    internal void Warm()
    {
        new WindowInteropHelper(this).EnsureHandle();
        ApplyPalette(false);
        _root.Measure(new Size(1280, 800)); _root.Arrange(new Rect(0, 0, 1280, 800));
    }

    // ---------------------------------------------------------------- open / close

    /// <summary>Shows Control Center with the open animation.</summary>
    internal void Open()
    {
        if (_open && !_closing) { AppWindows.SetForeground(_handle); return; }
        var generation = ++_generation;
        var reopening = _open && _closing;
        _closing = false;
        if (!reopening)
        {
            TabletShell.CloseOverlays(); // dock / switcher go away first so they are not in the snapshot
            var screen = System.Windows.Forms.Screen.PrimaryScreen!;
            var bounds = screen.Bounds;
            var shot = Backdrop.Capture(bounds);
            _sharp.Source = shot?.Sharp; _blurred.Source = shot?.Blurred;
            _palette = Palette.For(SystemTheme.IsDark);
            ApplyPalette(false);
            _open = true; _wasActive = false; _ticks = 0;
            _audio.Attach();
            RefreshAll();
            _backdrop.Opacity = 0; _status.Opacity = 0; _panel.Opacity = 0; _zoom.ScaleX = _zoom.ScaleY = 0.96; _move.Y = -24; _move.X = 0;
            var handle = new WindowInteropHelper(this).EnsureHandle();
            Cloak(handle, true); // keep DWM from showing a frame before WPF has rendered one
            Show();
            SetWindowPos(handle, -1 /* HWND_TOPMOST */, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0040 /* SWP_SHOWWINDOW */);
            FitPanel(new Size(bounds.Width / VisualTreeHelper.GetDpi(this).DpiScaleX, bounds.Height / VisualTreeHelper.GetDpi(this).DpiScaleY));
            var started = Environment.TickCount64;
            AfterFrames(2, () =>
            {
                if (generation != _generation) return;
                Cloak(handle, false);
                AnimateIn();
                DwmGetWindowAttribute(handle, 14 /* DWMWA_CLOAKED */, out var cloaked, sizeof(int));
                GetWindowRect(handle, out var rect);
                TabletLog.Write("cc", $"shown after {Environment.TickCount64 - started} ms, cloaked={cloaked} visible={IsWindowVisible(handle)} rect={rect.Left},{rect.Top},{rect.Right},{rect.Bottom} fg={AppWindows.GetForegroundWindow() == handle}");
            });
            ForegroundActivation.Request(handle);
            Activate();
            TabletLog.Write("cc", $"open {bounds.Width}x{bounds.Height}{(shot == null ? " (no backdrop)" : "")}");
        }
        else AnimateIn();
        _tick.Start();
    }

    private void AnimateIn()
    {
        // The shared tablet motion: panels arrive on the Fluent decelerate curve, without a bounce.
        _frames.Begin("open");
        CompositionTarget.Rendering -= CountFrame; CompositionTarget.Rendering += CountFrame;
        Run(_backdrop, OpacityProperty, 1, TabletMotion.Enter, TabletMotion.Decelerate);
        Run(_status, OpacityProperty, 1, TabletMotion.Enter, TabletMotion.Decelerate);
        Run(_panel, OpacityProperty, 1, TabletMotion.Short, TabletMotion.Decelerate);
        Run(_zoom, ScaleTransform.ScaleXProperty, 1, TabletMotion.Enter, TabletMotion.Decelerate);
        Run(_zoom, ScaleTransform.ScaleYProperty, 1, TabletMotion.Enter, TabletMotion.Decelerate);
        Run(_move, TranslateTransform.YProperty, 0, TabletMotion.Enter, TabletMotion.Decelerate, () => { CompositionTarget.Rendering -= CountFrame; _frames.End(); });
    }

    /// <summary>Hides Control Center; <paramref name="then"/> runs after it is off screen (plus <paramref name="delayMs"/>).</summary>
    internal void Dismiss(Action? then = null, int delayMs = 0)
    {
        if (!_open || _closing) { then?.Invoke(); return; }
        _closing = true; _tick.Stop();
        var generation = _generation;
        var ease = TabletMotion.Accelerate;
        Run(_backdrop, OpacityProperty, 0, TabletMotion.Exit + 40, ease);
        Run(_status, OpacityProperty, 0, TabletMotion.Exit, ease);
        Run(_panel, OpacityProperty, 0, TabletMotion.Exit, ease);
        Run(_zoom, ScaleTransform.ScaleXProperty, 0.97, TabletMotion.Exit + 40, ease);
        Run(_zoom, ScaleTransform.ScaleYProperty, 0.97, TabletMotion.Exit + 40, ease);
        Run(_move, TranslateTransform.YProperty, Math.Min(_move.Y, 0) - 20, TabletMotion.Exit + 40, ease, () =>
        {
            if (generation != _generation || !_closing) return;
            Hide();
            _open = false; _closing = false;
            _sharp.Source = null; _blurred.Source = null; // release the 16 MB snapshot
            TabletLog.Write("cc", "closed");
            if (then == null) return;
            if (delayMs <= 0) Ui.Safe("after close", then);
            else new DispatcherTimer(TimeSpan.FromMilliseconds(delayMs), DispatcherPriority.Normal, (s, _) => { ((DispatcherTimer)s!).Stop(); Ui.Safe("after close", then); }, Dispatcher).Start();
        });
    }

    private void FitPanel(Size size)
    {
        // Pin to the top-right like iPadOS; shrink only if a short screen cannot fit the grid.
        var scale = Math.Min(1, (size.Height - 64 - 20) / GridHeight);
        _panel.LayoutTransform = scale < 1 ? new ScaleTransform(scale, scale) : Transform.Identity;
        _panel.Margin = new Thickness(0, 60, size.Width < 900 ? 24 : 32, 0);
    }

    private void AfterFrames(int frames, Action action)
    {
        EventHandler? handler = null;
        handler = (_, _) => { if (--frames > 0) return; CompositionTarget.Rendering -= handler; Ui.Safe("first frame", action); };
        CompositionTarget.Rendering += handler;
    }

    private static void Run(IAnimatable target, DependencyProperty property, double to, double ms, IEasingFunction? ease, Action? done = null)
    {
        var element = (DependencyObject)target;
        var from = (double)element.GetValue(property);
        element.SetValue(property, to); // the base value matches the end, so removing the clock never jumps
        var animation = TabletMotion.FromTo(from, to, (int)ms, ease);
        if (done != null) animation.Completed += (_, _) => done();
        target.BeginAnimation(property, animation);
    }

    private static void Hold(IAnimatable target, DependencyProperty property)
    {
        var element = (DependencyObject)target;
        var current = (double)element.GetValue(property);
        target.BeginAnimation(property, null);
        element.SetValue(property, current);
    }

    // ---------------------------------------------------------------- swipe up / tap outside

    private void OnPress(object sender, MouseButtonEventArgs e)
    {
        _pressAt = e.GetPosition(_root); _pressed = true; _dragging = false; _backgroundPress = false;
        _gestureBlocked = IsInside<PillSlider>(e.OriginalSource as DependencyObject);
        _samples.Clear(); _samples.Add((Environment.TickCount64, _pressAt.Y));
    }

    private void OnDrag(object sender, MouseEventArgs e)
    {
        if (!_pressed || _gestureBlocked || _closing || e.LeftButton != MouseButtonState.Pressed) return;
        var at = e.GetPosition(_root);
        var dy = at.Y - _pressAt.Y;
        _samples.Add((Environment.TickCount64, at.Y));
        if (_samples.Count > 8) _samples.RemoveAt(0);
        if (!_dragging)
        {
            if (dy > -14 || Math.Abs(dy) < Math.Abs(at.X - _pressAt.X) * 1.2) return;
            _dragging = true;
            foreach (var (target, property) in Animated()) Hold(target, property);
            _root.CaptureMouse(); // takes the press away from the tile under the finger
        }
        var pull = Math.Min(0, dy);
        _move.Y = pull;
        _panel.Opacity = Math.Clamp(1 + pull / 320, 0, 1);
        _backdrop.Opacity = Math.Clamp(1 + pull / 520, 0, 1);
        _status.Opacity = _panel.Opacity;
    }

    private void OnRelease(object sender, MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            var (t0, y0) = _samples[0]; var (t1, y1) = _samples[^1];
            var velocity = t1 > t0 ? (y1 - y0) / (t1 - t0) : 0; // DIP per ms, negative = up
            _root.ReleaseMouseCapture();
            EndDrag(velocity);
            e.Handled = true;
        }
        else if (_pressed && _backgroundPress && !_closing && (e.GetPosition(_root) - _pressAt).Length < 20) Dismiss();
        _pressed = false; _backgroundPress = false;
    }

    private void EndDrag(double velocity)
    {
        if (!_dragging) return;
        _dragging = false; _pressed = false;
        if (_move.Y < -70 || velocity < -0.45) { Dismiss(); return; }
        Run(_move, TranslateTransform.YProperty, 0, TabletMotion.Enter, TabletMotion.Decelerate);
        Run(_panel, OpacityProperty, 1, TabletMotion.Short, TabletMotion.Decelerate);
        Run(_status, OpacityProperty, 1, TabletMotion.Short, TabletMotion.Decelerate);
        Run(_backdrop, OpacityProperty, 1, TabletMotion.Short, TabletMotion.Decelerate);
    }

    private IEnumerable<(IAnimatable, DependencyProperty)> Animated() =>
    [
        (_move, TranslateTransform.YProperty), (_zoom, ScaleTransform.ScaleXProperty), (_zoom, ScaleTransform.ScaleYProperty),
        (_panel, OpacityProperty), (_backdrop, OpacityProperty), (_status, OpacityProperty),
    ];

    private bool HitsModule(Point point)
    {
        foreach (FrameworkElement module in _panel.Children)
            if (module.Visibility == Visibility.Visible && new Rect(Canvas.GetLeft(module), Canvas.GetTop(module), module.Width, module.Height).Contains(point)) return true;
        return false;
    }

    private static bool IsInside<T>(DependencyObject? element) where T : DependencyObject
    {
        for (var e = element; e != null; e = e is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(e) : LogicalTreeHelper.GetParent(e))
            if (e is T) return true;
        return false;
    }

    // ---------------------------------------------------------------- layout helpers

    private void Place(FrameworkElement element, int column, int row, int columns = 1, int rows = 1)
    {
        element.Width = columns * Cell + (columns - 1) * Gap;
        element.Height = rows * Cell + (rows - 1) * Gap;
        Canvas.SetLeft(element, column * (Cell + Gap));
        Canvas.SetTop(element, row * (Cell + Gap));
        if (!_panel.Children.Contains(element)) _panel.Children.Add(element);
    }

    private void ApplyPalette(bool animate)
    {
        Ui.Tint(_scrim.Background, _palette.Scrim, animate);
        foreach (var text in new[] { _clock, _date, _statusPercent, _statusWifi }) Ui.Tint(text, _palette.Text, animate);
        foreach (var apply in _themed) apply(animate);
        UpdateBattery();
    }

    private void BuildStatus()
    {
        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _date.Margin = new Thickness(10, 0, 0, 0);
        left.Children.Add(_clock); left.Children.Add(_date);
        var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        _statusWifi.Margin = new Thickness(0, 0, 14, 0); _statusPercent.Margin = new Thickness(0, 0, 6, 0); _statusPercent.VerticalAlignment = VerticalAlignment.Center;
        right.Children.Add(_statusWifi); right.Children.Add(_statusPercent); right.Children.Add(_statusBattery);
        _status.Children.Add(left); _status.Children.Add(right);
    }

    private void BuildMedia()
    {
        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        _art.Child = _artGlyph;
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
        titles.Children.Add(_mediaTitle); titles.Children.Add(_mediaArtist);
        _mediaArtist.Margin = new Thickness(0, 2, 0, 0);
        var top = new StackPanel();
        top.Children.Add(new Border { Child = _art, HorizontalAlignment = HorizontalAlignment.Left });
        top.Children.Add(titles);
        grid.Children.Add(top);
        foreach (var _ in new[] { 0, 1, 2 }) _transport.ColumnDefinitions.Add(new ColumnDefinition());
        _transport.VerticalAlignment = VerticalAlignment.Bottom; _transport.Margin = new Thickness(-8, 0, -8, -8);
        AddTransport(_prev, 0, () => _ = MediaSessions.Shared.PreviousAsync());
        AddTransport(_play, 1, () => _ = MediaSessions.Shared.PlayPauseAsync());
        AddTransport(_next, 2, () => _ = MediaSessions.Shared.NextAsync());
        Grid.SetRow(_transport, 1);
        grid.Children.Add(_transport);
        _media.Child = grid;
        _themed.Add(animate =>
        {
            Ui.Tint(_mediaTitle, _palette.Text, animate); Ui.Tint(_mediaArtist, _palette.Secondary, animate);
            Ui.Tint(_artGlyph, _palette.Secondary, animate);
            foreach (var glyph in new[] { _prev, _play, _next }) Ui.Tint(glyph, _palette.Text, animate);
            if (_mediaKey == null) Ui.Tint(_art.Background, _palette.ToggleOff, animate);
        });
    }

    private void AddTransport(TextBlock glyph, int column, Action action)
    {
        var hit = new Border { Background = Brushes.Transparent, Height = 52, Child = glyph };
        Ui.Tappable(hit, () => { if (MediaSessions.Shared.Current != null) action(); }, 0.82);
        Grid.SetColumn(hit, column);
        _transport.Children.Add(hit);
    }

    private void BuildPerformance()
    {
        var grid = new Grid { Margin = new Thickness(6) };
        _perfThumb.RenderTransform = _perfThumbMove;
        grid.Children.Add(_perfThumb);
        var segments = new Grid();
        for (var i = 0; i < _perfSegments.Length; i++)
        {
            segments.ColumnDefinitions.Add(new ColumnDefinition());
            var (glyph, label, mode, _) = _perfSegments[i];
            label.Text = mode switch { GHelper.Silent => "Silent", GHelper.Turbo => "Turbo", _ => "Balanced" };
            label.HorizontalAlignment = HorizontalAlignment.Center; label.Margin = new Thickness(0, 4, 0, 0);
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(glyph); stack.Children.Add(label);
            var hit = new Border { Background = Brushes.Transparent, Child = stack };
            Ui.Tappable(hit, () => SetPerformance(mode), 0.9);
            Grid.SetColumn(hit, i);
            segments.Children.Add(hit);
        }
        grid.Children.Add(segments);
        grid.SizeChanged += (_, _) => { _perfThumb.Width = grid.ActualWidth / 3; PlacePerfThumb(false); };
        _performance.Child = grid;
        _themed.Add(animate => { Ui.Tint(_perfThumb.Background, _palette.Active, animate); UpdatePerformance(animate); });
    }

    private void BuildBattery()
    {
        var grid = new Grid { Margin = new Thickness(18, 10, 16, 12) };
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _batteryPercent.FontFamily = Ui.Display; _batteryPercent.FontWeight = FontWeights.Light;
        text.Children.Add(_batteryPercent); text.Children.Add(_batteryDetail);
        _batteryGlyph.HorizontalAlignment = HorizontalAlignment.Right;
        grid.Children.Add(text); grid.Children.Add(_batteryGlyph);
        _battery.Child = grid;
    }

    // ---------------------------------------------------------------- state

    private void Tick()
    {
        if (!_open || _closing) return;
        _ticks++;
        UpdateClock(); UpdateBattery();
        if (_ticks % 2 == 0) { _ = UpdateBrightnessAsync(); UpdatePerformance(true); UpdateRefresh(); UpdateRotation(); UpdateRadios(); UpdateVolume(); }
    }

    private void RefreshAll()
    {
        UpdateClock(); UpdateBattery(); UpdateRadios(); UpdateMedia(); UpdateVolume(); UpdateRotation(); UpdateRefresh(); UpdateTheme();
        _perfPending = null; UpdatePerformance(false);
        _ = UpdateBrightnessAsync();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        _clock.Text = now.ToString("t", CultureInfo.CurrentCulture);
        _date.Text = now.ToString("ddd MMM d", CultureInfo.CurrentCulture);
    }

    private void UpdateBattery()
    {
        var state = SystemActions.Battery();
        _battery.Visibility = state.HasBattery ? Visibility.Visible : Visibility.Hidden;
        _statusPercent.Text = state.HasBattery ? state.Percent + "%" : "";
        _statusBattery.Text = state.HasBattery ? Ui.BatteryGlyph(state.Percent, state.Charging || state.OnAc && state.Percent >= 99) : "";
        _batteryPercent.Text = state.Percent + "%";
        _batteryGlyph.Text = Ui.BatteryGlyph(state.Percent, state.Charging);
        _batteryDetail.Text = state switch
        {
            { Charging: true, Remaining: { } left } => Ui.FormatSpan(left) + " to full",
            { Charging: true } => "Charging",
            { OnAc: true } => state.Percent >= 99 ? "Fully charged" : "Plugged in, not charging",
            { Remaining: { } left } => Ui.FormatSpan(left) + " left" + (state.Saver ? " · saver" : ""),
            _ => state.Saver ? "Energy saver on" : "On battery",
        };
        var accent = state.Charging || state.OnAc ? TabletStyle.Good : state.Percent <= 20 ? TabletStyle.Critical : _palette.Text;
        Ui.Tint(_batteryGlyph, accent, false); Ui.Tint(_statusBattery, state.Percent <= 20 && !state.OnAc ? TabletStyle.Critical : _palette.Text, false);
        Ui.Tint(_batteryPercent, _palette.Text, false); Ui.Tint(_batteryDetail, _palette.Secondary, false);
    }

    private void UpdateRadios()
    {
        var airplane = SystemRadios.AirplaneMode;
        _airplane.Set(airplane == true, airplane == null ? "Unavailable" : airplane == true ? "On" : "Off", airplane != null);
        var wifi = _radios.WifiOn;
        var network = wifi == true ? SystemRadios.WifiNetwork() : null;
        _wifi.Set(wifi == true, wifi == null ? "Unavailable" : wifi == false ? "Off" : network ?? "Not connected", wifi != null);
        _statusWifi.Visibility = network != null ? Visibility.Visible : Visibility.Collapsed;
        var bluetooth = _radios.BluetoothOn;
        _bluetooth.Set(bluetooth == true, bluetooth == null ? "Unavailable" : bluetooth == true ? "On" : "Off", bluetooth != null);
    }

    private void ToggleAirplane()
    {
        var on = SystemRadios.AirplaneMode;
        if (on == null) return;
        _airplane.Set(!on.Value, !on.Value ? "On" : "Off", true);
        if (!SystemRadios.SetAirplaneMode(!on.Value)) { TabletLog.Write("cc", "airplane toggle refused"); UpdateRadios(); }
    }

    private async Task ToggleWifiAsync()
    {
        if (_radios.WifiOn is not { } on) return;
        _wifi.Set(!on, !on ? "Turning on…" : "Off", true);
        if (!await _radios.SetWifiAsync(!on)) TabletLog.Write("cc", "wi-fi toggle refused");
        UpdateRadios();
    }

    private async Task ToggleBluetoothAsync()
    {
        if (_radios.BluetoothOn is not { } on) return;
        _bluetooth.Set(!on, !on ? "On" : "Off", true);
        if (!await _radios.SetBluetoothAsync(!on)) TabletLog.Write("cc", "bluetooth toggle refused");
        UpdateRadios();
    }

    private void UpdateMedia()
    {
        var now = MediaSessions.Shared.Current;
        _mediaTitle.Text = now?.Title ?? "Not Playing";
        _mediaArtist.Text = now == null ? "" : string.IsNullOrEmpty(now.Artist) ? now.Album : now.Artist;
        _play.Text = now?.IsPlaying == true ? "" : "";
        _transport.Opacity = now == null ? 0.35 : 1;
        var key = now == null ? null : now.Title + "\n" + now.SourceApp + "\n" + (now.Thumbnail?.Length ?? 0);
        if (key == _mediaKey) return;
        _mediaKey = key;
        ImageSource? art = null;
        if (now?.Thumbnail is { Length: > 0 } bytes)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 120;
                image.StreamSource = new MemoryStream(bytes); image.EndInit(); image.Freeze();
                art = image;
            }
            catch (Exception ex) when (ex is NotSupportedException or IOException or ArgumentException or InvalidOperationException) { }
        }
        if (art != null) { _art.Background = new ImageBrush(art) { Stretch = Stretch.UniformToFill }; _artGlyph.Visibility = Visibility.Collapsed; }
        else { _art.Background = new SolidColorBrush(_palette.ToggleOff); _artGlyph.Visibility = Visibility.Visible; }
    }

    private void UpdateVolume()
    {
        if (!_audio.IsAvailable) { _volume.IsEnabled = false; _volume.Opacity = 0.4; return; }
        _volume.IsEnabled = true; _volume.Opacity = 1;
        var level = _audio.Volume;
        if (level >= 0) _volume.SetLevel(_audio.Muted ? 0 : level);
        UpdateVolumeGlyph();
    }

    private void UpdateVolumeGlyph() => _volume.SetGlyph(_audio.Muted || _audio.Volume == 0 ? "" : "");

    private async Task UpdateBrightnessAsync()
    {
        var value = await Brightness.ReadAsync();
        _brightness.IsEnabled = value >= 0; _brightness.Opacity = value >= 0 ? 1 : 0.4;
        // Ignore reads that race a drag: WMI reports the previous level for a moment.
        if (value >= 0 && Environment.TickCount64 > _brightnessQuietUntil) _brightness.SetLevel(value / 100.0);
    }

    private void UpdateRotation()
    {
        var (available, locked, active) = RotationLock.Read();
        _rotation.Set(locked, !available ? "Unavailable" : locked ? "Locked" : active ? "Rotation Lock" : "Rotation (laptop)", available);
    }

    private void ToggleRotation()
    {
        var (available, locked, _) = RotationLock.Read();
        if (!available) return;
        if (!RotationLock.SetLocked(!locked)) TabletLog.Write("cc", "rotation lock refused");
        UpdateRotation();
    }

    private void UpdateTheme() => _theme.Set(SystemTheme.IsDark, "Dark Mode", true);

    private void ToggleTheme()
    {
        var dark = !SystemTheme.IsDark;
        SystemTheme.SetDark(dark);
        _palette = Palette.For(dark);
        ApplyPalette(true);
        UpdateTheme();
    }

    private void UpdateRefresh()
    {
        if (Environment.TickCount64 < _refreshPendingUntil) return;
        _rate = RefreshRate.Read();
        var usable = _rate.Max > 60 && _rate.Has60;
        _refresh.SetBig(_rate.Current > 0 ? _rate.Current.ToString(CultureInfo.InvariantCulture) : "–", "Hz");
        _refresh.Set(false, usable ? "Refresh Rate" : "Fixed", usable);
    }

    private void ToggleRefresh()
    {
        if (!(_rate.Max > 60 && _rate.Has60)) return;
        var target = _rate.Current > 60 ? 60 : _rate.Max;
        // G-Helper toggles between 60 Hz and the panel maximum and remembers the manual choice;
        // without it, switch the mode directly.
        var viaGHelper = GHelper.IsAvailable && GHelper.ToggleRefreshRate();
        if (!viaGHelper) RefreshRate.Set(target);
        _refresh.SetBig(target.ToString(CultureInfo.InvariantCulture), "Hz");
        _refreshPendingUntil = Environment.TickCount64 + 2500;
        TabletLog.Write("cc", $"refresh -> {target} Hz ({(viaGHelper ? "g-helper" : "direct")})");
    }

    private void UpdatePerformance(bool animate)
    {
        var available = GHelper.IsAvailable;
        _performance.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        // Without G-Helper the battery module takes the whole row.
        Place(_battery, available ? 2 : 0, 4, available ? 2 : 4, 1);
        if (!available) return;
        var actual = GHelper.PerformanceMode;
        if (_perfPending is { } pending)
        {
            if (actual == pending || Environment.TickCount64 > _perfPendingUntil)
            {
                if (actual != pending) TabletLog.Write("cc", $"performance mode {pending} not confirmed (config says {actual})");
                _perfPending = null;
            }
        }
        _perfMode = _perfPending ?? actual;
        for (var i = 0; i < _perfSegments.Length; i++)
        {
            var (glyph, label, mode, color) = _perfSegments[i];
            var selected = mode == _perfMode;
            Ui.Tint(glyph, selected ? _palette.OnActive : _palette.Text, animate);
            Ui.Tint(label, selected ? _palette.OnActive : _palette.Text, animate);
        }
        PlacePerfThumb(animate);
    }

    private void PlacePerfThumb(bool animate)
    {
        var index = Array.FindIndex(_perfSegments, s => s.Mode == _perfMode);
        _perfThumb.Visibility = index < 0 ? Visibility.Hidden : Visibility.Visible;
        if (index < 0) return;
        var x = index * _perfThumb.Width;
        if (animate) Run(_perfThumbMove, TranslateTransform.XProperty, x, TabletMotion.Enter, TabletMotion.Decelerate);
        else { _perfThumbMove.BeginAnimation(TranslateTransform.XProperty, null); _perfThumbMove.X = x; }
    }

    private void SetPerformance(int mode)
    {
        if (mode == _perfMode) return;
        if (!GHelper.SetPerformanceMode(mode)) { TabletLog.Write("cc", "performance hotkey failed"); return; }
        // G-Helper saves its config two seconds after a change; keep the optimistic choice until then.
        _perfPending = mode; _perfPendingUntil = Environment.TickCount64 + 4000;
        UpdatePerformance(true);
    }

    // ---------------------------------------------------------------- module views

    /// <summary>Circle toggle with a title and detail line (connectivity module).</summary>
    private sealed class Row
    {
        private readonly ControlCenterWindow _owner;
        private readonly Border _circle = new() { Width = 44, Height = 44, Background = new SolidColorBrush(Colors.Gray) };
        private readonly TextBlock _glyph, _title = Ui.Label(14.5, FontWeights.SemiBold), _detail = Ui.Label(12.5, FontWeights.Normal);
        private readonly Color _color;
        private bool _on, _enabled = true;
        internal Grid View { get; } = new() { Height = 54, Background = Brushes.Transparent };

        internal Row(ControlCenterWindow owner, string glyph, string title, Color color, Action toggle)
        {
            _owner = owner; _color = color; _glyph = Ui.Glyph(glyph, 20); _title.Text = title;
            _circle.Child = _glyph;
            View.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            View.ColumnDefinitions.Add(new ColumnDefinition());
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            text.Children.Add(_title); text.Children.Add(_detail);
            Grid.SetColumn(text, 1);
            View.Children.Add(_circle); View.Children.Add(text);
            Ui.Tappable(View, () => { if (_enabled) toggle(); }, 0.96);
            owner._themed.Add(Apply);
        }

        internal void Set(bool on, string detail, bool enabled)
        {
            var changed = on != _on;
            _on = on; _detail.Text = detail; _enabled = enabled; View.Opacity = enabled ? 1 : 0.4;
            Apply(changed);
        }

        private void Apply(bool animate)
        {
            var p = _owner._palette;
            Ui.Tint(_circle.Background, _on ? p.Active : p.ToggleOff, animate);
            Ui.Tint(_glyph, _on ? p.OnActive : p.Text, animate);
            Ui.Tint(_title, p.Text, animate); Ui.Tint(_detail, p.Secondary, animate);
        }
    }

    /// <summary>1x1 module, a Home tile: glyph (or a big value) centered, label bottom-left; "on" fills it with the accent.</summary>
    private sealed class Tile
    {
        private readonly ControlCenterWindow _owner;
        private readonly TextBlock _glyph, _label = Ui.Label(11.5, FontWeights.SemiBold), _value = Ui.Label(26, FontWeights.SemiBold), _unit = Ui.Label(12, FontWeights.SemiBold);
        private readonly Color _color;
        private bool _on, _enabled = true;
        internal Border View { get; } = Ui.Platter();

        internal Tile(ControlCenterWindow owner, string glyph, string label, Color color, Action tap, bool big = false)
        {
            _owner = owner; _color = color; _glyph = Ui.Glyph(glyph, 26); _label.Text = label;
            var grid = new Grid();
            _label.HorizontalAlignment = HorizontalAlignment.Left; _label.VerticalAlignment = VerticalAlignment.Bottom; _label.Margin = new Thickness(9, 0, 8, 7);
            if (big)
            {
                _value.FontFamily = Ui.Display; _value.FontWeight = FontWeights.Light;
                var value = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
                _unit.VerticalAlignment = VerticalAlignment.Bottom; _unit.Margin = new Thickness(2, 0, 0, 5);
                value.Children.Add(_value); value.Children.Add(_unit);
                grid.Children.Add(value);
            }
            else { _glyph.Margin = new Thickness(0, 0, 0, 12); grid.Children.Add(_glyph); }
            grid.Children.Add(_label);
            View.Child = grid;
            Ui.Tappable(View, () => { if (_enabled) tap(); });
            owner._themed.Add(Apply);
        }

        internal void Set(bool on, string label, bool enabled)
        {
            var changed = on != _on;
            _on = on; _label.Text = label; _enabled = enabled; View.Opacity = enabled ? 1 : 0.45;
            Apply(changed);
        }

        internal void SetBig(string value, string unit) { _value.Text = value; _unit.Text = unit; }

        private void Apply(bool animate)
        {
            var p = _owner._palette;
            Ui.Tint(View.Background, _on ? p.Active : p.Platter, animate);
            Ui.Tint(_glyph, _on ? p.OnActive : p.Text, animate);
            foreach (var text in new[] { _label, _value, _unit }) Ui.Tint(text, _on ? p.OnActive : p.Text, animate);
        }
    }

    // ---------------------------------------------------------------- native

    private static void Cloak(nint handle, bool cloak) { var value = cloak ? 1 : 0; DwmSetWindowAttribute(handle, 13 /* DWMWA_CLOAK */, ref value, sizeof(int)); }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint window, out Native.Rect rect);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
}
