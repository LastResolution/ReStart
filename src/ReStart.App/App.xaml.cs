using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

/// <summary>Owns the single-instance lease, tray, shell integration, and application shutdown.</summary>
public partial class App : Application
{
    private SingleInstanceGate? _instance;
    private RegisteredWaitHandle? _wait;
    private StartIntegration? _integration;
    private System.Windows.Forms.NotifyIcon? _tray;
    private MainWindow? _window;
    private PinRequestChannel? _pinRequests;

    /// <summary>Acquires the instance lease before creating UI or registering shell hooks.</summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var preview = e.Args.Contains("--preview") || e.Args.Contains("--smoke-test");
        var smoke = e.Args.Contains("--smoke-test");
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var pinIndex = Array.IndexOf(e.Args, "--pin");
        var pinPath = pinIndex >= 0 && pinIndex + 1 < e.Args.Length ? e.Args[pinIndex + 1] : null;
        if (pinIndex >= 0 && pinPath == null) { MessageBox.Show(L.T(K.ErrorPinPath), "ReStart"); Shutdown(1); return; }
        using var process = Process.GetCurrentProcess();
        var channelIdentity = sid + "." + process.SessionId + (preview ? ".Preview" : "");
        _instance = new(sid, preview);
        if (!_instance.IsPrimary)
        {
            if (pinPath != null)
            {
                try
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    var reply = await PinRequestChannel.SendAsync(channelIdentity, pinPath, deadline.Token);
                    if (!reply.Success && !preview) MessageBox.Show(reply.Message, "ReStart", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(reply.Success ? 0 : 1);
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException)
                { if (!preview) MessageBox.Show(L.T(K.ErrorPinConnection), "ReStart", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
                return;
            }
            if (!smoke && !e.Args.Contains("--background")) _instance.NotifyExisting();
            Shutdown(smoke ? 2 : 0); return;
        }
        var data = preview ? Path.Combine(AppContext.BaseDirectory, "preview-data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReStart");
        var store = new LayoutStore(Path.Combine(data, "layout.json"), AppBuildInfo.Version);
        _window = new MainWindow(store, preview);
        MainWindow = _window;
        _pinRequests = new(channelIdentity, path => Dispatcher.InvokeAsync(() => _window.PinPathAsync(path)).Task.Unwrap());
        _wait = ThreadPool.RegisterWaitForSingleObject(_instance.Activation, (_, _) => Dispatcher.BeginInvoke(() => _window?.Present()), null, Timeout.Infinite, false);
        if (preview)
        {
            _window.Title = L.T(K.PreviewTitle);
            _window.Width = 1280; _window.Height = 800; _window.Topmost = false;
            _window.ShowInTaskbar = true; _window.WindowStyle = WindowStyle.SingleBorderWindow;
            _window.ResizeMode = ResizeMode.CanResize; _window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (e.Args.Contains("--background") && !smoke) _ = _window.InitializeAsync();
            else _window.Show();
            if (pinPath != null) await _window.PinPathAsync(pinPath);
            if (smoke) RunSmoke(e.Args);
            return;
        }
        var handle = new WindowInteropHelper(_window).EnsureHandle();
        LegacySettingsCleanup.Run();
        _integration = new StartIntegration(handle, Dispatcher, _window.Toggle, _window.DismissFromTaskbar, _window.ToggleFromStartButton);
        _window.IsVisibleChanged += (_, _) => _integration.SetMenuOpen(_window.IsPresenting);
        _window.PresentationVisibilityChanged = () => _integration.SetMenuOpen(_window.IsPresenting);
        _window.PreferencesChanged = () =>
        {
            _integration.Apply(_window.Document.Preferences);
            try { ExplorerPinRegistration.Current.SetEnabled(_window.Document.Preferences.ExplorerContextMenu); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException)
            { _window.SetStatus(L.T(K.ErrorExplorerRegistration) + ex.Message); }
            RefreshTrayLanguage();
            if (_integration.Status is string status) _window.SetStatus(status);
        };
        CreateTray();
        if (pinPath != null) await _window.PinPathAsync(pinPath);
        else if (e.Args.Contains("--background")) _ = _window.InitializeAsync();
        else _window.Present();
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReStart");
            try { Directory.CreateDirectory(directory); File.AppendAllText(Path.Combine(directory, "error.log"), $"{DateTimeOffset.Now:O} {args.Exception}\n"); } catch (IOException) { }
            MessageBox.Show(L.T(K.ErrorUnhandled), "ReStart", MessageBoxButton.OK, MessageBoxImage.Error);
            ExitApplication();
        };
    }

    private void CreateTray()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(L.T(K.AppOpen), null, (_, _) => Dispatcher.Invoke(() => _window!.Present()));
        menu.Items.Add(L.T(K.NavSettings), null, (_, _) => Dispatcher.Invoke(() => _window!.PresentSettings()));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(L.T(K.CommonExit), null, (_, _) => Dispatcher.Invoke(ExitApplication));
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = L.T(K.TrayTooltip),
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => _window!.Toggle());
    }

    private void RefreshTrayLanguage()
    {
        if (_tray?.ContextMenuStrip is not { } menu) return;
        menu.Items[0].Text = L.T(K.AppOpen); menu.Items[1].Text = L.T(K.NavSettings); menu.Items[3].Text = L.T(K.CommonExit);
        _tray.Text = L.T(K.TrayTooltip);
    }

    private void RunSmoke(string[] args)
    {
        var watch = Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        var settled = false;
        var searchIndex = Array.IndexOf(args, "--search-query");
        var query = searchIndex >= 0 && searchIndex + 1 < args.Length ? args[searchIndex + 1] : null;
        var searchPrepared = false;
        timer.Tick += async (_, _) =>
        {
            if (_window == null) return;
            if ((!_window.IsReady || !_window.IconsReady || !_window.ScriptsReady) && watch.Elapsed < TimeSpan.FromSeconds(45)) return;
            if (query != null && !searchPrepared) { _window.SetSmokeSearch(query); searchPrepared = true; return; }
            if (!settled) { settled = true; return; }
            timer.Stop();
            var pathIndex = Array.IndexOf(args, "--snapshot");
            var path = pathIndex >= 0 && pathIndex + 1 < args.Length ? Path.GetFullPath(args[pathIndex + 1]) : Path.Combine(AppContext.BaseDirectory, "smoke.png");
            var success = _window.IsReady && _window.IconsReady && _window.ScriptsReady && _window.CatalogCount > 0 && _window.IconCount > 0;
            NavigationSmokeResult? navigation = null;
            PlacementSmokeResult? placement = null;
            AppFolderSmokeResult? appFolders = null;
            FirstInputSmokeResult? firstInput = null;
            AppMenuSmokeResult? appMenus = null;
            ReleaseInfoSmokeResult? releaseInfo = null;
            ScrollSmokeResult? scrolling = null;
            BundledTilesSmokeResult? bundledTiles = null;
            IconSmokeResult? pinIcons = null;
            MotionSmokeResult? motion = null;
            EditingSmokeResult? editing = null;
            FolderDropSmokeResult? folderDrops = null;
            TileDataSmokeResult? tileData = null;
            SearchSelectionSmokeResult? searchSelection = null;
            StartupSmokeResult? startupPreparation = null;
            AppearanceSmokeResult? appearance = null;
            string? error = null;
            try
            {
                if (args.Contains("--navigation-test")) navigation = _window.VerifyNavigation(path);
                if (args.Contains("--placement-test")) placement = _window.VerifyPlacement(path);
                if (args.Contains("--app-tree-test")) appFolders = _window.VerifyAppFolders(path);
                if (args.Contains("--first-input-test")) firstInput = await _window.VerifyFirstInputAsync(path);
                if (args.Contains("--app-menu-test")) appMenus = await _window.VerifyAppMenusAsync(path);
                if (args.Contains("--release-info-test")) releaseInfo = await _window.VerifyReleaseInfoAsync(path);
                if (args.Contains("--scroll-test")) scrolling = await _window.VerifyScrollingAsync(path);
                if (args.Contains("--bundled-tiles-test")) bundledTiles = await _window.VerifyBundledTilesAsync(path);
                if (args.Contains("--icon-test")) pinIcons = await _window.VerifyIconsAsync(path);
                if (args.Contains("--motion-test")) motion = await _window.VerifyMotionAsync(path);
                if (args.Contains("--editing-test")) editing = await _window.VerifyEditingAsync(path);
                if (args.Contains("--folder-drop-test")) folderDrops = await _window.VerifyFolderDropsAsync(path);
                if (args.Contains("--tile-data-test")) tileData = await _window.VerifyTileDataAsync(path);
                if (args.Contains("--search-selection-test")) searchSelection = await _window.VerifySearchSelectionAsync(path);
                if (args.Contains("--startup-test")) startupPreparation = await _window.VerifyStartupAsync(path);
                if (args.Contains("--appearance-test")) appearance = await _window.VerifyAppearanceAsync(path);
                _window.SaveScreenshot(path);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException) { success = false; error = ex.Message; }
            try
            {
                File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonSerializer.Serialize(new { success, apps = _window.CatalogCount, icons = _window.IconCount, catalogFolders = _window.CatalogFolderCount, groups = _window.Document.Groups.Count, scriptTiles = _window.ScriptTileCount, scriptFrames = _window.ScriptFrameCount, query, searchResults = _window.SearchResultNames, searchInputVisible = _window.SearchInputInVisibleTree, navigation, placement, appFolders, firstInput, appMenus, releaseInfo, scrolling, bundledTiles, pinIcons, motion, editing, folderDrops, tileData, searchSelection, startupPreparation, appearance, startup = _window.StartupMetrics, error, elapsedMs = watch.ElapsedMilliseconds }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (IOException) { success = false; }
            _window.Exit(); Shutdown(success ? 0 : 1);
        };
        timer.Start();
    }

    /// <summary>Saves the current layout and disposes the launcher before shutting down the application.</summary>
    public void ExitApplication() { _window?.SaveLayout(); _window?.Exit(); Shutdown(); }
    /// <summary>Releases hooks, tray resources, activation waits, and the instance lease.</summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _integration?.Dispose();
        _pinRequests?.Dispose();
        _tray?.Dispose();
        _wait?.Unregister(null);
        _instance?.Dispose();
        base.OnExit(e);
    }
}
