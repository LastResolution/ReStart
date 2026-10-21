using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using ReStart.App.Services;

namespace ReStart.App;

public partial class MainWindow
{
    private string? _wallpaperPath;
    private DateTime _wallpaperWriteTime;
    private long _wallpaperLength;
    private System.Drawing.Rectangle? _wallpaperMonitor;
    private DispatcherTimer? _appearanceRefresh;

    private void WatchDesktopAppearance()
    {
        if (_preview) return;
        _appearanceRefresh = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(200) };
        _appearanceRefresh.Tick += (_, _) => { _appearanceRefresh.Stop(); if (!_exit) { _wallpaperPath = null; RefreshLayout(); } };
        SystemEvents.UserPreferenceChanged += OnDesktopAppearanceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.TimeChanged += OnSystemTimeChanged;
    }

    private void OnDesktopAppearanceChanged(object sender, UserPreferenceChangedEventArgs args) => QueueAppearanceRefresh();
    private void OnDisplaySettingsChanged(object? sender, EventArgs args) => QueueAppearanceRefresh();
    private void OnSystemTimeChanged(object? sender, EventArgs args) => Dispatcher.BeginInvoke(async () =>
    {
        if (_exit) return;
        TimeZoneInfo.ClearCachedData();
        // Workers cache their process-local time zone too. Retire them while
        // retaining tile JSON state so Date uses the newly selected PC zone.
        PauseScripts();
        foreach (var runtime in _scriptSessions.Values) runtime.NextUpdate = DateTimeOffset.MinValue;
        UpdateLiveTiles(); await UpdateScriptTilesAsync();
    });
    private void QueueAppearanceRefresh() => Dispatcher.BeginInvoke(() =>
    {
        if (_exit || _appearanceRefresh == null) return;
        _appearanceRefresh.Stop(); _appearanceRefresh.Start();
    });

    private void StopWatchingDesktopAppearance()
    {
        SystemEvents.UserPreferenceChanged -= OnDesktopAppearanceChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.TimeChanged -= OnSystemTimeChanged;
        _appearanceRefresh?.Stop();
    }

    private void RefreshWallpaper(string? fixturePath = null)
    {
        if (!Document.Preferences.ShowWallpaper || SystemParameters.HighContrast || _preview && fixturePath == null && !Document.Preferences.UseCustomBackground)
        { Wallpaper.Source = null; _wallpaperPath = null; return; }
        try
        {
            var path = fixturePath ?? (Document.Preferences.UseCustomBackground
                ? new BackgroundImageStore(Path.GetDirectoryName(_store.FilePath)!).Resolve(Document.Preferences.CustomBackgroundId)
                : DesktopWallpaperSource.CurrentPath(_wallpaperMonitor ?? System.Windows.Forms.Screen.PrimaryScreen!.Bounds));
            var file = path == null ? null : new FileInfo(path);
            if (file?.Exists != true) { Wallpaper.Source = null; _wallpaperPath = null; return; }
            if (_wallpaperPath == path && Wallpaper.Source != null && _wallpaperWriteTime == file.LastWriteTimeUtc && _wallpaperLength == file.Length) return;
            Wallpaper.Source = DesktopWallpaperSource.Load(path!);
            _wallpaperPath = path; _wallpaperWriteTime = file.LastWriteTimeUtc; _wallpaperLength = file.Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        { Wallpaper.Source = null; _wallpaperPath = null; }
    }
}
