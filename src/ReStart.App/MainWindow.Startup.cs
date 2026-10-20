using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow
{
    private bool _startupComplete, _startupPreparing, _startupWanted;
    private Task? _startupPresentation;
    private StartupWindow? _startupWindow;
    private System.Drawing.Point _startupPoint;
    private TextKey _startupStage = K.StartupApps;
    private double _startupFraction = .08;
    private readonly HashSet<string> _scriptAttempts = [];
    private Task _startupWorkerWarmup = Task.CompletedTask;
    private bool _startupSettings;
    internal Action? PresentationVisibilityChanged { get; set; }
    internal bool IsPresenting => IsVisible || _startupWindow?.IsVisible == true;

    private void ReportStartup(TextKey stage, double fraction)
    {
        _startupStage = stage; _startupFraction = fraction;
        _startupWindow?.UpdateProgress(stage, fraction);
    }

    private void RequestStartupPresentation()
    {
        if (_startupWindow != null) { _startupWindow.Activate(); _startupWindow.Query.Focus(); return; }
        _startupWanted = true;
        _startupPoint = _startButtonPoint ?? System.Windows.Forms.Cursor.Position;
        // Loading preferences runs synchronously before the first catalog await,
        // so the splash and the Windows-key hook immediately use the saved theme.
        _ = InitializeAsync();
        _startupWindow = new(() => Document.Preferences.CloseOnDeactivate);
        var created = _startupWindow;
        // WPF's Owner property requires an already shown owner. Native ownership
        // keeps foreground dismissal correct without flashing the hidden launcher.
        new WindowInteropHelper(created).Owner = new WindowInteropHelper(this).EnsureHandle();
        created.Closed += (_, _) => { if (_startupWindow == created) { _startupWindow = null; CancelStartupPresentation(); } };
        _startupWindow.CancelRequested += CancelStartupPresentation;
        _startupWindow.UpdateProgress(_startupStage, _startupFraction);
        _startupWindow.ShowAt(_startupPoint, !_preview);
        PresentationVisibilityChanged?.Invoke();
        _startupPresentation ??= CompleteStartupPresentationAsync();
    }

    private void CancelStartupPresentation()
    {
        _startupWanted = false;
        _startupSettings = false;
        var window = _startupWindow; _startupWindow = null; window?.Close();
        PauseScripts(); PresentationVisibilityChanged?.Invoke();
    }

    private async Task CompleteStartupPresentationAsync()
    {
        // Always yield before assigning this task to its field, including when
        // background initialization has already completed without live tiles.
        await Dispatcher.Yield(DispatcherPriority.Background);
        try
        {
            await InitializeAsync();
            if (_exit || !_startupWanted) return;
            ReportStartup(K.StartupTiles, .8);
            _startupPreparing = true;
            SizeStartupContent();
            await _startupWorkerWarmup;
            if (!_startupWanted || _exit) return;
            var watch = Stopwatch.StartNew();
            // Every tile gets either an initial frame or a visible error. External
            // sources retain their bounded loading/error state instead of holding
            // the entire launcher hostage to an unavailable service.
            do
            {
                await UpdateScriptTilesAsync().WaitAsync(TimeSpan.FromSeconds(12));
                if (!_startupWanted || _exit) return;
                var localPending = _scriptViews.Any(v => v.Tile.Live && !_scriptAttempts.Contains(v.Tile.Id));
                var samplePending = _systemMetrics != null && _systemMetrics.Latest?.NetworkReceiveBytesPerSecond == null;
                var imagesPending = _slideshowSources.Values.Any(v => !v.Source.Initialized);
                var dataPending = _tileData.Values.Any(v => v.Runtime.IsLoading);
                if (!localPending && !samplePending && !imagesPending && !dataPending) break;
                await Task.Delay(100);
                foreach (var session in _scriptSessions.Values) session.NextUpdate = DateTimeOffset.MinValue;
            } while (watch.Elapsed < TimeSpan.FromSeconds(9));
            // Providers can finish while the first JS batch is in flight. Render
            // their settled snapshot once before exposing the prepared board.
            foreach (var session in _scriptSessions.Values) session.NextUpdate = DateTimeOffset.MinValue;
            await UpdateScriptTilesAsync().WaitAsync(TimeSpan.FromSeconds(12));
            if (!_startupWanted || _exit) return;
            _startupComplete = true;
            MarkStartup("prepared");
            ReportStartup(K.StartupReady, 1);
        }
        catch (TimeoutException)
        {
            PauseScripts();
            foreach (var view in _scriptViews.Where(v => v.Body.Children.Count == 0))
                view.Body.Children.Add(new System.Windows.Controls.TextBlock { Text = L.T(K.ErrorScript), TextWrapping = TextWrapping.Wrap });
            _startupComplete = true;
        }
        finally
        {
            _startupPreparing = false; _startupPresentation = null;
            if (!_startupWanted || _exit) PauseScripts();
        }
        if (!_startupWanted || _exit) return;
        var query = _startupWindow?.Query.Text ?? "";
        var launch = _startupWindow?.LaunchRequested == true;
        _startupWanted = false;
        var splash = _startupWindow; _startupWindow = null;
        _startButtonPoint = _startupPoint;
        try
        {
            splash?.Close();
            Present();
            SearchBox.Text = query; SearchBox.CaretIndex = query.Length;
            FocusSearchInput();
            PresentationVisibilityChanged?.Invoke();
            if (_startupSettings) { _startupSettings = false; ShowSettings(); }
            if (launch && SearchResults.SelectedItem is System.Windows.Controls.Button selected) selected.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        }
        finally { _startButtonPoint = null; }
    }

    /// <summary>Opens settings after startup preparation, including when requested from the tray during loading.</summary>
    public void PresentSettings()
    {
        if (_startupComplete) { Present(); ShowSettings(); }
        else { _startupSettings = true; Present(); }
    }

    private void SizeStartupContent()
    {
        var handle = new WindowInteropHelper(this).EnsureHandle();
        if (!_preview)
        {
            var screen = System.Windows.Forms.Screen.FromPoint(_startupPoint);
            var area = screen.WorkingArea;
            _wallpaperMonitor = screen.Bounds; RefreshWallpaper();
            // Size the hidden HWND without showing it or taking focus from the splash.
            Services.Native.SetWindowPos(handle, 0, area.X, area.Y, area.Width, area.Height, 0x0014);
        }
        var transform = HwndSource.FromHwnd(handle).CompositionTarget.TransformFromDevice;
        var size = !_preview ? transform.Transform(new Point(System.Windows.Forms.Screen.FromPoint(_startupPoint).WorkingArea.Width,
            System.Windows.Forms.Screen.FromPoint(_startupPoint).WorkingArea.Height)) : new Point(Width, Height);
        Root.Measure(new Size(size.X, size.Y)); Root.Arrange(new Rect(0, 0, size.X, size.Y)); Root.UpdateLayout();
        FitPanels(); Root.UpdateLayout();
    }

    private void StartStartupTileWarmup()
    {
        if (!_startupWanted || ScriptTileCount == 0) return;
        foreach (var tile in Document.Groups.SelectMany(g => g.Tiles).Where(t => t.Widget == "script" && t.Live))
        {
            try
            {
                if (_scriptPackages.Read(tile.ScriptId).Manifest.Capabilities.Contains("system.metrics") && (!_preview || _verifyHostMetrics))
                    _systemMetrics ??= new();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException) { }
        }
        var pool = _scriptWorkerPool ??= new();
        _startupWorkerWarmup = Task.Run(() => Task.WhenAll(Enumerable.Range(0, Math.Min(4, ScriptTileCount))
            .Select(_ => pool.RenderAsync("function render() { return { text: 'Ready' }; }", new { }))));
    }
}
