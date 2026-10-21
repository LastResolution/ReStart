using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ReStart.App.Services;
using ReStart.App.Controls;
using ReStart.Core;

namespace ReStart.App;

/// <summary>Edits local HTML/CSS/browser JavaScript packages and legacy native drafts with four isolated size previews.</summary>
internal sealed class TileStudioWindow : Window
{
    private readonly MainWindow _main;
    private readonly bool _offscreen;
    private Tile? _tile;
    private string? _libraryId;
    private readonly TextBox _source = Editor(ScriptTileProtocol.MaxSource);
    private readonly TextBox _html = Editor(262144);
    private readonly TextBox _css = Editor(262144);
    private readonly TextBox _manifest = Editor(16384);
    private readonly TextBox _samples = Editor(16384);
    private readonly TextBox _input = Editor(131072);
    private readonly TextBox _feedback = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 76, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new(0, 10, 0, 10) };
    private readonly CheckBox _auto = new() { IsChecked = true, Margin = new(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly ComboBox _templates = new() { MinWidth = 180, MaxWidth = 280, Margin = new(8, 0, 8, 0) };
    private ComboBox _documents = null!;
    private readonly Grid _editorHost = new() { Margin = new(0, 8, 12, 0) };
    private readonly WrapPanel _previews = new();
    private readonly Dictionary<TileSize, Border> _cards = [];
    private readonly Dictionary<TileSize, JsonElement> _states = [];
    private readonly Dictionary<TileSize, HtmlTileView> _webPreviews = [];
    private IReadOnlyDictionary<string, ScriptTilePackages.WebAsset> _previewAssets = new Dictionary<string, ScriptTilePackages.WebAsset>();
    private readonly ScriptTileWorkerPool _workers = new();
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private int _revision;
    private bool _loading, _dirty, _rendering, _pending, _closed;
    private readonly TextBlock _repairWarning = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 8), Visibility = Visibility.Collapsed };
    internal bool PreviewSucceeded { get; private set; }
    internal string SourceText { get => _source.Text; set => _source.Text = value; }
    internal int PreviewCount => _cards.Values.Count(card => card.Child is Button);
    internal string PreviewError => _feedback.Text;

    internal TileStudioWindow(MainWindow main, Tile? tile = null, bool offscreen = false)
    {
        _main = main; _tile = tile; _offscreen = offscreen;
        Style = (Style)FindResource(typeof(Window)); ThemeWindow.Attach(this);
        Title = L.T(K.StudioTitle); Width = 1240; Height = 820; MinWidth = 920; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false; UseLayoutRounding = true;
        var root = new DockPanel { Margin = new(24) };
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        footer.Children.Add(_feedback);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        ActionButton(actions, K.CommonClose, Close);
        ActionButton(actions, tile == null ? K.StudioSaveLibrary : K.CommonSave, () => Save(false));
        if (tile == null) ActionButton(actions, K.StudioSaveAndPin, () => Save(true), accent: true).IsEnabled = main.CanPinStudioTile;
        footer.Children.Add(actions);
        var heading = new StackPanel(); DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        heading.Children.Add(new TextBlock { Text = L.T(K.StudioTitle), FontSize = 24, Margin = new(0, 0, 0, 8) });
        heading.Children.Add(new TextBlock { Text = L.T(K.StudioHelp), TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 12) });
        heading.Children.Add(_repairWarning);
        var toolbar = new WrapPanel { Margin = new(0, 0, 0, 8) };
        toolbar.Children.Add(new TextBlock { Text = L.T(K.StudioTemplate), VerticalAlignment = VerticalAlignment.Center });
        foreach (var id in ScriptTilePackages.BundledIds)
        {
            var package = ScriptTilePackages.BundledManifest(id);
            _templates.Items.Add(new ComboBoxItem { Content = package.LocalizedTitles.GetValueOrDefault(L.Language, package.Title), Tag = id });
        }
        _templates.SelectedIndex = ScriptTilePackages.BundledIds.ToList().IndexOf("clock"); toolbar.Children.Add(_templates);
        ActionButton(toolbar, K.StudioLoadTemplate, () =>
        {
            if (_dirty && !ConfirmationWindow.Ask(this, L.T(K.StudioDiscard))) return;
            LoadTemplate((string)((ComboBoxItem)_templates.SelectedItem).Tag); _dirty = true; SchedulePreview();
        });
        ActionButton(toolbar, K.StudioPreview, async () => await PreviewAsync());
        _auto.Content = L.T(K.StudioAutoPreview); toolbar.Children.Add(_auto);
        _auto.Checked += (_, _) => SchedulePreview(); _auto.Unchecked += (_, _) => _debounce.Stop();
        ActionButton(toolbar, K.StudioResetState, async () => { _states.Clear(); await PreviewAsync(); });
        heading.Children.Add(toolbar);
        var columns = new Grid(); columns.ColumnDefinitions.Add(new() { Width = new(3, GridUnitType.Star) }); columns.ColumnDefinitions.Add(new() { Width = new(2, GridUnitType.Star), MinWidth = 300 });
        root.Children.Add(columns);
        var editors = new DockPanel(); columns.Children.Add(editors);
        var selector = new ComboBox { ItemsSource = new[] { "tile.js", "index.html", "tile.css", "manifest.json", L.T(K.StudioSamples), L.T(K.StudioInput) }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 200, Margin = new(0, 0, 12, 0) };
        _documents = selector;
        AutomationProperties.SetName(selector, L.T(K.StudioEditor)); DockPanel.SetDock(selector, Dock.Top); editors.Children.Add(selector);
        foreach (var editor in new[] { _source, _html, _css, _manifest, _samples, _input }) { _editorHost.Children.Add(editor); editor.Visibility = editor == _source ? Visibility.Visible : Visibility.Collapsed; }
        _input.IsReadOnly = true;
        selector.SelectionChanged += (_, _) =>
        {
            var fields = new[] { _source, _html, _css, _manifest, _samples, _input };
            for (var i = 0; i < fields.Length; i++) fields[i].Visibility = i == selector.SelectedIndex ? Visibility.Visible : Visibility.Collapsed;
            fields[selector.SelectedIndex].Focus();
        };
        editors.Children.Add(_editorHost);
        var previewHost = new DockPanel { Margin = new(8, 0, 0, 0) }; Grid.SetColumn(previewHost, 1); columns.Children.Add(previewHost);
        var hint = new TextBlock { Text = L.T(K.StudioClickHint), TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 8) }; DockPanel.SetDock(hint, Dock.Top); previewHost.Children.Add(hint);
        foreach (var (size, label) in new[] { (TileSize.Small, K.TileSmall), (TileSize.Medium, K.TileMedium), (TileSize.Wide, K.TileWide), (TileSize.Large, K.TileLarge) })
        {
            var card = new StackPanel { Margin = new(0, 0, 14, 16), MinWidth = 100 };
            card.Children.Add(new TextBlock { Text = L.T(label), Margin = new(0, 0, 0, 6) });
            var border = new Border { HorizontalAlignment = HorizontalAlignment.Left };
            _cards[size] = border; card.Children.Add(border); _previews.Children.Add(card);
        }
        previewHost.Children.Add(new ScrollViewer { Content = _previews, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
        LoadTemplate("clock"); _dirty = false;
        if (tile != null)
            try
            {
                var package = main.ScriptPackages.Read(tile.ScriptId);
                _loading = true; _manifest.Text = File.ReadAllText(Path.Combine(main.ScriptPackages.Folder(tile.ScriptId), "manifest.json")); _source.Text = package.Source;
                if (package.Manifest.Renderer == "web") { var web = main.ScriptPackages.ReadWeb(tile.ScriptId); _html.Text = web.Html; _css.Text = web.Css; _previewAssets = main.ScriptPackages.WebAssets(tile.ScriptId); }
                _samples.Text = TileStudioContext.Sample(package.Manifest); _loading = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            { _repairWarning.Text = L.T(K.StudioMissing); _repairWarning.Visibility = Visibility.Visible; _loading = false; }
        _source.TextChanged += Changed; _manifest.TextChanged += Changed; _html.TextChanged += Changed; _css.TextChanged += Changed;
        _samples.TextChanged += (_, _) => { if (!_loading) { _revision++; SchedulePreview(); } };
        _debounce.Tick += async (_, _) => { _debounce.Stop(); await PreviewAsync(); };
        Loaded += async (_, _) => { await PreviewAsync(); _source.Focus(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { Save(false); e.Handled = true; }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { _ = PreviewAsync(); e.Handled = true; }
        };
        Closing += ConfirmClose;
        Closed += (_, _) => { _closed = true; _revision++; _debounce.Stop(); _workers.Dispose(); foreach (var view in _webPreviews.Values) view.Dispose(); };
    }

    private static TextBox Editor(int maximum) => new()
    {
        AcceptsReturn = true,
        AcceptsTab = true,
        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
        FontSize = 13,
        MaxLength = maximum,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        TextWrapping = TextWrapping.NoWrap,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };

    private static Button ActionButton(Panel panel, K key, Action action, bool accent = false)
    {
        var button = new Button { Content = L.T(key), Margin = new(0, 0, 8, 0) };
        if (accent) { button.SetResourceReference(BackgroundProperty, "AccentBrush"); button.SetResourceReference(ForegroundProperty, "AccentTextBrush"); }
        button.Click += (_, _) => action(); panel.Children.Add(button); return button;
    }

    private void LoadTemplate(string id)
    {
        _loading = true;
        try
        {
            var path = ScriptTilePackages.BundledManifestPath(id); var json = File.ReadAllText(path);
            _manifest.Text = json; _source.Text = File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "tile.js"));
            var manifest = ScriptTilePackages.ValidateDraft(json, _source.Text);
            _previewAssets = manifest.Renderer == "web" ? ScriptTilePackages.ReadAssets(Path.GetDirectoryName(path)!) : new Dictionary<string, ScriptTilePackages.WebAsset>();
            if (manifest.Renderer == "web") { var web = ScriptTilePackages.ReadWebFolder(Path.GetDirectoryName(path)!); _html.Text = web.Html; _css.Text = web.Css; }
            else { _html.Text = ""; _css.Text = ""; }
            _samples.Text = TileStudioContext.Sample(manifest);
            _states.Clear(); _revision++;
        }
        finally { _loading = false; }
    }

    private void Changed(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        _dirty = true; _revision++; SchedulePreview();
    }

    private void SchedulePreview()
    {
        PreviewSucceeded = false; _debounce.Stop();
        if (_auto.IsChecked == true && !_closed) _debounce.Start();
    }

    internal async Task PreviewAsync(TileSize? clicked = null)
    {
        if (_closed) return;
        if (_rendering) { _pending = true; return; }
        _debounce.Stop(); _rendering = true; PreviewSucceeded = false;
        var revision = _revision;
        try
        {
            var source = _source.Text; var samples = _samples.Text; var html = _html.Text; var css = _css.Text;
            var manifest = ScriptTilePackages.ValidateDraft(_manifest.Text, source);
            if (manifest.Renderer == "web" && (System.Text.Encoding.UTF8.GetByteCount(html) > 262144 || System.Text.Encoding.UTF8.GetByteCount(css) > 262144))
                throw new InvalidDataException(L.T(K.ErrorScriptRequestSize));
            var title = manifest.LocalizedTitles.GetValueOrDefault(L.Language, manifest.Title);
            var now = DateTimeOffset.Now;
            var errors = await Task.WhenAll(_cards.Keys.Select(async size =>
            {
                var state = _states.GetValueOrDefault(size, JsonSerializer.SerializeToElement(new { }));
                var context = TileStudioContext.Build(manifest, size, _main.Document.Preferences.TileScale, samples, state, clicked == size, now,
                    ThemeWindow.IsLight ? "light" : "dark", SystemParameters.HighContrast, !_main.Document.Preferences.Animate, _main.Document.Preferences.Accent, _offscreen, _main.Document.Preferences.ShowLiveTileNames);
                if (size == (clicked ?? TileSize.Wide)) _input.Text = JsonSerializer.Serialize(context, new JsonSerializerOptions(ScriptTileProtocol.Json) { WriteIndented = true });
                HtmlTileView? web = null;
                if (manifest.Renderer == "web")
                {
                    if (!_webPreviews.TryGetValue(size, out web)) _webPreviews[size] = web = new(_main.ScriptPackages.BrowserDataRoot, Guid.NewGuid().ToString("N"), _offscreen);
                    web.SetAssets(_previewAssets);
                    if (web.Parent is Button old) old.Content = null;
                    var (width, height) = TileLayout.Dimensions(size);
                    var preview = new Button
                    {
                        Content = web,
                        Width = width * 52 * _main.Document.Preferences.TileScale - 4,
                        Height = height * 52 * _main.Document.Preferences.TileScale - 4,
                        Padding = new(0),
                        BorderThickness = new(0),
                        Background = Brushes.Transparent,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        VerticalContentAlignment = VerticalAlignment.Stretch
                    };
                    var target = web;
                    preview.PreviewMouseWheel += async (_, e) =>
                    {
                        e.Handled = true;
                        if (!await target.ScrollAsync(e.GetPosition(preview), e.Delta, Keyboard.Modifiers == ModifierKeys.Shift))
                        {
                            var parent = System.Windows.Media.VisualTreeHelper.GetParent(_previews);
                            while (parent != null && parent is not ScrollViewer) parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                            if (parent is ScrollViewer scroll) scroll.ScrollToVerticalOffset(scroll.VerticalOffset - e.Delta / 120d * 48);
                        }
                    };
                    preview.PreviewKeyDown += async (_, e) =>
                    {
                        if (e.Key is not (Key.PageUp or Key.PageDown) || Keyboard.Modifiers != ModifierKeys.None) return;
                        e.Handled = true;
                        await target.ScrollAsync(new(preview.Width / 2, preview.Height / 2), e.Key == Key.PageUp ? 120 : -120, page: true);
                    };
                    preview.Click += async (_, _) => await PreviewAsync(size); _cards[size].Child = preview;
                }
                var (frame, error) = web != null ? await web.RenderAsync(html, css, source, false, context) : await _workers.RenderAsync(source, context);
                if (_closed || revision != _revision) return null;
                if (frame == null) return error ?? L.T(K.ErrorScript);
                _states[size] = frame.State.Clone();
                if (web == null)
                {
                    var button = _main.CreateStudioPreview(size, title, frame);
                    button.Click += async (_, _) => await PreviewAsync(size);
                    _cards[size].Child = button;
                }
                return null;
            }));
            if (_closed || revision != _revision) return;
            PreviewSucceeded = errors.All(error => error == null);
            _feedback.Text = PreviewSucceeded ? L.T(K.StudioPreviewSuccess) : string.Join(Environment.NewLine, errors.Where(error => error != null).Distinct());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or InvalidOperationException)
        { if (!_closed) _feedback.Text = ex.Message; }
        finally
        {
            _rendering = false;
            if (_pending && !_closed) { _pending = false; _ = Dispatcher.BeginInvoke(async () => await PreviewAsync()); }
        }
    }

    private async void Save(bool pin)
    {
        try
        {
            if (pin)
            {
                if (!_main.CanPinStudioTile) { _feedback.Text = L.T(K.LayoutLockedStatus); return; }
                await PreviewAsync(); if (!PreviewSucceeded || _closed) return;
            }
            if (_tile != null)
            { _main.ScriptPackages.SaveInstalled(_tile.ScriptId, _manifest.Text, _source.Text, _html.Text, _css.Text); _main.ReloadStudioTile(_tile); _repairWarning.Visibility = Visibility.Collapsed; }
            else
            {
                var path = _main.ScriptPackages.SaveLibrary(_manifest.Text, _source.Text, _libraryId, _html.Text, _css.Text);
                _libraryId = Path.GetFileName(Path.GetDirectoryName(path));
                if (pin) _tile = _main.AddScriptPackage(path);
            }
            _dirty = false; _feedback.Text = L.T(K.StudioSaved);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or InvalidOperationException)
        { _feedback.Text = ex.Message; }
    }

    private void ConfirmClose(object? sender, CancelEventArgs e)
    {
        if (_dirty && !ConfirmationWindow.Ask(this, L.T(K.StudioDiscard))) e.Cancel = true;
    }

    internal Task FreezeForDocumentationAsync() => Task.WhenAll(_webPreviews.Values.Select(view => view.FreezeForDocumentationAsync()));

    /// <summary>Selects an unmodified bundled draft and editor for hidden product documentation.</summary>
    /// <param name="template">Bundled package identifier; no user data or connections are loaded.</param>
    /// <param name="document">Zero-based editor index: JavaScript, HTML, CSS, manifest, sample JSON or context.</param>
    internal void PrepareDocumentation(string template, int document)
    {
        LoadTemplate(template); _dirty = false;
        _templates.SelectedIndex = ScriptTilePackages.BundledIds.ToList().IndexOf(template);
        _documents.SelectedIndex = document;
        foreach (var editor in new[] { _source, _html, _css, _manifest, _samples, _input }) editor.FontSize = 16;
    }
}
