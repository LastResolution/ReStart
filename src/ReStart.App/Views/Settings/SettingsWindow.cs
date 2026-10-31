using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

internal sealed partial class SettingsWindow : Window
{
    private readonly MainWindow _main;
    private readonly LayoutStore _store;
    private Preferences _draft;
    private readonly TextBlock _feedback = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 0) };
    private bool _dataChanged;
    private readonly Dictionary<string, Button> _sectionButtons = [];

    public SettingsWindow(MainWindow main, LayoutStore store)
    {
        _main = main;
        ThemeWindow.Attach(this);
        Style = (Style)FindResource(typeof(Window));
        _store = store;
        _draft = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(main.Document.Preferences))!;
        if (_draft.ColorThemeId == null) _draft.ColorThemeId = ThemePresets.Match(_draft)?.Id ?? "";
        if (_draft.ColorThemeId.Length > 0) ThemePresets.Apply(_draft, _draft.ColorThemeId, ThemeWindow.SystemIsLight);
        if (!main.IsPreview) _draft.StartWithWindows = StartupRegistration.Current.IsEnabled;
        Title = L.T(K.SettingsTitle); Width = 940; Height = 740; MinWidth = 760; MinHeight = 500;
        WindowStartupLocation = main.IsPreview ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner; ShowInTaskbar = main.IsPreview;
        BuildContent();
        _baselineDraft = JsonSerializer.Serialize(_draft);
        _previewTimer = new(System.Windows.Threading.DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(120) };
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); ApplyDraftPreview(); };
        Closing += OnClosingSettings;
        Closed += (_, _) =>
        {
            _previewTimer.Stop(); DisposeContent(); _main.EndPreferencesPreview(this);
            CleanupBackgroundImports();
            if (_dataChanged) _main.RefreshLayout();
        };
    }

    private void BuildContent()
    {
        _buildingContent = true;
        DisposeContent(); _sectionButtons.Clear(); _presetButtons.Clear();
        _backgroundColor = null!; _accentColor = null!; _baseTheme = null!; _startAccent = null!;
        LibraryTiles = new(); _feedback.Text = "";
        if (_feedback.Parent is Panel previousFooter) previousFooter.Children.Remove(_feedback);
        Title = L.T(K.SettingsTitle); Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.Culture.IetfLanguageTag);
        var root = new DockPanel { Margin = new(28, 24, 28, 20) };
        var footer = new StackPanel { Margin = new(0, 12, 0, 0) };
        Note(footer, L.T(K.SettingsPreviewHelp));
        footer.Children.Add(_feedback);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var close = new Button { Content = L.T(K.CommonClose), MinWidth = 90, IsCancel = true };
        var save = new Button { Content = L.T(K.CommonSave), MinWidth = 100, Margin = new(12, 0, 0, 0), IsDefault = true };
        save.SetResourceReference(BackgroundProperty, "AccentBrush"); save.SetResourceReference(ForegroundProperty, "AccentTextBrush");
        save.Click += Save;
        buttons.Children.Add(close); buttons.Children.Add(save); footer.Children.Add(buttons);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var title = new TextBlock { Text = Title, FontSize = 26, Margin = new(0, 0, 0, 24) };
        DockPanel.SetDock(title, Dock.Top); root.Children.Add(title);
        var body = new Grid(); body.ColumnDefinitions.Add(new() { Width = new GridLength(180) }); body.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var navigation = new StackPanel { Margin = new(0, 0, 24, 0) }; body.Children.Add(navigation);
        var view = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new(0, 0, 16, 0) };
        Grid.SetColumn(view, 1); body.Children.Add(view);
        var navigationButtons = new List<Button>();
        StackPanel Section(TextKey key)
        {
            var name = L.T(key);
            var section = new StackPanel(); Heading(section, name);
            var button = new Button { Content = new TextBlock { Text = name, TextWrapping = TextWrapping.Wrap }, HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new(0, 0, 0, 4), Padding = new(12, 12, 8, 12) };
            button.Click += (_, _) => { _selectedSection = key; view.Content = section; view.ScrollToTop(); foreach (var b in navigationButtons) b.Background = Brushes.Transparent; button.SetResourceReference(BackgroundProperty, "TileBrush"); };
            navigation.Children.Add(button); navigationButtons.Add(button);
            _sectionButtons[name] = button;
            if (key == _selectedSection) { view.Content = section; button.SetResourceReference(BackgroundProperty, "TileBrush"); }
            return section;
        }
        var content = Section(K.SettingsStartup);
        Check(content, L.T(K.SettingsWindowsKey), _draft.CaptureWindowsKey, v => _draft.CaptureWindowsKey = v);
        Note(content, L.T(K.SettingsShortcutHelp));
        Check(content, L.T(K.SettingsStartButton), _draft.CaptureStartButton, value => _draft.CaptureStartButton = value);
        var autostart = Check(content, L.T(K.SettingsAutostart), _draft.StartWithWindows, v => _draft.StartWithWindows = v);
        autostart.IsEnabled = !_main.IsPreview;
        Note(content, L.T(_main.IsPreview ? K.PreviewAutostartHelp : K.StartupBackgroundHelp));
        if (!_main.IsPreview && StartupRegistration.Current.Command is string command) Note(content, L.T(K.StartupCommandPrefix) + command);
        Check(content, L.T(K.SettingsCloseOnDeactivate), _draft.CloseOnDeactivate, v => _draft.CloseOnDeactivate = v);
        Check(content, L.T(K.SettingsShowTaskbar), _draft.ShowTaskbarWhileOpen, v => _draft.ShowTaskbarWhileOpen = v);
        Note(content, L.T(K.SettingsTaskbarHelp));
        Heading(content, L.T(K.SettingsTablet));
        var tablet = Check(content, L.T(K.SettingsTabletEnable), _draft.TabletFeaturesEnabled, v => _draft.TabletFeaturesEnabled = v);
        var keyboardPolicy = Check(content, L.T(K.SettingsTabletKeyboard), _draft.TabletKeyboardMeansDesktop, v => _draft.TabletKeyboardMeansDesktop = v);
        var hideTaskbar = Check(content, L.T(K.SettingsTabletHide), _draft.TabletHideTaskbar, v => _draft.TabletHideTaskbar = v);
        void UpdateTabletControls() { keyboardPolicy.IsEnabled = hideTaskbar.IsEnabled = tablet.IsChecked == true; }
        tablet.Checked += (_, _) => UpdateTabletControls(); tablet.Unchecked += (_, _) => UpdateTabletControls(); UpdateTabletControls();
        Note(content, L.T(K.SettingsTabletHelp));
        var explorer = Check(content, L.T(K.ExplorerPinSetting), _draft.ExplorerContextMenu, v => _draft.ExplorerContextMenu = v);
        explorer.IsEnabled = !_main.IsPreview;
        Note(content, L.T(K.ExplorerPinHelp));
        content = Section(K.SettingsAppearance);
        BuildAppearance(content);
        BuildNavigationSettings(Section(K.SettingsStartExperience));
        content = Section(K.SettingsLanguage);
        var languages = new[] { "system" }.Concat(TextCatalog.SupportedLanguages).ToArray();
        _languageSelect = Select(content, L.T(K.SettingsDisplayLanguage), new[] { L.T(K.ThemeSystem) }.Concat(TextCatalog.SupportedLanguages.Select(tag => CultureInfo.GetCultureInfo(tag).NativeName)).ToArray(),
            Math.Max(0, Array.IndexOf(languages, _draft.Language)), i => _draft.Language = languages[i]);
        Note(content, L.T(K.SettingsLanguageHelp));
        content = Section(K.SettingsLayout);
        Note(content, L.T(K.SettingsLayoutHelp));
        var dataButtons = new WrapPanel { Margin = new(0, 10, 0, 4) };
        DataButton(dataButtons, L.T(K.CommonExport), Export);
        DataButton(dataButtons, L.T(K.CommonImport), Import);
        DataButton(dataButtons, L.T(K.ResetLayout), Reset);
        content.Children.Add(dataButtons);
        var backups = new WrapPanel { Margin = new(0, 10, 0, 4) };
        var createBackup = DataButton(backups, L.T(K.BackupCreate), ExportBackup); createBackup.IsEnabled = !_store.IsReadOnly;
        var restoreBackup = DataButton(backups, L.T(K.BackupRestore), RestoreBackup);
        restoreBackup.IsEnabled = !_store.IsReadOnly && !_main.Document.Preferences.LayoutLocked && _main.Document.Preferences.LockedGroupIds.Count == 0;
        content.Children.Add(backups); Note(content, L.T(K.BackupHelp));
        Note(content, L.T(K.StoragePrefix) + _store.FilePath);
        Note(content, L.T(K.SettingsPrivacy));
        BuildMigrationInfo(content);
        content = Section(K.SettingsScriptTiles);
        Check(content, L.T(K.ShowLiveTileNames), _draft.ShowLiveTileNames, v => _draft.ShowLiveTileNames = v);
        Check(content, L.T(K.PauseOffscreenLiveTiles), _draft.PauseOffscreenLiveTiles, v => _draft.PauseOffscreenLiveTiles = v);
        Check(content, L.T(K.AutoRecoverLiveTiles), _draft.AutoRecoverLiveTiles, v => _draft.AutoRecoverLiveTiles = v);
        Heading(content, L.T(K.BundledTiles));
        var bundled = new WrapPanel { Margin = new(0, 0, 0, 16) };
        foreach (var id in ScriptTilePackages.BundledIds)
        {
            try
            {
                var manifest = ScriptTilePackages.BundledManifest(id);
                var titleText = manifest.LocalizedTitles.GetValueOrDefault(L.Language, manifest.Title);
                var button = new Button
                {
                    Content = new TextBlock { Text = titleText, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
                    Width = 152,
                    Height = 70,
                    Margin = new(0, 0, 8, 8),
                    Foreground = Brushes.White,
                    ToolTip = L.F(K.AddBundledTile, titleText)
                };
                var color = manifest.Settings.TryGetProperty("color", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (color?.Length != 7 || color[0] != '#' || !color.Skip(1).All(Uri.IsHexDigit)) color = "#0078D7";
                button.Background = (Brush)new BrushConverter().ConvertFromString(color!)!;
                if (SystemParameters.HighContrast) { button.Background = SystemColors.WindowBrush; button.Foreground = SystemColors.WindowTextBrush; }
                button.Click += (_, _) =>
                {
                    var tile = _main.AddBundledScriptTile(id);
                    if (tile == null) return;
                    if (manifest.Capabilities.Contains("images.slideshow")) _main.ChooseSlideshowFolder(tile, this);
                    if (manifest.Capabilities.Contains("data.sources")) _main.ConfigureTileData(tile, this);
                    _feedback.Text = L.T(K.StatusScriptAdded);
                };
                bundled.Children.Add(button);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException) { }
        }
        content.Children.Add(bundled);
        BuildTileLibrary(content);
        Note(content, L.T(K.SystemMetricsHelp));
        Note(content, L.T(K.ScriptAuthoringHelp));
        var scriptButtons = new WrapPanel();
        DataButton(scriptButtons, L.T(K.StudioTitle), () => _main.OpenTileStudio(owner: this));
        DataButton(scriptButtons, L.T(K.ImportScriptTile), () => { _main.ImportScriptTile(); });
        content.Children.Add(scriptButtons);
        Note(content, L.T(K.ScriptCapabilitiesHelp));
        content = Section(K.SettingsUpdates);
        BuildUpdates(content);
        content = Section(K.SettingsAbout);
        BuildAbout(content);
        var links = new WrapPanel { Margin = new(0, 10, 0, 0) };
        DataButton(links, "GitHub / README", () => ShellLauncher.Launch("https://github.com/LastResolution/ReStart"));
        DataButton(links, L.T(K.OpenWindowsSettings), () => ShellLauncher.Launch("ms-settings:"));
        DataButton(links, L.T(K.AppExit), RequestExit);
        content.Children.Add(links);
        root.Children.Add(body);
        Content = root;
        _buildingContent = false;
    }

    private static System.Windows.Media.Brush Resource(string key) => (System.Windows.Media.Brush)Application.Current.FindResource(key);
    private static void Heading(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, FontSize = 20, Margin = new(0, 0, 0, 16) });
    private static void Note(Panel panel, string text)
    {
        var note = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new(0, 3, 0, 8), LineHeight = 20 };
        note.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); panel.Children.Add(note);
    }
    private CheckBox Check(Panel panel, string label, bool initial, Action<bool> changed)
    {
        var checkbox = new CheckBox { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }, IsChecked = initial };
        System.Windows.Automation.AutomationProperties.SetName(checkbox, label);
        checkbox.Checked += (_, _) => { changed(true); DraftChanged(); }; checkbox.Unchecked += (_, _) => { changed(false); DraftChanged(); }; panel.Children.Add(checkbox); return checkbox;
    }
    private ComboBox Select(Panel panel, string label, string[] options, int index, Action<int> changed)
    {
        var row = new DockPanel { Margin = new(0, 10, 0, 7) };
        row.Children.Add(new TextBlock { Text = label, Width = 170, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        var select = new ComboBox { ItemsSource = options, SelectedIndex = index, MinWidth = 180, HorizontalAlignment = HorizontalAlignment.Left, Padding = new(6) };
        System.Windows.Automation.AutomationProperties.SetName(select, label);
        select.SelectionChanged += (_, _) => { changed(select.SelectedIndex); DraftChanged(); }; row.Children.Add(select); panel.Children.Add(row); return select;
    }
    /// <summary>Adds a named slider and localized value, keeping numeric blur radii distinct from percentages.</summary>
    /// <param name="panel">Settings section receiving the row.</param>
    /// <param name="label">Localized label and accessible slider name.</param>
    /// <param name="min">Inclusive lower bound in the caller's units.</param>
    /// <param name="max">Inclusive upper bound in the caller's units.</param>
    /// <param name="value">Initial normalized draft value.</param>
    /// <param name="changed">Updates the settings draft after a value change; no save is performed here.</param>
    /// <param name="snap">Whether input snaps to the configured tick interval.</param>
    /// <param name="percentage">True formats fractions as percentages with 0.05 ticks; false displays integers with unit ticks.</param>
    private void Slide(Panel panel, string label, double min, double max, double value, Action<double> changed, bool snap, bool percentage = true)
    {
        string Display(double number) => number.ToString(percentage ? "P0" : "N0", L.Culture);
        var row = new DockPanel { Margin = new(0, 12, 0, 7) };
        row.Children.Add(new TextBlock { Text = label, Width = 170 });
        var percent = new TextBlock { Text = Display(value), Width = 48, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(percent, Dock.Right); row.Children.Add(percent);
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = percentage ? 0.05 : 1, IsSnapToTickEnabled = snap, SmallChange = percentage ? 0.05 : 1, LargeChange = percentage ? 0.1 : 5, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 10, 0) };
        System.Windows.Automation.AutomationProperties.SetName(slider, label);
        slider.ValueChanged += (_, _) => { changed(slider.Value); percent.Text = Display(slider.Value); DraftChanged(); }; row.Children.Add(slider); panel.Children.Add(row);
    }
    private static Button DataButton(Panel panel, string label, Action action)
    {
        var button = new Button { Content = label, Margin = new(0, 0, 8, 6) };
        button.SetResourceReference(BackgroundProperty, "TileBrush");
        button.Click += (_, _) => action(); panel.Children.Add(button); return button;
    }
    private void OpenExternal(string target)
    {
        try
        {
            if (!ResolvePendingChanges()) return;
            ShellLauncher.Launch(target);
            // A modal owner remains topmost while its dialog is open. Close the
            // dialog first, then dismiss Start after ShowDialog has unwound.
            Closed += (_, _) => _main.Dispatcher.BeginInvoke(() => _main.Dismiss(immediately: true));
            DiscardAndClose();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { _feedback.Text = ex.Message; }
    }
    internal void SelectSection(string name)
    {
        var button = _sectionButtons[name];
        button.Focus();
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    private void Save(object sender, RoutedEventArgs e)
    {
        if (SaveDraft()) { _allowClose = true; DialogResult = true; }
    }

    private bool SaveDraft()
    {
        if (!ValidateColors() || !ValidateBackgroundImage()) return false;
        var previous = _main.Document.Preferences;
        StartupRegistration.Entry? registration = null;
        try
        {
            if (!_main.IsPreview)
            {
                registration = StartupRegistration.Current.Capture();
                StartupRegistration.Current.SetEnabled(_draft.StartWithWindows);
            }
            _main.Document.Preferences = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(_draft))!;
            _store.Save(_main.Document);
            _baselineDraft = JsonSerializer.Serialize(_draft); _main.EndPreferencesPreview(this); return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
        {
            _main.Document.Preferences = previous;
            if (registration != null)
            {
                try { StartupRegistration.Current.Restore(registration); }
                catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException or System.Security.SecurityException) { _feedback.Text = L.T(K.ErrorStartupRollback) + rollback.Message; return false; }
            }
            _feedback.Text = L.T(K.ErrorSave) + ex.Message;
            return false;
        }
    }
    private void Export()
    {
        var dialog = new SaveFileDialog { Title = L.T(K.ExportLayout), FileName = "ReStart-layout.json", Filter = "JSON|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_main.Document, LayoutStore.JsonOptions)); _feedback.Text = L.T(K.StatusExported); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _feedback.Text = ex.Message; }
    }
    private void Import()
    {
        var dialog = new OpenFileDialog { Title = L.T(K.ImportLayout), Filter = "JSON|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var imported = LayoutStore.Read(dialog.FileName);
            if (!ConfirmationWindow.Ask(this, L.T(K.ConfirmImport))) return;
            // Preferences are local to this PC; importing a layout must not enable hooks or autostart.
            imported.Preferences = _main.Document.Preferences;
            _main.ReplaceDocument(imported); _dataChanged = true; _feedback.Text = L.T(K.StatusImported);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException or InvalidOperationException or NullReferenceException) { _feedback.Text = L.T(K.ErrorRead) + ex.Message; }
    }
    private void Reset()
    {
        if (!ConfirmationWindow.Ask(this, L.T(K.ConfirmReset))) return;
        var document = _main.CreateInitialLayout(); document.Preferences = _main.Document.Preferences;
        try { _main.ReplaceDocument(document); _dataChanged = true; _feedback.Text = L.T(K.StatusLayoutReset); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { _feedback.Text = L.T(K.ErrorSave) + ex.Message; }
    }
}
