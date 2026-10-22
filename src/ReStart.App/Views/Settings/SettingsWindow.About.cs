using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

internal sealed partial class SettingsWindow
{
    private void BuildAbout(Panel panel)
    {
        Note(panel, L.F(K.AboutVersion, AppBuildInfo.Version));
        Note(panel, L.F(K.AboutBuild, AppBuildInfo.Build));
        Note(panel, L.F(K.AboutRuntime, Environment.Version, AppBuildInfo.Runtime));
        Note(panel, L.F(K.AboutSchema, _main.Document.Version));
        Note(panel, L.F(K.AboutSavedVersion, _main.Document.SavedWithAppVersion.Length == 0 ? L.T(K.MigrationLegacy) : _main.Document.SavedWithAppVersion));
        var actions = new WrapPanel { Margin = new(0, 10, 0, 12) };
        DataButton(actions, L.T(K.AboutLicenses), () => new LicenseWindow(this).ShowDialog());
        DataButton(actions, L.T(K.UpdateOpenRelease), () => ShellLauncher.Launch(ReleaseClient.ReleasesPage.AbsoluteUri));
        DataButton(actions, L.T(K.DiagnosticsTitle), () => new DiagnosticsWindow(this, _main).ShowDialog());
        panel.Children.Add(actions);
        var support = new Hyperlink(new Run(L.T(K.AboutSupport))) { NavigateUri = new("https://buymeacoffee.com/LastResolution") };
        support.SetResourceReference(TextElement.ForegroundProperty, "TextBrush");
        support.RequestNavigate += (_, e) => { e.Handled = true; OpenExternal(e.Uri.AbsoluteUri); };
        panel.Children.Add(new TextBlock(support) { Margin = new(0, 14, 0, 0) });
    }

    private void BuildMigrationInfo(Panel panel)
    {
        Heading(panel, L.T(K.SettingsMigration));
        Note(panel, L.F(K.AboutSchema, _main.Document.Version));
        if (_store.IsReadOnly) Note(panel, L.T(K.MigrationReadOnly));
        if (_main.Document.LastUpgrade is { } upgrade)
        {
            Note(panel, L.F(K.MigrationLast, upgrade.FromAppVersion.Length == 0 ? L.T(K.MigrationLegacy) : upgrade.FromAppVersion,
                upgrade.ToAppVersion, upgrade.FromSchema, upgrade.ToSchema, upgrade.AtUtc.ToLocalTime().ToString("g", L.Culture)));
            Note(panel, L.F(K.MigrationBackup, Path.Combine(Path.GetDirectoryName(_store.FilePath)!, upgrade.BackupFile)));
        }
        else Note(panel, L.T(K.MigrationNone));
        Note(panel, L.T(K.MigrationRollbackHelp));
        var actions = new WrapPanel();
        var open = DataButton(actions, L.T(K.OpenDataFolder), () => OpenExternal(Path.GetDirectoryName(_store.FilePath)!));
        open.IsEnabled = !_main.IsPreview;
        panel.Children.Add(actions);
    }

    private void BuildUpdates(Panel panel)
    {
        Check(panel, L.T(K.UpdateOnStartup), _draft.CheckUpdatesOnStartup, value => _draft.CheckUpdatesOnStartup = value);
        Check(panel, L.T(K.UpdateIncludePrereleases), _draft.IncludePrereleaseUpdates, value => _draft.IncludePrereleaseUpdates = value);
        Note(panel, L.T(K.UpdateNoAutoInstall));
        Note(panel, L.T(K.UpdatePrivacy));
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new(0, 8, 0, 8) };
        var notes = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed };
        var checkedAt = new TextBlock { Margin = new(0, 3, 0, 8) };
        var buttons = new WrapPanel();
        Button? check = null;
        check = DataButton(buttons, L.T(K.UpdateCheckNow), async () =>
        {
            checkButton(false);
            try { await _main.CheckForUpdatesAsync(_draft.IncludePrereleaseUpdates); }
            finally { checkButton(true); }
        });
        void checkButton(bool enabled) { if (check != null) check.IsEnabled = enabled && !_main.IsPreview; if (!enabled) status.Text = L.T(K.UpdateChecking); }
        check.IsEnabled = !_main.IsPreview;
        var download = DataButton(buttons, L.T(K.UpdateDownload), async () => await _main.PrepareUpdateAsync());
        var cancel = DataButton(buttons, L.T(K.CommonCancel), _main.CancelUpdateDownload);
        Button? install = null;
        install = DataButton(buttons, L.T(K.UpdateRestart), async () =>
        {
            if (!ResolvePendingChanges() || !ConfirmationWindow.Ask(this, L.T(K.UpdateConfirmRestart))) return;
            installButton(false);
            if (await _main.BeginPreparedUpdateAsync())
            {
                Closed += (_, _) => _main.Dispatcher.BeginInvoke(() => ((App)Application.Current).ExitApplication());
                DiscardAndClose();
            }
            else { status.Text = L.T(K.UpdatePrepareFailed); installButton(true); }
        });
        void installButton(bool enabled) { if (install != null) install.IsEnabled = enabled; }
        var canInstall = _main.CanInstallUpdates;
        if (!canInstall && !_main.IsPreview) Note(panel, L.T(K.UpdatePortableRequired));
        DataButton(buttons, L.T(K.UpdateOpenRelease), () => ShellLauncher.Launch((_main.LastUpdateCheck?.Release?.Page ?? ReleaseClient.ReleasesPage).AbsoluteUri));
        panel.Children.Add(buttons); panel.Children.Add(status); panel.Children.Add(checkedAt); panel.Children.Add(notes);
        void Refresh()
        {
            status.Text = _main.IsPreview ? L.T(K.UpdatesDisabledInPreview) : _main.LastUpdateCheck is { } result ? MainWindow.UpdateStatusText(result) : "";
            if (_sectionButtons.TryGetValue(L.T(K.SettingsUpdates), out var navigation) && navigation.Content is TextBlock caption)
            {
                var ready = _main.UpdatePreparation == MainWindow.PreparationState.Ready;
                caption.Text = L.T(K.SettingsUpdates) + (ready ? " •" : "");
                navigation.ToolTip = ready ? L.F(K.UpdateReadyBody, _main.PreparedUpdate!.Version) : null;
                System.Windows.Automation.AutomationProperties.SetName(navigation, L.T(K.SettingsUpdates) + (ready ? " — " + L.T(K.UpdateReadyTitle) : ""));
            }
            var busy = _main.UpdatePreparation == MainWindow.PreparationState.Downloading;
            check!.IsEnabled = !_main.IsPreview && !busy;
            download.IsEnabled = canInstall && !busy && _main.UpdatePreparation != MainWindow.PreparationState.Ready && _main.LastUpdateCheck?.Status == UpdateCheckStatus.Available;
            cancel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            install!.Visibility = _main.UpdatePreparation == MainWindow.PreparationState.Ready ? Visibility.Visible : Visibility.Collapsed;
            install.IsEnabled = canInstall && _main.UpdatePreparation == MainWindow.PreparationState.Ready;
            status.Text = _main.UpdatePreparation switch
            {
                MainWindow.PreparationState.Downloading => L.F(K.UpdateDownloading, _main.UpdateBytes / (1024d * 1024)),
                MainWindow.PreparationState.Ready => L.F(K.UpdatePrepared, _main.PreparedUpdate!.Version),
                MainWindow.PreparationState.Failed => L.T(K.UpdatePrepareFailed),
                MainWindow.PreparationState.Cancelled => L.T(K.UpdateCancelled),
                _ => status.Text
            };
            checkedAt.Text = _main.LastUpdateCheckAt is { } time ? L.F(K.UpdateLastChecked, time.ToString("g", L.Culture)) : "";
            notes.Text = _main.LastUpdateCheck?.Release?.Notes ?? "";
            notes.Visibility = notes.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        Refresh();
        _main.UpdateCheckChanged += Refresh;
        _contentCleanup.Add(() => _main.UpdateCheckChanged -= Refresh);

    }
}

internal sealed class LicenseWindow : Window
{
    internal LicenseWindow(Window owner)
    {
        Owner = owner; Title = L.T(K.LicenseTitle); Width = 900; Height = 680; MinWidth = 640; MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Style = (Style)FindResource(typeof(Window)); ThemeWindow.Attach(this);
        var root = new DockPanel { Margin = new(24) };
        var close = new Button { Content = L.T(K.CommonClose), IsCancel = true, MinWidth = 100, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 12, 0, 0) };
        close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Bottom); root.Children.Add(close);
        var licenses = AppBuildInfo.Licenses();
        var select = new ComboBox { ItemsSource = licenses.Keys, SelectedIndex = 0, Margin = new(0, 0, 0, 12) };
        System.Windows.Automation.AutomationProperties.SetName(select, L.T(K.LicenseChoose));
        var text = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontSize = 12, FontFamily = new("Consolas") };
        select.SelectionChanged += (_, _) => { text.Text = select.SelectedItem is string name ? licenses[name] : ""; text.ScrollToHome(); };
        text.Text = licenses.Values.FirstOrDefault() ?? "";
        DockPanel.SetDock(select, Dock.Top); root.Children.Add(select); root.Children.Add(text); Content = root;
    }
}
