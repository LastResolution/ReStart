using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using ReStart.App.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

/// <summary>Hosts the full-screen launcher and coordinates local models, native input, and tile rendering.</summary>
public partial class MainWindow : Window
{
    private readonly LayoutStore _store;
    private readonly bool _preview;
    private readonly AppCatalog _catalog = new();
    private readonly Func<Task<List<AppEntry>>> _loadCatalog;
    private readonly bool _catalogFixture;
    private readonly DispatcherTimer _clock;
    private readonly List<(TextBlock Text, Tile Tile)> _liveLabels = [];
    private readonly Dictionary<string, FrameworkElement> _sections = [];
    private List<AppEntry> _apps = [];
    private bool _allApps, _dragging, _ready;
    private Point _dragOrigin;
    private Point _dragOffset;
    private GroupHeader? _editingGroupHeader;
    private bool _groupsRenderPending;
    private DropAdorner? _dropAdorner;
    private long _lastDragScroll;
    private int _dialogDepth;
    private Tile? _openFolder;
    private bool _exit;
    private bool _searchRequested;
    private bool _focusOnActivation;
    private bool _verifyNativeActivation;
    private System.Drawing.Point? _startButtonPoint;
    private Task? _initialization;
    /// <summary>Gets the current local layout document.</summary>
    public LayoutDocument Document { get; private set; } = new();
    /// <summary>Gets or sets the callback that reapplies shell integration after loading or saving preferences.</summary>
    public Action? PreferencesChanged { get; set; }
    /// <summary>Gets whether initial catalog loading has completed, including a reported failure.</summary>
    public bool IsReady => _ready;
    /// <summary>Gets whether hooks, startup registration, and real user data are isolated for verification.</summary>
    public bool IsPreview => _preview;
    /// <summary>Gets whether background icon enumeration has completed.</summary>
    public bool IconsReady { get; private set; }
    /// <summary>Gets the number of real applications enumerated for diagnostics.</summary>
    public int CatalogCount => _catalog.Count;
    /// <summary>Gets the number of logical Start-menu directory paths found for all-apps diagnostics.</summary>
    public int CatalogFolderCount => _catalog.FolderCount;
    /// <summary>Gets the number of icons loaded for diagnostics.</summary>
    public int IconCount => _catalog.Icons.Count;
    /// <summary>Creates a localized default layout from the currently loaded app catalog.</summary>
    public LayoutDocument CreateInitialLayout() => SeedLayout.Create(_apps);

    /// <summary>Hosts the full-screen launcher and coordinates local models, native input, and tile rendering.</summary>
    public MainWindow(LayoutStore store, bool preview) : this(store, preview, null) { }

    internal MainWindow(LayoutStore store, bool preview, Func<Task<List<AppEntry>>>? catalogLoader)
    {
        if (!preview && catalogLoader != null) throw new ArgumentException("Catalog fixtures require preview mode.", nameof(catalogLoader));
        _store = store;
        _preview = preview;
        _catalogFixture = catalogLoader != null;
        _loadCatalog = catalogLoader ?? _catalog.LoadAsync;
        _scriptPackages = new(Path.GetDirectoryName(store.FilePath)!);
        IsVisibleChanged += (_, _) => { if (!IsVisible) PauseScripts(); };
        InitializeComponent();
        InstallGroupBoundaries();
        ThemeWindow.Attach(this);
        WatchDesktopAppearance();
        Activated += (_, _) =>
        {
            if (!_focusOnActivation) return;
            Dispatcher.BeginInvoke(() =>
            {
                if (IsVisible && IsActive && _focusOnActivation && _dialogDepth == 0)
                {
                    if (Keyboard.FocusedElement == null || Keyboard.FocusedElement == this || SearchBox.IsKeyboardFocused) FocusSearchInput();
                    _focusOnActivation = false;
                }
            }, DispatcherPriority.Input);
        };
        PinnedNav.AllowDrop = true;
        PinnedNav.PreviewDragOver += (_, e) => { if (HasDrag(e)) { ShowPinned(this, new()); e.Effects = DragDropEffects.Move; e.Handled = true; } };
        ContentScroll.AllowDrop = true;
        ContentScroll.DragLeave += (_, e) =>
        {
            if (new Rect(new Point(), ContentScroll.RenderSize).Contains(e.GetPosition(ContentScroll))) return;
            _folderHover.Reset(); StopFolderDwell(); ClearDropPreview();
        };
        ContentScroll.PreviewMouseWheel += (_, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift && ContentScroll.ScrollableWidth > 0)
            { ContentScroll.ScrollToHorizontalOffset(ContentScroll.HorizontalOffset - e.Delta); e.Handled = true; }
        };
        GroupsPanel.PreviewDragOver += OnBoardDragOver;
        GroupsPanel.PreviewDrop += OnBoardDrop;
        GroupsPanel.RequestBringIntoView += (_, e) => { if (_dragging) e.Handled = true; };
        ContentScroll.PreviewDragOver += (_, e) =>
        {
            if (!HasDrag(e)) return;
            var now = Environment.TickCount64;
            if (now - _lastDragScroll < 80) return;
            _lastDragScroll = now;
            var point = e.GetPosition(ContentScroll);
            if (point.Y < 42) ContentScroll.ScrollToVerticalOffset(ContentScroll.VerticalOffset - 26);
            else if (point.Y > ContentScroll.ActualHeight - 42)
            {
                ExtendDragSpace();
                ContentScroll.ScrollToVerticalOffset(ContentScroll.VerticalOffset + 26);
            }
            if (point.X < 42) ContentScroll.ScrollToHorizontalOffset(ContentScroll.HorizontalOffset - 26);
            else if (point.X > ContentScroll.ActualWidth - 42)
            {
                GroupsPanel.DragMinimumWidth = Math.Min(10000 * Cell, ContentScroll.HorizontalOffset + ContentScroll.ViewportWidth + Cell * 4);
                GroupsPanel.InvalidateMeasure();
                ContentScroll.ScrollToHorizontalOffset(ContentScroll.HorizontalOffset + 26);
            }
        };
        UserButton.Content = "◉     " + (_preview ? "ReStart" : Environment.UserName);
        PreviewMouseDown += (_, _) => CompleteEntrance();
        IsVisibleChanged += (_, _) => { if (!IsVisible) { StopMotion(); SetLayoutEditing(false); } };
        Loaded += async (_, _) => await InitializeAsync();
        SourceInitialized += (_, _) =>
        {
            var square = 1;
            Native.DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 33, ref square, sizeof(int));
        };
        SizeChanged += (_, _) => FitPanels();
        ContentScroll.SizeChanged += (_, _) => FitPanels();
        _clock = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += async (_, _) => { UpdateLiveTiles(); await UpdateScriptTilesAsync(); };
        _clock.Start();
    }

    /// <summary>Initializes layout and catalog once; background startup may call this before showing a window.</summary>
    public Task InitializeAsync() => _initialization ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        try
        {
            Document = _store.Load() ?? new();
            ApplyAppearance();
            // Preferences and the bare-Windows-key hook must not wait for shell
            // enumeration. Input is accepted while the catalog is still loading.
            PreferencesChanged?.Invoke();
            _apps = await _loadCatalog();
            var actualApps = _apps;
            if (_preview && !_catalogFixture)
            {
                var previews = PreviewApps();
                _apps = previews;
                Document.Preferences.ShowWallpaper = false;
            }
            if (Document.Groups.Count == 0 && !File.Exists(_store.FilePath)) Document = SeedLayout.Create(_apps);
            if (_preview) Document.Preferences.ShowWallpaper = false;
            ApplyAppearance();
            RenderGroups();
            RenderApps();
            _layoutHistory.Reset(Document);
            _historyReady = true;
            SetStatus(_store.RecoveryMessage ?? L.F(K.StatusCatalogReady, _apps.Count));
            _ready = true;
            UpdateEditingChrome();
            RefreshSearchResults();
            PreferencesChanged?.Invoke();
            if (IsActive && (Keyboard.FocusedElement == null || Keyboard.FocusedElement == this || SearchBox.IsKeyboardFocused)) FocusSearchInput();
            var pinnedIds = Document.Groups.SelectMany(g => g.Tiles).SelectMany(t => new[] { t }.Concat(t.Children)).Select(t => t.AppId).ToHashSet();
            var pinnedFiles = Document.Groups.SelectMany(g => g.Tiles).SelectMany(t => new[] { t }.Concat(t.Children))
                .Where(t => !t.IsFolder && t.Widget.Length == 0 && !t.IsShellApp)
                .Select(t => new AppEntry(t.AppId, t.Label, t.Target, false));
            await _catalog.LoadIconsAsync(actualApps.Concat(pinnedFiles).DistinctBy(a => a.Id, StringComparer.OrdinalIgnoreCase).OrderByDescending(a => pinnedIds.Contains(a.Id)), () => Dispatcher.BeginInvoke(() =>
            {
                if (!_dragging) RenderGroups();
            }, DispatcherPriority.Background));
            if (_preview)
            {
                foreach (var app in _apps)
                {
                    var actual = actualApps.FirstOrDefault(a => a.Name.Contains(app.Name, StringComparison.CurrentCultureIgnoreCase));
                    if (actual != null && _catalog.Icons.TryGetValue(actual.Id, out var icon)) _catalog.Icons[app.Id] = icon;
                }
            }
            IconsReady = true;
            RenderGroups();
            RenderApps();
            RefreshSearchResults();
            if (_store.RecoveryMessage == null) SetStatus("");
            if (!_preview && !_store.IsReadOnly) SaveLayout();
            if (!_preview && Document.Preferences.CheckUpdatesOnStartup) _ = CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            SetStatus(L.T(K.ErrorCatalogLoad) + ex.Message);
            _ready = true;
            RefreshSearchResults();
        }
    }

    private static List<AppEntry> PreviewApps() =>
    [
        new("edge", "Microsoft Edge", "https://www.microsoft.com/edge", false),
        new("mail", L.T(K.AppMail), "mailto:", false), new("photos", L.T(K.AppPhotos), "ms-photos:", false),
        new("store", "Microsoft Store", "ms-windows-store:", false), new("notepad", L.T(K.AppNotepad), "notepad.exe", false),
        new("calc", L.T(K.AppCalculator), "calc.exe", false), new("paint", L.T(K.AppPaint), "mspaint.exe", false),
        new("terminal", "Windows Terminal", "wt.exe", false), new("code", "Visual Studio Code", "https://code.visualstudio.com", false)
    ];

    internal void ToggleFromStartButton(Native.Point point)
    {
        _startButtonPoint = new(point.X, point.Y);
        try { Toggle(); }
        finally { _startButtonPoint = null; }
    }

    /// <summary>Hides the active launcher or presents the existing window.</summary>
    public void Toggle()
    {
        if (IsVisible && IsActive && _dialogDepth == 0 && !_dismissing) Dismiss();
        else Present();
    }

    /// <summary>Returns to the pinned home view and presents the launcher; existing owned dialogs retain focus and preview windows retain their size.</summary>
    public void Present()
    {
        var dialog = OwnedWindows.Cast<Window>().LastOrDefault(w => w.IsVisible);
        if (dialog != null) { dialog.Activate(); return; }
        var wasHidden = !IsVisible;
        var wasDismissing = _dismissing;
        var previousOpacity = Opacity;
        var previousScale = ContentScale.ScaleX;
        StopMotion();
        if (_expandedAppFolders.Count > 0) { _expandedAppFolders.Clear(); RenderApps(); }
        ResetToPinnedView();
        var handle = new WindowInteropHelper(this).EnsureHandle();
        _focusOnActivation = true;
        if (wasHidden && MotionEnabled) Opacity = EntranceOpacity;
        if (!IsVisible) Show();
        if (!_preview)
        {
            var screen = System.Windows.Forms.Screen.FromPoint(_startButtonPoint ?? System.Windows.Forms.Cursor.Position);
            var area = screen.WorkingArea;
            _wallpaperMonitor = screen.Bounds;
            RefreshWallpaper();
            Native.SetWindowPos(handle, new nint(-1), area.X, area.Y, area.Width, area.Height, 0x0040);
        }
        if (!_preview || _verifyNativeActivation) ForegroundActivation.Request(handle);
        Activate();
        ExpandedRail.Visibility = Visibility.Collapsed;
        if (wasHidden || wasDismissing)
            BeginMotion(false, wasHidden ? EntranceOpacity : previousOpacity, wasHidden ? 0.9 : previousScale);
        FocusSearchInput();
        Dispatcher.BeginInvoke(() =>
        {
            if (IsActive && _dialogDepth == 0 && SearchBox.Text.Length == 0)
            {
                if (Keyboard.FocusedElement == null || Keyboard.FocusedElement == this || SearchBox.IsKeyboardFocused) FocusSearchInput();
                _focusOnActivation = false;
            }
        }, DispatcherPriority.Input);
    }

    private void RefreshBackgroundMenu()
    {
        var backgroundMenu = new ContextMenu();
        Add(backgroundMenu, L.T(K.NewGroup), AddGroup);
        Add(backgroundMenu, L.T(K.PinApp), () => ShowAllApps(this, new()));
        Add(backgroundMenu, L.T(K.AddTile), () => OpenAddMenu(AddButton, new()));
        AddLiveTileMenu(backgroundMenu);
        Add(backgroundMenu, L.T(K.SettingsTitle), ShowSettings);
        AddLayoutEditingMenu(backgroundMenu);
        Root.ContextMenu = backgroundMenu;
    }

    /// <summary>Applies language, theme, contrast, wallpaper, and shared control resources from local preferences.</summary>
    public void ApplyAppearance()
    {
        var p = Document.Preferences;
        RespectMotionPreference();
        LocalizationResources.Apply(p.Language);
        if (_scriptLanguage != L.Language)
        {
            _scriptLanguage = L.Language; PauseScripts(); _scriptFrames.Clear();
            foreach (var runtime in _scriptSessions.Values) runtime.NextUpdate = DateTimeOffset.MinValue;
        }
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.Culture.IetfLanguageTag);
        RefreshBackgroundMenu();
        if (_preview) Title = L.T(K.PreviewTitle);
        var light = p.Theme == ReStart.Core.ThemeMode.Light;
        if (p.Theme == ReStart.Core.ThemeMode.System)
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            light = key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
        }
        var highContrast = SystemParameters.HighContrast;
        var foreground = highContrast ? SystemColors.WindowTextColor : light ? Colors.Black : Colors.White;
        var background = highContrast ? SystemColors.WindowColor : light ? Color.FromRgb(238, 238, 238) : Color.FromRgb(31, 31, 31);
        Application.Current.Resources["TextBrush"] = new SolidColorBrush(foreground);
        Application.Current.Resources["MutedBrush"] = new SolidColorBrush(highContrast ? foreground : light ? Color.FromRgb(70, 70, 70) : Color.FromRgb(195, 195, 195));
        Application.Current.Resources["PanelBrush"] = new SolidColorBrush(background);
        Application.Current.Resources["TileBrush"] = new SolidColorBrush(highContrast ? background : light ? Color.FromRgb(218, 218, 218) : Color.FromRgb(58, 58, 58));
        Application.Current.Resources["AccentBrush"] = Brush(p.Accent, "#0078D7");
        Application.Current.Resources["ControlBrush"] = new SolidColorBrush(highContrast ? background : light ? Colors.White : Color.FromRgb(43, 43, 43));
        Application.Current.Resources["BorderBrush"] = new SolidColorBrush(highContrast ? foreground : light ? Color.FromRgb(135, 135, 135) : Color.FromRgb(112, 112, 112));
        Application.Current.Resources["HoverBrush"] = new SolidColorBrush(highContrast ? background : light ? Color.FromRgb(225, 225, 225) : Color.FromRgb(70, 70, 70));
        Application.Current.Resources["ScrollThumbBrush"] = new SolidColorBrush(highContrast ? foreground : light ? Color.FromRgb(135, 135, 135) : Color.FromRgb(115, 115, 115));
        var startBackground = background;
        if (p.ShowAccentOnStart && !light && !highContrast)
        {
            var accent = Brush(p.Accent, "#0078D7").Color;
            // Use a darkened accent surface with uniform translucent tile plates.
            // Exact Windows 10 acrylic composition remains a documented approximation.
            startBackground = Color.FromRgb((byte)(accent.R * 0.65), (byte)(accent.G * 0.65), (byte)(accent.B * 0.65));
        }
        Application.Current.Resources["StartTileBrush"] = new SolidColorBrush(highContrast ? background
            : light ? Color.FromArgb(20, 0, 0, 0) : Color.FromArgb(32, 255, 255, 255));
        ThemeWindow.IsLight = light;
        foreach (Window window in Application.Current.Windows) ThemeWindow.Apply(window);
        Tint.Background = new SolidColorBrush(Color.FromArgb((byte)(highContrast ? 255 : 255 * p.BackgroundOpacity), startBackground.R, startBackground.G, startBackground.B));
        Root.Background = new SolidColorBrush(startBackground);
        UpdateSearchChrome(SearchBox.Text.Trim().Length > 0 || _searchRequested);
        RefreshWallpaper();
    }

    /// <summary>Reapplies preferences and rebuilds visible groups and app views without changing saved labels.</summary>
    public void RefreshLayout()
    {
        ApplyAppearance();
        RenderGroups();
        RenderApps();
        SearchChanged(SearchBox, new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
    }

    private static SolidColorBrush Brush(string color, string fallback)
    {
        try { return new((Color)ColorConverter.ConvertFromString(color)); }
        catch (FormatException) { return new((Color)ColorConverter.ConvertFromString(fallback)); }
    }

    /// <summary>Persists the current document and reports recoverable write errors in the status area.</summary>
    public void SaveLayout()
    {
        TrySaveLayout();
    }

    private bool TrySaveLayout(bool recordHistory = true)
    {
        try
        {
            _store.Save(Document);
            _selectionPlanCache = null;
            if (_historyReady && recordHistory) _layoutHistory.Record(Document);
            UpdateEditingChrome();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            if (_historyReady && recordHistory) _layoutHistory.RestoreCurrent(Document);
            Dispatcher.BeginInvoke(RenderGroups);
            SetStatus(L.T(K.ErrorSave) + ex.Message);
            return false;
        }
    }

    private void RenderGroups()
    {
        if (_editingGroupHeader?.IsEditing == true) { _groupsRenderPending = true; return; }
        _groupsRenderPending = false;
        if (_openFolder != null && (!_openFolder.IsFolder || !Document.Groups.Any(g => g.Tiles.Contains(_openFolder)))) _openFolder = null;
        GroupsPanel.ExpandedFolderId = _openFolder?.Id;
        GroupsPanel.Children.Clear();
        _selectionViews.Clear();
        _liveLabels.Clear();
        _scriptViews.Clear();
        var activeScriptIds = Document.Groups.SelectMany(g => g.Tiles).SelectMany(t => new[] { t }.Concat(t.Children)).Where(t => t.Widget == "script").Select(t => t.Id).ToHashSet();
        foreach (var id in _scriptSessions.Keys.Where(id => !activeScriptIds.Contains(id)).ToArray()) _scriptSessions.Remove(id);
        foreach (var id in _scriptFrames.Keys.Where(id => !activeScriptIds.Contains(id)).ToArray()) _scriptFrames.Remove(id);
        foreach (var id in _slideshowSources.Keys.Where(id => !activeScriptIds.Contains(id)).ToArray()) { _slideshowSources.Remove(id); _slideshowImages.Remove(id); }
        foreach (var group in Document.Groups)
        {
            var placements = TileLayout.Pack(group.Tiles, Document.Preferences.Columns);
            var expansion = FolderExpansion.Create(placements, Document.Preferences.Columns, _openFolder?.Id);
            var compact = group.IsStandalone && placements.Count == 1 && expansion == null;
            var columns = compact ? placements[0].Width : expansion != null ? Document.Preferences.Columns : Math.Max(1, placements.Count == 0 ? 0 : placements.Max(p => p.Column + p.Width));
            var stack = new StackPanel { Background = Brushes.Transparent, Width = columns * Cell, Tag = group };
            if (!group.IsStandalone)
            {
                var header = new GroupHeader(group.Name) { Tag = group };
                header.Handle.Tag = group;
                AutomationProperties.SetName(header.Handle, L.T(K.GroupPrefix) + group.Name);
                WireInlineName(header, name =>
                {
                    group.Name = name;
                    AutomationProperties.SetName(header.Handle, L.T(K.GroupPrefix) + name);
                }, () => GroupsPanel.Children.OfType<StackPanel>().Where(s => s.Tag == group).SelectMany(s => s.Children.OfType<GroupHeader>()).FirstOrDefault());
                header.Handle.ContextMenu = GroupMenu(group);
                WireDrag(header.Handle, "ReStart.Group", group.Id);
                stack.Children.Add(header);
                stack.ContextMenu = GroupMenu(group);
            }
            var panel = new TilePanel { Tag = group, Columns = columns, Cell = Cell, Placements = compact ? [placements[0] with { Column = 0, Row = 0 }] : placements, Expansion = expansion };
            foreach (var placement in panel.Placements) panel.Children.Add(CreateTileButton(placement.Tile));
            if (panel.Expansion != null) panel.Children.Add(CreateInlineFolder(panel));
            stack.Children.Add(panel);
            GroupsPanel.Children.Add(stack);
        }
        if (Document.Groups.Count == 0)
        {
            var button = new Button { Content = L.T(K.EmptyGroups), Margin = new(0, 80, 0, 0), FontSize = 18 };
            button.Click += (_, _) => AddGroup();
            GroupsPanel.Children.Add(button);
        }
        UpdateLiveTiles();
        FitPanels();
        RefreshEntranceTargets();
        RefreshTileSelection();
        ShowGroupBoundaries(_layoutEditing || _dragging);
    }

    private double Cell => 52 * Document.Preferences.TileScale;

    private void FitPanels()
    {
        if (ContentScroll.ActualWidth < 1) return;
        GroupsPanel.TileColumns = Document.Preferences.Columns;
        GroupsPanel.Cell = Cell;
        GroupsPanel.InvalidateMeasure();
    }

    private Button CreateTileButton(Tile tile)
    {
        var displayName = TileDisplayName(tile);
        var button = new Button { Tag = tile, Padding = new(0), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, ToolTip = displayName };
        AutomationProperties.SetName(button, displayName + (tile.IsFolder && tile.Label.Length > 0 ? L.T(K.TileFolderSuffix) : ""));
        var colorful = tile.UseCustomColor || Document.Preferences.ColoredTiles || tile.Widget.Length > 0;
        button.Background = colorful && !SystemParameters.HighContrast ? Brush((tile.UseCustomColor || tile.Widget.Length > 0) && tile.Color.Length > 0 ? tile.Color : Document.Preferences.Accent, "#0078D7") : (System.Windows.Media.Brush)FindResource("StartTileBrush");
        if (colorful && !SystemParameters.HighContrast && button.Background is SolidColorBrush plate)
        {
            static double Linear(byte value) => value / 255d <= 0.04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + 0.055) / 1.055, 2.4);
            var luminance = 0.2126 * Linear(plate.Color.R) + 0.7152 * Linear(plate.Color.G) + 0.0722 * Linear(plate.Color.B);
            button.Foreground = luminance > 0.179 ? Brushes.Black : Brushes.White;
        }
        var grid = new Grid { Margin = new(tile.Widget == "script" ? tile.Size == TileSize.Small ? 3 * Document.Preferences.TileScale : 5 : 7) };
        if (tile.IsFolder)
        {
            if (_openFolder == tile)
            {
                button.SetResourceReference(BorderBrushProperty, SystemParameters.HighContrast ? "TextBrush" : "AccentBrush");
                button.BorderThickness = new(2);
                grid.Children.Add(new TextBlock { Text = "\uE70E", FontFamily = new("Segoe MDL2 Assets"), FontSize = 22, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
            }
            else
            {
                var mini = new UniformGridProxy { Columns = 2, Rows = 2, Width = tile.Size == TileSize.Small ? 28 : 52, Height = tile.Size == TileSize.Small ? 28 : 52, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                foreach (var child in tile.Children.Take(4)) mini.Children.Add(CreateIcon(child, tile.Size == TileSize.Small ? 12 : 21));
                grid.Children.Add(mini);
            }
        }
        else if (tile.Widget == "script" && tile.Live)
        {
            grid.Children.Add(new SlideshowTileView { IsHitTestVisible = false, Margin = new(-5) });
            var plot = new Grid { Margin = tile.Size == TileSize.Small ? new(0) : new(5, 2, 5, 20), ClipToBounds = true };
            plot.Children.Add(new ScriptTileArt { IsHitTestVisible = false });
            var body = new StackPanel { VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
            plot.Children.Add(body); grid.Children.Add(plot); _scriptViews.Add((tile, body, button));
            if (_scriptFrames.TryGetValue(tile.Id, out var cached)) ApplyScriptFrame(body, button, tile, cached);
        }
        else if (tile.Widget.Length > 0 && tile.Live && !(tile.Widget == "clock" && tile.Size == TileSize.Small))
        {
            var live = new TextBlock { FontWeight = FontWeights.Light, FontSize = tile.Size == TileSize.Small ? 17 : tile.Widget == "clock" ? 31 : 21, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(5, 0, 0, tile.Size == TileSize.Small ? 0 : 16) };
            _liveLabels.Add((live, tile));
            grid.Children.Add(live);
        }
        else grid.Children.Add(CreateIcon(tile, tile.Size == TileSize.Small ? 23 : tile.Size == TileSize.Large ? 60 : 40));
        if (tile.Size != TileSize.Small && !tile.IsFolder)
            grid.Children.Add(new TextBlock { Text = displayName, Tag = tile, FontSize = 12, VerticalAlignment = VerticalAlignment.Bottom, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(2, 0, 1, 0) });
        button.Content = grid;
        InstallTileSelection(button, grid, tile);
        button.ContextMenu = TileMenu(tile);
        button.Click += (_, _) =>
        {
            if (_dragging || _dismissing) return;
            if (_layoutEditing) { ToggleTileSelection(tile.Id); return; }
            if (tile.Widget == "script") InteractWithScriptTile(tile);
            else if (tile.IsFolder) OpenFolder(tile);
            else Launch(new(tile.AppId, tile.Label, tile.Target, tile.IsShellApp));
        };
        WireDrag(button, "ReStart.Tile", tile.Id);
        return button;
    }

    private FrameworkElement CreateIcon(Tile tile, double size)
    {
        if (_catalog.Icons.TryGetValue(tile.AppId, out var source))
            return new Image { Source = source, Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 0, 0, tile.Size == TileSize.Small ? 0 : 8) };
        var glyph = tile.Widget == "calendar" ? "\uE787" : tile.Widget == "clock" ? "\uE823" : tile.AppId == "restart:explorer" ? "\uE8B7" : tile.AppId == "restart:settings" ? "\uE713" : "\uE71D";
        return new TextBlock { Text = glyph, FontFamily = new("Segoe MDL2 Assets"), FontSize = size * 0.8, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 0, 0, tile.Size == TileSize.Small ? 0 : 8) };
    }

    private void UpdateLiveTiles()
    {
        var now = DateTime.Now;
        foreach (var (text, tile) in _liveLabels)
            text.Text = tile.Widget == "clock" ? now.ToString("HH:mm") : tile.Size == TileSize.Small ? now.Day.ToString(CultureInfo.CurrentCulture) : now.ToString("m", L.Culture) + "\n" + now.ToString("dddd", L.Culture);
    }

    private ContextMenu TileMenu(Tile tile)
    {
        var menu = new ContextMenu();
        if (tile.IsFolder) Add(menu, L.T(K.OpenTileFolder), () => OpenFolder(tile));
        Add(menu, L.T(K.SelectTileForEditing), () => SelectTileForEditing(tile.Id));
        Add(menu, L.T(K.UnpinTile), () => { RemoveTile(tile.Id); CommitLayout(); });
        var resize = new MenuItem { Header = L.T(K.ResizeTile) };
        foreach (var (size, label) in new[] { (TileSize.Small, L.T(K.TileSmall)), (TileSize.Medium, L.T(K.TileMedium)), (TileSize.Wide, L.T(K.TileWide)), (TileSize.Large, L.T(K.TileLarge)) })
        {
            var item = new MenuItem { Header = label, IsCheckable = true, IsChecked = tile.Size == size };
            item.Click += (_, _) => { tile.Size = size; CommitLayout(); };
            resize.Items.Add(item);
        }
        menu.Items.Add(resize);
        if (!tile.IsFolder && tile.Widget.Length == 0 && tile.Target.Length > 0)
            AddAppShellItems(menu, _apps.FirstOrDefault(a => a.Id == tile.AppId) ?? new(tile.AppId, tile.Label, tile.Target, tile.IsShellApp));
        var extras = new MenuItem { Header = "ReStart" };
        extras.Items.Add(TileColorMenu(tile));
        Add(extras, L.T(K.RenameTile), () =>
        {
            if (tile.IsFolder) { RenameFolder(tile); return; }
            var value = Prompt(L.T(K.PromptTileName), TileDisplayName(tile));
            if (value != null && value != TileDisplayName(tile)) { tile.Label = value; tile.UseLocalizedName = false; CommitLayout(); }
        });
        if (tile.Widget.Length > 0) Add(menu, tile.Live ? L.T(K.DisableLiveTile) : L.T(K.EnableLiveTile), () => { tile.Live = !tile.Live; CommitLayout(); });
        if (tile.Widget == "script") AddScriptMenu(menu, tile);
        var move = new MenuItem { Header = L.T(K.MoveToGroup) };
        foreach (var group in Document.Groups.Where(g => !g.IsStandalone))
        {
            var item = new MenuItem { Header = group.Name };
            item.Click += (_, _) => { LayoutEditor.Move(Document, tile, group); CommitLayout(); };
            move.Items.Add(item);
        }
        extras.Items.Add(move);
        if (!tile.IsFolder)
        {
            var folders = new MenuItem { Header = L.T(K.CreateFolder) };
            foreach (var target in Document.Groups.SelectMany(g => g.Tiles).Where(t => t.Id != tile.Id && t.Widget.Length == 0))
            {
                var item = new MenuItem { Header = TileDisplayName(target) };
                item.Click += (_, _) => { MergeFolder(tile, target); CommitLayout(); };
                folders.Items.Add(item);
            }
            folders.IsEnabled = folders.Items.Count > 0;
            extras.Items.Add(folders);
        }
        else Add(extras, L.T(K.UnpackFolder), () =>
        {
            LayoutEditor.UnpackFolder(Document, tile);
            CommitLayout();
        });
        menu.Items.Add(extras);
        AddLayoutEditingMenu(menu);
        return menu;
    }

    private ContextMenu GroupMenu(TileGroup group)
    {
        var menu = new ContextMenu();
        AddLiveTileMenu(menu);
        Add(menu, L.T(K.RenameGroup), () => RenameGroup(group));
        Add(menu, L.T(K.DeleteGroup), () =>
        {
            if (Confirm(L.F(K.ConfirmDeleteGroup, group.Name)))
            { Document.Groups.Remove(group); CommitLayout(); }
        });
        AddLayoutEditingMenu(menu);
        return menu;
    }

    private static void Add(ItemsControl menu, string label, Action action)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    private void BeginDrag(Button button, MouseEventArgs e, string format, string id)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragging) return;
        var point = e.GetPosition(this);
        if (Math.Abs(point.X - _dragOrigin.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - _dragOrigin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _dragging = true;
        ShowGroupBoundaries(true);
        _folderHover.Reset();
        if (format == "ReStart.Group" && _openFolder != null) { CloseFolder(this, new()); UpdateLayout(); }
        var previousView = PrepareDragView(format);
        if (_layoutEditing && format == "ReStart.Tile")
        {
            if (!_selectedTileIds.Contains(id)) { _selectedTileIds.Clear(); _selectedTileIds.Add(id); RefreshTileSelection(); }
            if (_selectedTileIds.Count > 1)
            {
                UpdateLayout();
                _selectionDragPositions = CaptureTileSelection();
                format = "ReStart.Selection";
            }
        }
        PreserveDragExtent();
        button.ReleaseMouseCapture();
        e.Handled = true;
        try
        {
            // A list/search row can disappear when switching to tiles. Keep the
            // OLE drag source attached to the window for the whole operation.
            var result = DragDrop.DoDragDrop(this, new DataObject(format, id), DragDropEffects.Move);
            if (result == DragDropEffects.None) RestoreDragView(previousView);
        }
        finally
        {
            ShowGroupBoundaries(_layoutEditing);
            ClearDropPreview();
            _folderHover.Reset();
            StopFolderDwell();
            _selectionDragPositions = null;
            _selectionPlanCache = null;
            GroupsPanel.DragMinimumHeight = 0;
            GroupsPanel.DragMinimumWidth = 0;
            GroupsPanel.InvalidateMeasure();
            Dispatcher.BeginInvoke(() => _dragging = false, DispatcherPriority.Background);
        }
    }

    private void WireDrag(Button button, string format, string id)
    {
        button.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _dragOrigin = e.GetPosition(this);
            _dragOffset = format == "ReStart.App" ? new Point(Cell, Cell) : e.GetPosition(button);
        };
        // ButtonBase consumes bubbling mouse movement; listen in the tunneling phase, including handled events.
        button.AddHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler((_, e) => BeginDrag(button, e, format, id)), true);
    }

    private static bool HasDrag(DragEventArgs e) => e.Data.GetDataPresent("ReStart.Tile") || e.Data.GetDataPresent("ReStart.App") || e.Data.GetDataPresent("ReStart.Group") || e.Data.GetDataPresent("ReStart.Selection");
    private Tile? DragTile(DragEventArgs e) => e.Data.GetData("ReStart.Tile") is string id ? FindTile(id) :
        e.Data.GetData("ReStart.App") is string appId ? TileForAppDrag(appId) : null;
    private Tile? TileForAppDrag(string appId) => _apps.FirstOrDefault(a => a.Id == appId) is AppEntry app
        ? AppPinning.Find(Document, app) ?? Tile.FromApp(app) : null;
    private void ShowDropPreview(TilePanel panel, Tile tile, int col, int row)
    {
        if (_dropAdorner?.AdornedElement != panel)
        {
            ClearDropPreview();
            _dropAdorner = new(panel) { IsHitTestVisible = false };
            AdornerLayer.GetAdornerLayer(panel)?.Add(_dropAdorner);
        }
        var (width, height) = TileLayout.Dimensions(tile.Size);
        var y = row * Cell + (panel.Expansion != null && row >= panel.Expansion.Row ? panel.Expansion.ExtraHeight(Cell) : 0);
        _dropAdorner.CellBounds = new(col * Cell, y, width * Cell - 4, height * Cell - 4);
        _dropAdorner.SelectionBounds = null;
        _dropAdorner.FolderTarget = false;
        _dropAdorner.GroupBounds = panel.FolderOwner == null && panel.Tag is TileGroup { IsStandalone: false }
            ? new Rect(0, -GroupLayout.HeaderHeight, Math.Max(panel.ActualWidth, (col + width) * Cell), Math.Max(panel.ActualHeight, y + height * Cell) + GroupLayout.HeaderHeight) : null;
        _dropAdorner.Caption = panel.FolderOwner != null ? L.T(K.DropIntoFolder) + " · " + TileDisplayName(panel.FolderOwner) : panel.Tag is TileGroup group && !group.IsStandalone
            ? L.F(K.DropIntoGroup, group.Name.Length == 0 ? L.T(K.UnnamedGroup) : group.Name) : "";
        _dropAdorner.InvalidateVisual();
    }
    private void ClearDropPreview()
    {
        if (GroupsPanel.DragPlacements != null) { GroupsPanel.DragPlacements = null; GroupsPanel.InvalidateMeasure(); }
        if (_dropAdorner == null) return;
        AdornerLayer.GetAdornerLayer(_dropAdorner.AdornedElement)?.Remove(_dropAdorner);
        _dropAdorner = null;
    }

    private void DropInGroup(TileGroup group, TilePanel panel, DragEventArgs e)
    {
        e.Handled = true;
        ClearDropPreview();
        if (!e.Data.GetDataPresent("ReStart.Group"))
        {
            var point = e.GetPosition(panel);
            var tile = DragTile(e);
            if (tile == null) return;
            var model = panel.ToModelPoint(point);
            var (col, row) = TileLayout.Snap(model.X - _dragOffset.X, model.Y - _dragOffset.Y, Cell, tile.Size, Document.Preferences.Columns);
            var sourceId = (string?)e.Data.GetData("ReStart.Tile") ?? (string?)e.Data.GetData("ReStart.App") ?? tile.Id;
            var folderTarget = FolderHoverTarget(tile, panel, point, sourceId);
            if (group.IsStandalone && folderTarget == null) MoveTileToStandalone(tile, e.GetPosition(GroupsPanel));
            else MoveTileIntoGroup(tile, group, col, row, folderTarget);
        }
    }

    private static T? Ancestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source != null) { if (source is T target) return target; source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source); }
        return null;
    }

    private Tile? FindTile(string id) => LayoutEditor.Find(Document, id);
    private void RemoveTile(string id)
    {
        var owner = Document.Groups.FirstOrDefault(g => g.Tiles.Any(t => t.Id == id || t.Children.Any(c => c.Id == id)));
        LayoutEditor.Remove(Document, id);
        if (owner?.Tiles.Count == 0) Document.Groups.Remove(owner);
    }
    private void MergeFolder(Tile source, Tile target) => LayoutEditor.MergeFolder(Document, source, target);

    private void CommitLayout()
    {
        SaveLayout();
        if (_openFolder != null)
        {
            var current = FindTile(_openFolder.Id);
            _openFolder = current?.IsFolder == true ? current : null;
        }
        RenderGroups();
    }

    private Button AppButton(AppEntry app)
    {
        var button = new Button { Tag = app, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new(4, 3, 4, 3), Height = 44, ToolTip = app.Name };
        AutomationProperties.SetName(button, app.Name);
        var panel = new DockPanel();
        var iconBox = new Border { Width = 32, Height = 32, Margin = new(0, 0, 12, 0), Background = Document.Preferences.ColoredTiles ? (System.Windows.Media.Brush)FindResource("AccentBrush") : Brushes.Transparent };
        var icon = CreateIcon(Tile.FromApp(app), 25);
        icon.Margin = new(0);
        iconBox.Child = icon;
        DockPanel.SetDock(iconBox, Dock.Left);
        panel.Children.Add(iconBox);
        panel.Children.Add(new TextBlock { Text = app.Name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
        button.Content = panel;
        button.Click += (_, _) => { if (!_dragging) Launch(app); };
        button.ContextMenu = CreateAppMenu(app);
        WireDrag(button, "ReStart.App", app.Id);
        return button;
    }

    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchPlaceholder == null) return;
        var query = SearchBox.Text.Trim();
        SearchPlaceholder.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var searching = query.Length > 0;
        UpdateSearchChrome(searching || _searchRequested);
        ResultsPanel.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
        GroupsPanel.Visibility = !searching && !_allApps ? Visibility.Visible : Visibility.Collapsed;
        AppsPanel.Visibility = !searching && _allApps ? Visibility.Visible : Visibility.Collapsed;
        ViewTitle.Text = searching ? L.T(K.SearchTitle) : _allApps ? L.T(K.NavAllApps) : L.T(K.NavStart);
        AddButton.Visibility = searching || _allApps ? Visibility.Collapsed : Visibility.Visible;
        if (!_allApps && !searching) PinnedNav.SetResourceReference(BackgroundProperty, "TileBrush"); else PinnedNav.Background = Brushes.Transparent;
        if (_allApps && !searching) AppsNav.SetResourceReference(BackgroundProperty, "TileBrush"); else AppsNav.Background = Brushes.Transparent;
        RefreshSearchResults();
        if (!searching) return;
        ContentScroll.ScrollToTop();
        ContentScroll.ScrollToLeftEnd();
    }

    private void RefreshSearchResults()
    {
        var focusedId = ((Keyboard.FocusedElement as Button)?.Tag as AppEntry)?.Id;
        ResultsPanel.Children.Clear();
        var query = SearchBox.Text.Trim();
        if (query.Length == 0) return;
        var results = AppSearch.Find(_apps, query);
        var loading = !_ready && _apps.Count == 0;
        ResultsPanel.Children.Add(new TextBlock { Text = loading ? L.T(K.StatusLoading) : results.Count == 0 ? L.T(K.SearchNoMatches) : L.T(K.SearchBestMatch), Margin = new(6, 10, 0, 14), Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush") });
        foreach (var result in results) ResultsPanel.Children.Add(AppButton(result));
        if (results.Count == 0 && !loading)
            ResultsPanel.Children.Add(new TextBlock { Text = L.T(K.SearchHint), Margin = new(6), FontSize = 13 });
        if (focusedId != null && ResultsPanel.IsVisible)
            ResultsPanel.Children.OfType<Button>().FirstOrDefault(b => b.Tag is AppEntry app && app.Id == focusedId)?.Focus();
    }

    private void ResetToPinnedView()
    {
        _editingGroupHeader?.EndEdit(true);
        _allApps = false;
        _searchRequested = false;
        SearchBox.Clear();
        // Clear() raises no event for an already-empty input, so always refresh the view.
        SearchChanged(SearchBox, new(TextBox.TextChangedEvent, UndoAction.None));
        ExpandedRail.Visibility = Visibility.Collapsed;
        AlphabetOverlay.Visibility = Visibility.Collapsed;
        CloseFolder(this, new());
        ClearDropPreview();
        ContentScroll.ScrollToTop();
    }
    private void ShowPinned(object sender, RoutedEventArgs e) => ResetToPinnedView();
    private void ShowAllApps(object sender, RoutedEventArgs e) { SetLayoutEditing(false); _searchRequested = false; _allApps = true; SearchBox.Clear(); SearchChanged(SearchBox, new(TextBox.TextChangedEvent, UndoAction.None)); ExpandedRail.Visibility = Visibility.Collapsed; ContentScroll.ScrollToTop(); }
    private void ClearSearch(object sender, RoutedEventArgs e) { SearchBox.Clear(); SearchBox.Focus(); }
    private void ToggleRail(object sender, RoutedEventArgs e) => ExpandedRail.Visibility = ExpandedRail.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    private void RenameGroup(TileGroup group) => GroupsPanel.Children.OfType<StackPanel>().Where(s => s.Tag == group).SelectMany(s => s.Children.OfType<GroupHeader>()).FirstOrDefault()?.BeginEdit();
    private void AddGroup()
    {
        ShowPinned(this, new());
        var group = new TileGroup { Name = "" };
        Document.Groups.Add(group);
        CommitLayout();
        RenameGroup(group);
    }

    private void OpenAddMenu(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        Add(menu, L.T(K.NewGroup), AddGroup);
        Add(menu, L.T(K.PinApp), () => ShowAllApps(this, new()));
        Add(menu, L.T(K.PinFile), () => ChoosePinPath(false));
        Add(menu, L.T(K.PinFolder), () => ChoosePinPath(true));
        Add(menu, L.T(K.PinWebsite), () =>
        {
            var url = Prompt(L.T(K.PromptWebsiteUrl), "https://");
            if (url == null) return;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) { SetStatus(L.T(K.ErrorInvalidUrl)); return; }
            var label = Prompt(L.T(K.PromptTileName), uri.Host);
            if (label != null) PinCustom(new() { AppId = url, Label = label, Target = url, IsShellApp = false, Size = TileSize.Wide });
        });
        Add(menu, L.T(K.CreateScriptTile), CreateScriptTile);
        AddLiveTileMenu(menu);
        Add(menu, L.T(K.ImportScriptTile), ImportScriptTile);
        Add(menu, L.T(K.AddClock), () => PinCustom(new() { AppId = "restart:clock", Label = L.T(K.AppClock), Widget = "clock", Target = "ms-clock:", IsShellApp = false }));
        Add(menu, L.T(K.AddCalendar), () => PinCustom(new() { AppId = "restart:calendar", Label = L.T(K.AppCalendar), Widget = "calendar", Target = "", IsShellApp = false, Size = TileSize.Wide }));
        OpenMenu(menu, (Button)sender);
    }

    private void AddCustomTile()
    {
        var menu = new ContextMenu();
        Add(menu, L.T(K.ChooseFile), () =>
        {
            WithDialog(() => { var dialog = new OpenFileDialog { Title = L.T(K.PromptPinFile) }; if (dialog.ShowDialog(this) == true) PinCustom(new() { AppId = dialog.FileName, Target = dialog.FileName, Label = Path.GetFileNameWithoutExtension(dialog.FileName), IsShellApp = false }); });
        });
        Add(menu, L.T(K.ChooseFolder), () =>
        {
            WithDialog(() => { var dialog = new OpenFolderDialog { Title = L.T(K.PromptPinFolder) }; if (dialog.ShowDialog(this) == true) PinCustom(new() { AppId = dialog.FolderName, Target = dialog.FolderName, Label = Path.GetFileName(dialog.FolderName), IsShellApp = false }); });
        });
        OpenMenu(menu, AddButton);
    }

    private void PinCustom(Tile tile)
    {
        LayoutEditor.PinIndependent(Document, tile, GroupsPanel.Columns, Cell, ContentScroll.ViewportWidth, ContentScroll.VerticalOffset);
        CommitLayout();
    }

    private void OpenAlphabet()
    {
        AlphabetButtons.Children.Clear();
        var sections = new[] { "#" }.Concat(Enumerable.Range('A', 26).Select(c => ((char)c).ToString()))
            .Concat(_sections.Keys.Where(s => s != "#" && !(s.Length == 1 && char.IsAsciiLetter(s[0]))));
        foreach (var section in sections)
        {
            var button = new Button { Content = section, IsEnabled = _sections.ContainsKey(section), Width = 75, Height = 60, FontSize = 22, HorizontalContentAlignment = HorizontalAlignment.Center };
            button.Click += (_, _) =>
            {
                AlphabetOverlay.Visibility = Visibility.Collapsed;
                // Icon loading can rebuild the rows while this picker is open.
                if (_sections.TryGetValue(section, out var header)) { header.BringIntoView(); header.Focus(); }
            };
            AlphabetButtons.Children.Add(button);
        }
        AlphabetOverlay.Visibility = Visibility.Visible;
        AlphabetButtons.Children.OfType<Button>().FirstOrDefault(b => b.IsEnabled)?.Focus();
    }
    private void CloseAlphabet(object sender, RoutedEventArgs e) => AlphabetOverlay.Visibility = Visibility.Collapsed;

    private void Launch(AppEntry app)
    {
        if (_dismissing || _layoutEditing) return;
        if (app.Id == "restart:calendar")
        {
            WithDialog(() =>
            {
                new CalendarWindow(this).ShowDialog();
            });
            return;
        }
        try { ShellLauncher.Launch(app); Dismiss(); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or System.IO.FileNotFoundException) { SetStatus(L.T(K.ErrorLaunch) + ex.Message); }
    }
    private void OpenDocuments(object sender, RoutedEventArgs e) => LaunchPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
    private void OpenPictures(object sender, RoutedEventArgs e) => LaunchPath(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
    private void LaunchPath(string path) => Launch(new(path, path, path, false));
    private void OpenAccount(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        Add(menu, L.T(K.AccountSettings), () => LaunchPath("ms-settings:yourinfo"));
        Add(menu, L.T(K.AccountLock), () => { Dismiss(immediately: true); Native.LockWorkStation(); });
        Add(menu, L.T(K.AccountSignOut), () => RunPower("/l", L.T(K.AccountSignOut)));
        OpenMenu(menu, (Button)sender);
    }
    private void OpenPower(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        Add(menu, L.T(K.PowerSleep), () => { if (Confirm(L.T(K.ConfirmSleep))) { Dismiss(immediately: true); System.Windows.Forms.Application.SetSuspendState(System.Windows.Forms.PowerState.Suspend, false, false); } });
        Add(menu, L.T(K.PowerShutdown), () => RunPower("/s", L.T(K.PowerShutdown)));
        Add(menu, L.T(K.PowerRestart), () => RunPower("/r", L.T(K.PowerRestart)));
        OpenMenu(menu, (Button)sender);
    }
    private void RunPower(string operation, string label)
    {
        if (!Confirm(L.F(K.ConfirmPower, label))) return;
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe")) { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add(operation);
        if (operation != "/l") { info.ArgumentList.Add("/t"); info.ArgumentList.Add("0"); }
        try { Process.Start(info); Dismiss(immediately: true); }
        catch (Win32Exception ex) { SetStatus(ex.Message); }
    }

    private static void OpenMenu(ContextMenu menu, Button target) { menu.PlacementTarget = target; menu.IsOpen = true; }
    private void OpenSettings(object sender, RoutedEventArgs e) => ShowSettings();
    /// <summary>Shows the settings dialog with close-on-deactivate suspended, then reapplies saved preferences.</summary>
    public void ShowSettings()
    {
        WithDialog(() => { var settings = new SettingsWindow(this, _store) { Owner = _preview ? null : this }; settings.ShowDialog(); });
        if (_exit) return;
        RefreshLayout();
        PreferencesChanged?.Invoke();
    }
    /// <summary>Displays a transient status or recovery message.</summary>
    private System.Windows.Threading.DispatcherTimer? _statusTimer;

    /// <summary>Displays a transient operation message, restarting its five-second lifetime on replacement.</summary>
    public void SetStatus(string message)
    {
        _statusTimer ??= CreateStatusTimer();
        _statusTimer.Stop();
        StatusText.Text = message;
        if (message.Length > 0) _statusTimer.Start();
    }

    private System.Windows.Threading.DispatcherTimer CreateStatusTimer()
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) => { timer.Stop(); StatusText.Text = ""; };
        Closed += (_, _) => timer.Stop();
        return timer;
    }
    /// <summary>Prepares a query for rendering verification only; this does not simulate physical keyboard input.</summary>
    public void SetSmokeSearch(string query) { SearchBox.Text = query; FocusSearchInput(); }
    /// <summary>Gets rendered app-result names for smoke diagnostics.</summary>
    public string[] SearchResultNames => ResultsPanel.Children.OfType<Button>().Select(AutomationProperties.GetName).ToArray();
    /// <summary>Gets whether the native text input remains in the visible WPF tree.</summary>
    public bool SearchInputInVisibleTree => SearchBox.IsVisible;
    private void WithDialog(Action action) { _dialogDepth++; try { action(); } finally { _dialogDepth--; } }
    private bool Confirm(string text)
    {
        var result = false;
        WithDialog(() => result = MessageBox.Show(this, text, "ReStart", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes);
        return result;
    }
    private string? Prompt(string title, string value)
    {
        string? answer = null;
        WithDialog(() => answer = TextPrompt.Show(this, title, value));
        return answer;
    }
    /// <summary>Atomically saves an imported/reset layout before replacing the visible document and recording one undo step.</summary>
    public void ReplaceDocument(LayoutDocument document)
    {
        _store.Save(document);
        Document = document;
        _selectionPlanCache = null;
        if (_historyReady) _layoutHistory.Record(document);
        RefreshLayout();
    }

    private void FocusSearchInput()
    {
        // Keep the native TextBox in the visible tree while its idle row is clipped to zero.
        // Native input handles IME composition, the first character, selection and backspace.
        FocusManager.SetFocusedElement(this, SearchBox);
        Keyboard.Focus(SearchBox);
    }
    private void UpdateSearchChrome(bool visible)
    {
        TopBar.Height = visible ? 52 : 0; TopBar.Opacity = visible ? 1 : 0; TopBar.IsHitTestVisible = visible;
    }
    /// <summary>Closes the menu after a taskbar click unless a dialog or drag currently owns the interaction.</summary>
    public void DismissFromTaskbar()
    {
        if (IsVisible && _dialogDepth == 0 && !_dragging && Document.Preferences.CloseOnDeactivate) Dismiss();
    }
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_dismissing) { e.Handled = true; return; }
        if (_dragging) return;
        CompleteEntrance();
        if (_editingGroupHeader?.IsEditing == true && e.OriginalSource is TextBox) return;
        if (HandleLayoutEditingKey(e)) return;
        if (e.Key == Key.Tab && SearchBox.IsKeyboardFocused && SearchBox.Text.Length == 0 && !_searchRequested)
        { PinnedNav.Focus(); e.Handled = true; return; }

        if (e.Key == Key.Escape)
        {
            if (AlphabetOverlay.Visibility == Visibility.Visible) AlphabetOverlay.Visibility = Visibility.Collapsed;
            else if (_openFolder != null) CloseFolder(this, e);
            else if (ExpandedRail.Visibility == Visibility.Visible) ExpandedRail.Visibility = Visibility.Collapsed;
            else if (SearchBox.Text.Length > 0 || _searchRequested) { _searchRequested = false; SearchBox.Clear(); UpdateSearchChrome(false); FocusSearchInput(); }
            else Dismiss();
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { _searchRequested = true; UpdateSearchChrome(true); SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
        else if (e.Key == Key.Enter && SearchBox.IsKeyboardFocused && SearchBox.Text.Trim().Length > 0)
        {
            var app = AppSearch.Find(_apps, SearchBox.Text, 1).FirstOrDefault();
            if (app != null) Launch(app);
            e.Handled = true;
        }
        else if (e.Key == Key.Down && SearchBox.IsKeyboardFocused)
        {
            var first = SearchBox.Text.Length > 0 ? ResultsPanel.Children.OfType<Button>().FirstOrDefault()
                : _allApps ? AppsPanel.Children.OfType<Button>().FirstOrDefault(b => b.Tag is AppEntry || b is AppFolderButton)
                : GroupsPanel.Children.OfType<StackPanel>().SelectMany(p => p.Children.OfType<TilePanel>())
                    .SelectMany(p => p.Children.OfType<Button>()).FirstOrDefault();
            if (first != null) { first.Focus(); first.BringIntoView(); e.Handled = true; }
        }
    }
    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_dismissing) { e.Handled = true; return; }
        if (_dragging) return;
        CompleteEntrance();
        if (_layoutEditing && _editingGroupHeader?.IsEditing != true) { e.Handled = true; return; }
        if (e.OriginalSource is TextBox || Ancestor<TextBox>(e.OriginalSource as DependencyObject) != null || Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || e.Text.Length == 0 || e.Text.All(char.IsControl)) return;
        if (FocusAppInitial(e.Text)) { e.Handled = true; return; }
        UpdateSearchChrome(true); SearchBox.Focus(); SearchBox.AppendText(e.Text); SearchBox.CaretIndex = SearchBox.Text.Length; e.Handled = true;
    }
    private void OnDeactivated(object? sender, EventArgs e)
    {
        _focusOnActivation = false;
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsActive && _dialogDepth == 0 && !_dragging && Document.Preferences.CloseOnDeactivate && (!_preview || _verifyNativeActivation) && !IsAnyMenuOpen()) Dismiss();
        }, DispatcherPriority.Background);
    }
    private static bool IsAnyMenuOpen() => Mouse.Captured is ContextMenu || Mouse.Captured is System.Windows.Controls.Primitives.MenuBase;
    private void OnClosing(object? sender, CancelEventArgs e) { if (!_exit) { e.Cancel = true; Dismiss(); } }
    /// <summary>Stops timers and isolated script workers before permanently closing the launcher.</summary>
    public void Exit() { _exit = true; StopMotion(); StopFolderDwell(); StopWatchingDesktopAppearance(); _clock.Stop(); _nameRefreshTimer?.Stop(); _updateCancellation.Cancel(); _releaseHttp.Dispose(); PauseScripts(); _systemMetrics?.Dispose(); Close(); }

    /// <summary>Renders only the launcher root to a PNG for reproducible UI verification.</summary>
    public void SaveScreenshot(string path)
    {
        SaveBitmap(path, RenderScreenshot());
    }

    private BitmapSource RenderScreenshot()
    {
        UpdateLayout();
        var explicitPreviewSize = _preview && double.IsFinite(Root.Width) && double.IsFinite(Root.Height);
        if (explicitPreviewSize)
        {
            // A fixture can exceed the CI desktop. Arrange it at its own size so
            // WPF does not retain the smaller native window's layout clip.
            Root.Measure(new(Root.Width, Root.Height));
            Root.Arrange(new Rect(0, 0, Root.Width, Root.Height));
        }
        var bitmap = new RenderTargetBitmap((int)Root.ActualWidth, (int)Root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(Root);
        if (explicitPreviewSize)
        {
            var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth - 1, bitmap.PixelHeight - 1, 1, 1), pixel, 4, 0);
            if (pixel[3] != 255) throw new InvalidOperationException("The preview render was clipped before its bottom-right corner.");
        }
        return bitmap;
    }

    private static void SaveBitmap(string path, BitmapSource bitmap)
    {
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path); png.Save(stream);
    }
}

// Keeps the regular WPF UniformGrid type out of generated XAML namespace ambiguities.
internal sealed class UniformGridProxy : System.Windows.Controls.Primitives.UniformGrid;
