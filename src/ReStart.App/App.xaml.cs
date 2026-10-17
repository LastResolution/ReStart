using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class App : Application
{
    private SingleInstanceGate? _instance;
    private RegisteredWaitHandle? _wait;
    private StartIntegration? _integration;
    private System.Windows.Forms.NotifyIcon? _tray;
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var preview = e.Args.Contains("--preview") || e.Args.Contains("--smoke-test");
        var smoke = e.Args.Contains("--smoke-test");
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        _instance = new(sid, preview);
        if (!_instance.IsPrimary)
        {
            if (!smoke && !e.Args.Contains("--background")) _instance.NotifyExisting();
            Shutdown(smoke ? 2 : 0); return;
        }
        var data = preview ? Path.Combine(AppContext.BaseDirectory, "preview-data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReStart");
        var store = new LayoutStore(Path.Combine(data, "layout.json"));
        _window = new MainWindow(store, preview);
        MainWindow = _window;
        _wait = ThreadPool.RegisterWaitForSingleObject(_instance.Activation, (_, _) => Dispatcher.BeginInvoke(() => _window?.Present()), null, Timeout.Infinite, false);
        if (preview)
        {
            _window.Title = L.T("ReStart — プレビュー");
            _window.Width = 1280; _window.Height = 800; _window.Topmost = false;
            _window.ShowInTaskbar = true; _window.WindowStyle = WindowStyle.SingleBorderWindow;
            _window.ResizeMode = ResizeMode.CanResize; _window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (e.Args.Contains("--background") && !smoke) _ = _window.InitializeAsync();
            else _window.Show();
            if (smoke) RunSmoke(e.Args);
            return;
        }
        var handle = new WindowInteropHelper(_window).EnsureHandle();
        _integration = new StartIntegration(handle, Dispatcher, _window.Toggle);
        _window.PreferencesChanged = () =>
        {
            _integration.Apply(_window.Document.Preferences);
            RefreshTrayLanguage();
            if (_integration.Status is string status) _window.SetStatus(status);
        };
        CreateTray();
        if (e.Args.Contains("--background")) _ = _window.InitializeAsync();
        else _window.Present();
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReStart");
            try { Directory.CreateDirectory(directory); File.AppendAllText(Path.Combine(directory, "error.log"), $"{DateTimeOffset.Now:O} {args.Exception}\n"); } catch (IOException) { }
            MessageBox.Show(L.T("ReStart でエラーが発生したため終了します。保存済みのレイアウトは保持されます。"), "ReStart", MessageBoxButton.OK, MessageBoxImage.Error);
            ExitApplication();
        };
    }

    private void CreateTray()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(L.T("ReStart を開く"), null, (_, _) => Dispatcher.Invoke(() => _window!.Present()));
        menu.Items.Add(L.T("設定"), null, (_, _) => Dispatcher.Invoke(() => { _window!.Present(); _window.ShowSettings(); }));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(L.T("終了"), null, (_, _) => Dispatcher.Invoke(ExitApplication));
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = L.T("ReStart — Ctrl+Alt+Space で開く"),
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => _window!.Toggle());
    }

    private void RefreshTrayLanguage()
    {
        if (_tray?.ContextMenuStrip is not { } menu) return;
        menu.Items[0].Text = L.T("ReStart を開く"); menu.Items[1].Text = L.T("設定"); menu.Items[3].Text = L.T("終了");
        _tray.Text = L.T("ReStart — Ctrl+Alt+Space で開く");
    }

    private void RunSmoke(string[] args)
    {
        var watch = Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        var settled = false;
        timer.Tick += (_, _) =>
        {
            if (_window == null) return;
            if ((!_window.IsReady || !_window.IconsReady || !_window.ScriptsReady) && watch.Elapsed < TimeSpan.FromSeconds(45)) return;
            if (!settled) { settled = true; return; }
            timer.Stop();
            var pathIndex = Array.IndexOf(args, "--snapshot");
            var path = pathIndex >= 0 && pathIndex + 1 < args.Length ? Path.GetFullPath(args[pathIndex + 1]) : Path.Combine(AppContext.BaseDirectory, "smoke.png");
            var success = _window.IsReady && _window.IconsReady && _window.ScriptsReady && _window.CatalogCount > 0 && _window.IconCount > 0;
            try
            {
                _window.SaveScreenshot(path);
                File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonSerializer.Serialize(new { success, apps = _window.CatalogCount, icons = _window.IconCount, groups = _window.Document.Groups.Count, scriptTiles = _window.ScriptTileCount, scriptFrames = _window.ScriptFrameCount, elapsedMs = watch.ElapsedMilliseconds }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException) { success = false; }
            _window.Exit(); Shutdown(success ? 0 : 1);
        };
        timer.Start();
    }

    public void ExitApplication() { _window?.SaveLayout(); _window?.Exit(); Shutdown(); }
    protected override void OnExit(ExitEventArgs e)
    {
        _integration?.Dispose();
        _tray?.Dispose();
        _wait?.Unregister(null);
        _instance?.Dispose();
        base.OnExit(e);
    }
}
