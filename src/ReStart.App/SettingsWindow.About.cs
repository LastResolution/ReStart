using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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
        panel.Children.Add(actions);
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
        var open = DataButton(actions, L.T(K.OpenDataFolder), () => ShellLauncher.Launch(Path.GetDirectoryName(_store.FilePath)!));
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
        DataButton(buttons, L.T(K.UpdateOpenRelease), () => ShellLauncher.Launch((_main.LastUpdateCheck?.Release?.Page ?? ReleaseClient.ReleasesPage).AbsoluteUri));
        panel.Children.Add(buttons); panel.Children.Add(status); panel.Children.Add(checkedAt); panel.Children.Add(notes);
        void Refresh()
        {
            status.Text = _main.IsPreview ? L.T(K.UpdatesDisabledInPreview) : _main.LastUpdateCheck is { } result ? MainWindow.UpdateStatusText(result) : "";
            checkedAt.Text = _main.LastUpdateCheckAt is { } time ? L.F(K.UpdateLastChecked, time.ToString("g", L.Culture)) : "";
            notes.Text = _main.LastUpdateCheck?.Release?.Notes ?? "";
            notes.Visibility = notes.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        Refresh();
        _main.UpdateCheckChanged += Refresh;
        Closed += (_, _) => _main.UpdateCheckChanged -= Refresh;
        Heading(panel, L.T(K.UpdateAuth));
        Note(panel, L.T(K.UpdateAuthHelp));
        var credentialStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new(0, 4, 0, 10) };
        void RefreshCredential()
        {
            if (_main.IsPreview) { credentialStatus.Text = L.T(K.UpdatesDisabledInPreview); return; }
            try { credentialStatus.Text = L.T(ReleaseCredentialStore.Current.Read() == null ? K.UpdateAuthMissing : K.UpdateAuthSaved); }
            catch (Win32Exception) { credentialStatus.Text = L.T(K.UpdateCredentialError); }
        }
        RefreshCredential(); panel.Children.Add(credentialStatus);
        Note(panel, L.T(K.TokenLabel));
        var token = new PasswordBox { MaxLength = 2048, Padding = new(8), Margin = new(0, 0, 0, 10), IsEnabled = !_main.IsPreview };
        token.SetResourceReference(ForegroundProperty, "TextBrush"); token.SetResourceReference(BackgroundProperty, "ControlBrush");
        token.SetResourceReference(BorderBrushProperty, "BorderBrush");
        System.Windows.Automation.AutomationProperties.SetName(token, L.T(K.TokenLabel));
        panel.Children.Add(token);
        var credentials = new WrapPanel();
        var saveToken = DataButton(credentials, L.T(K.UpdateSaveToken), () =>
        {
            if (_main.IsPreview) return;
            try { ReleaseCredentialStore.Current.Save(token.Password); token.Clear(); RefreshCredential(); _feedback.Text = L.T(K.TokenSaved); }
            catch (Exception ex) when (ex is Win32Exception or ArgumentException) { _feedback.Text = L.T(K.UpdateCredentialError); }
        });
        saveToken.IsEnabled = !_main.IsPreview;
        DataButton(credentials, L.T(K.UpdateRemoveToken), () =>
        {
            if (_main.IsPreview) return;
            try { ReleaseCredentialStore.Current.Delete(); token.Clear(); RefreshCredential(); _feedback.Text = L.T(K.TokenRemoved); }
            catch (Win32Exception) { _feedback.Text = L.T(K.UpdateCredentialError); }
        }).IsEnabled = !_main.IsPreview;
        token.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) { saveToken.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; }
        };
        panel.Children.Add(credentials);
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
