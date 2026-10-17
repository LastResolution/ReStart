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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using ReStart.App.Controls;
using ReStart.App.Services;
using ReStart.Core;

namespace ReStart.App;

public partial class MainWindow : Window
{
    private readonly LayoutStore _store;
    private readonly bool _preview;
    private readonly AppCatalog _catalog = new();
    private readonly DispatcherTimer _clock;
    private readonly List<(TextBlock Text, Tile Tile)> _liveLabels = [];
    private readonly Dictionary<string, FrameworkElement> _sections = [];
    private List<AppEntry> _apps = [];
    private bool _allApps, _dragging, _ready;
    private Point _dragOrigin;
    private Point _dragOffset;
    private DropAdorner? _dropAdorner;
    private long _lastDragScroll;
    private int _dialogDepth;
    private Tile? _openFolder;
    private bool _exit;
    private bool _searchRequested;
    private Task? _initialization;
    public LayoutDocument Document { get; private set; } = new();
    public Action? PreferencesChanged { get; set; }
    public bool IsReady => _ready;
    public bool IsPreview => _preview;
    public bool IconsReady { get; private set; }
    public int CatalogCount => _catalog.Count;
    public int IconCount => _catalog.Icons.Count;
    public LayoutDocument CreateInitialLayout() => SeedLayout.Create(_apps);

    public MainWindow(LayoutStore store, bool preview)
    {
        _store = store;
        _preview = preview;
        _scriptPackages = new(Path.GetDirectoryName(store.FilePath)!);
        IsVisibleChanged += (_, _) => { if (!IsVisible) PauseScripts(); };
        InitializeComponent();
        ThemeWindow.Attach(this);
        PinnedNav.AllowDrop = true;
        PinnedNav.PreviewDragOver += (_, e) => { if (HasDrag(e)) { ShowPinned(this, new()); e.Effects = DragDropEffects.Move; e.Handled = true; } };
        ContentScroll.AllowDrop = true;
        ContentScroll.PreviewDragOver += (_, e) =>
        {
            if (!HasDrag(e)) return;
            var now = Environment.TickCount64;
            if (now - _lastDragScroll < 80) return;
            _lastDragScroll = now;
            var point = e.GetPosition(ContentScroll);
            if (point.Y < 42) ContentScroll.ScrollToVerticalOffset(ContentScroll.VerticalOffset - 26);
            else if (point.Y > ContentScroll.ActualHeight - 42) ContentScroll.ScrollToVerticalOffset(ContentScroll.VerticalOffset + 26);
        };
        UserButton.Content = "◉     " + (_preview ? "ReStart" : Environment.UserName);
        Loaded += async (_, _) => await InitializeAsync();
        SourceInitialized += (_, _) =>
        {
            var square = 1;
            Native.DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 33, ref square, sizeof(int));
        };
        SizeChanged += (_, _) => FitPanels();
        _clock = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += async (_, _) => { UpdateLiveTiles(); await UpdateScriptTilesAsync(); };
        _clock.Start();
    }

    public Task InitializeAsync() => _initialization ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        try
        {
            Document = _store.Load() ?? new();
            ApplyAppearance();
            _apps = await _catalog.LoadAsync();
            var actualApps = _apps;
            if (_preview)
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
            SetStatus(_store.RecoveryMessage ?? L.F("{0} 個のアプリ  ·  文字を入力して検索  ·  Esc で閉じる", _apps.Count));
            _ready = true;
            PreferencesChanged?.Invoke();
            if (IsActive) FocusSearchInput();
            var pinnedIds = Document.Groups.SelectMany(g => g.Tiles).SelectMany(t => new[] { t }.Concat(t.Children)).Select(t => t.AppId).ToHashSet();
            await _catalog.LoadIconsAsync(actualApps.OrderByDescending(a => pinnedIds.Contains(a.Id)), () => Dispatcher.BeginInvoke(() =>
            {
                if (!_dragging && FolderOverlay.Visibility != Visibility.Visible) RenderGroups();
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
            if (_store.RecoveryMessage == null) SetStatus("");
        }
        catch (Exception ex)
        {
            SetStatus(L.T("アプリの読み込みに失敗しました: ") + ex.Message);
            _ready = true;
        }
    }

    private static List<AppEntry> PreviewApps() =>
    [
        new("edge", "Microsoft Edge", "https://www.microsoft.com/edge", false),
        new("mail", "メール", "mailto:", false), new("photos", "フォト", "ms-photos:", false),
        new("store", "Microsoft Store", "ms-windows-store:", false), new("notepad", "メモ帳", "notepad.exe", false),
        new("calc", "電卓", "calc.exe", false), new("paint", "ペイント", "mspaint.exe", false),
        new("terminal", "Windows Terminal", "wt.exe", false), new("code", "Visual Studio Code", "https://code.visualstudio.com", false)
    ];

    public void Toggle()
    {
        if (IsVisible && IsActive && _dialogDepth == 0) Hide();
        else Present();
    }

    public void Present()
    {
        var dialog = OwnedWindows.Cast<Window>().LastOrDefault(w => w.IsVisible);
        if (dialog != null) { dialog.Activate(); return; }
        var handle = new WindowInteropHelper(this).EnsureHandle();
        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
        var area = screen.WorkingArea;
        if (!IsVisible) Show();
        Native.SetWindowPos(handle, new nint(-1), area.X, area.Y, area.Width, area.Height, 0x0040);
        Native.SetForegroundWindow(handle);
        Activate();
        ExpandedRail.Visibility = Visibility.Collapsed;
        if (Document.Preferences.Animate && !SystemParameters.HighContrast && SystemParameters.ClientAreaAnimation)
            Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130)));
        else Root.Opacity = 1;
        _searchRequested = false;
        SearchBox.Clear();
        UpdateSearchChrome(false);
        FocusSearchInput();
        Dispatcher.BeginInvoke(() => { if (IsActive && _dialogDepth == 0 && SearchBox.Text.Length == 0) FocusSearchInput(); }, DispatcherPriority.Input);
    }

    private void RefreshBackgroundMenu()
    {
        var backgroundMenu = new ContextMenu();
        Add(backgroundMenu, L.T("新しいグループ"), AddGroup);
        Add(backgroundMenu, L.T("アプリをピン留め"), () => ShowAllApps(this, new()));
        Add(backgroundMenu, L.T("タイルを追加"), () => OpenAddMenu(AddButton, new()));
        Add(backgroundMenu, L.T("ReStart の設定"), ShowSettings);
        Root.ContextMenu = backgroundMenu;
    }

    public void ApplyAppearance()
    {
        var p = Document.Preferences;
        LocalizationResources.Apply(p.Language);
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.Culture.IetfLanguageTag);
        RefreshBackgroundMenu();
        if (_preview) Title = L.T("ReStart — プレビュー");
        var light = p.Theme == ReStart.Core.ThemeMode.Light;
        if (p.Theme == ReStart.Core.ThemeMode.System)
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            light = key?.GetValue("AppsUseLightTheme") is int value && value != 0;
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
        ThemeWindow.IsLight = light;
        foreach (Window window in Application.Current.Windows) ThemeWindow.Apply(window);
        Tint.Background = new SolidColorBrush(Color.FromArgb((byte)(highContrast ? 255 : 255 * p.BackgroundOpacity), background.R, background.G, background.B));
        Root.Background = new SolidColorBrush(background);
        Wallpaper.Source = null;
        if (p.ShowWallpaper && !highContrast)
        {
            using var desktop = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            if (desktop?.GetValue("WallPaper") is string path && File.Exists(path))
            {
                try { Wallpaper.Source = new BitmapImage(new Uri(path)); }
                catch (Exception ex) when (ex is IOException or NotSupportedException) { }
            }
        }
    }

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

    public void SaveLayout()
    {
        try { _store.Save(Document); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            SetStatus(L.T("保存できませんでした: ") + ex.Message);
        }
    }

    private void RenderGroups()
    {
        GroupsPanel.Children.Clear();
        _liveLabels.Clear();
        _scriptViews.Clear();
        var activeScriptIds = Document.Groups.SelectMany(g => g.Tiles).SelectMany(t => new[] { t }.Concat(t.Children)).Where(t => t.Widget == "script" && t.Live).Select(t => t.Id).ToHashSet();
        foreach (var id in _scriptSessions.Keys.Where(id => !activeScriptIds.Contains(id)).ToArray()) { _scriptSessions[id].Dispose(); _scriptSessions.Remove(id); }
        foreach (var group in Document.Groups)
        {
            var stack = new StackPanel { Background = Brushes.Transparent, Width = Document.Preferences.Columns * Cell, Margin = new(0, 0, 24, 28), Tag = group, AllowDrop = true };
            var header = new Button { Content = group.Name, HorizontalContentAlignment = HorizontalAlignment.Left, FontSize = 15, Padding = new(0, 8, 0, 12), HorizontalAlignment = HorizontalAlignment.Stretch, Tag = group };
            AutomationProperties.SetName(header, L.T("グループ: ") + group.Name);
            header.Click += (_, _) => { if (!_dragging) RenameGroup(group); };
            header.ContextMenu = GroupMenu(group);
            WireDrag(header, "ReStart.Group", group.Id);
            var panel = new TilePanel { Columns = Document.Preferences.Columns, Cell = Cell, Placements = TileLayout.Pack(group.Tiles, Document.Preferences.Columns) };
            foreach (var placement in panel.Placements) panel.Children.Add(CreateTileButton(placement.Tile));
            stack.Children.Add(header);
            stack.Children.Add(panel);
            stack.PreviewDragOver += (_, e) =>
            {
                e.Effects = HasDrag(e) ? DragDropEffects.Move : DragDropEffects.None;
                if (HasDrag(e) && !e.Data.GetDataPresent("ReStart.Group"))
                {
                    var tile = DragTile(e);
                    if (tile != null)
                    {
                        var point = e.GetPosition(panel);
                        var (col, row) = TileLayout.Snap(point.X - _dragOffset.X, point.Y - _dragOffset.Y, Cell, tile.Size, Document.Preferences.Columns);
                        ShowDropPreview(panel, tile, col, row);
                    }
                }
                e.Handled = true;
            };
            stack.PreviewDrop += (_, e) => DropInGroup(group, panel, e);
            stack.ContextMenu = GroupMenu(group);
            GroupsPanel.Children.Add(stack);
        }
        if (Document.Groups.Count == 0)
        {
            var button = new Button { Content = L.T("＋ グループを作成して、アプリをピン留め"), Margin = new(0, 80, 0, 0), FontSize = 18 };
            button.Click += (_, _) => AddGroup();
            GroupsPanel.Children.Add(button);
        }
        UpdateLiveTiles();
        FitPanels();
    }

    private double Cell => 52 * Document.Preferences.TileScale;

    private void FitPanels()
    {
        if (ContentScroll.ActualWidth < 1) return;
        var groupWidth = Document.Preferences.Columns * Cell + 24;
        var count = Math.Max(1, Math.Min(Math.Min(4, Document.Groups.Count), (int)(ContentScroll.ActualWidth / groupWidth)));
        GroupsPanel.Width = Math.Min(ContentScroll.ActualWidth, count * groupWidth);
        AppsPanel.Columns = Math.Clamp((int)(ContentScroll.ActualWidth / 304), 1, 4);
        AppsPanel.Width = Math.Min(ContentScroll.ActualWidth, AppsPanel.Columns * 304);
        AppsPanel.InvalidateMeasure();
    }

    private Button CreateTileButton(Tile tile)
    {
        var button = new Button { Tag = tile, Padding = new(0), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, ToolTip = tile.Label };
        AutomationProperties.SetName(button, tile.Label + (tile.IsFolder ? L.T(" フォルダー") : ""));
        var colorful = Document.Preferences.ColoredTiles || tile.Widget.Length > 0;
        button.Background = colorful && !SystemParameters.HighContrast ? Brush(tile.Color.Length > 0 ? tile.Color : Document.Preferences.Accent, "#0078D7") : (System.Windows.Media.Brush)FindResource("TileBrush");
        if (colorful && !SystemParameters.HighContrast) button.Foreground = Brushes.White;
        var grid = new Grid { Margin = new(7) };
        if (tile.IsFolder)
        {
            var mini = new UniformGridProxy { Columns = 2, Rows = 2, Width = 52, Height = 52, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            foreach (var child in tile.Children.Take(4)) mini.Children.Add(CreateIcon(child, 21));
            grid.Children.Add(mini);
        }
        else if (tile.Widget == "script" && tile.Live)
        {
            var body = new StackPanel { Margin = new(5, 2, 5, tile.Size == TileSize.Small ? 0 : 20), VerticalAlignment = VerticalAlignment.Center };
            grid.Children.Add(body); _scriptViews.Add((tile, body, button));
            if (_scriptFrames.TryGetValue(tile.Id, out var cached)) ApplyScriptFrame(body, button, tile, cached);
        }
        else if (tile.Widget.Length > 0 && tile.Live && !(tile.Widget == "clock" && tile.Size == TileSize.Small))
        {
            var live = new TextBlock { FontWeight = FontWeights.Light, FontSize = tile.Size == TileSize.Small ? 17 : tile.Widget == "clock" ? 31 : 21, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(5, 0, 0, tile.Size == TileSize.Small ? 0 : 16) };
            _liveLabels.Add((live, tile));
            grid.Children.Add(live);
        }
        else grid.Children.Add(CreateIcon(tile, tile.Size == TileSize.Small ? 23 : tile.Size == TileSize.Large ? 60 : 40));
        if (tile.Size != TileSize.Small)
            grid.Children.Add(new TextBlock { Text = tile.Label, FontSize = 12, VerticalAlignment = VerticalAlignment.Bottom, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(2, 0, 1, 0) });
        button.Content = grid;
        button.ContextMenu = TileMenu(tile);
        button.Click += (_, _) =>
        {
            if (_dragging) return;
            if (tile.Widget == "script") EditScript(tile);
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
        Add(menu, L.T("スタートからピン留めを外す"), () => { RemoveTile(tile.Id); CommitLayout(); });
        var resize = new MenuItem { Header = L.T("サイズ変更") };
        foreach (var (size, label) in new[] { (TileSize.Small, L.T("小")), (TileSize.Medium, L.T("中")), (TileSize.Wide, L.T("横長")), (TileSize.Large, L.T("大")) })
        {
            var item = new MenuItem { Header = label, IsCheckable = true, IsChecked = tile.Size == size };
            item.Click += (_, _) => { tile.Size = size; CommitLayout(); };
            resize.Items.Add(item);
        }
        menu.Items.Add(resize);
        Add(menu, L.T("名前を変更"), () => { var value = Prompt(L.T("タイルの名前"), tile.Label); if (value != null) { tile.Label = value; CommitLayout(); } });
        if (tile.Widget.Length > 0) Add(menu, tile.Live ? L.T("ライブタイルをオフにする") : L.T("ライブタイルをオンにする"), () => { tile.Live = !tile.Live; CommitLayout(); });
        if (tile.Widget == "script") AddScriptMenu(menu, tile);
        var move = new MenuItem { Header = L.T("グループへ移動") };
        foreach (var group in Document.Groups)
        {
            var item = new MenuItem { Header = group.Name };
            item.Click += (_, _) => { LayoutEditor.Move(Document, tile, group); CommitLayout(); };
            move.Items.Add(item);
        }
        menu.Items.Add(move);
        if (!tile.IsFolder)
        {
            var folders = new MenuItem { Header = L.T("フォルダーにまとめる") };
            foreach (var target in Document.Groups.SelectMany(g => g.Tiles).Where(t => t.Id != tile.Id && t.Widget.Length == 0))
            {
                var item = new MenuItem { Header = target.Label };
                item.Click += (_, _) => { MergeFolder(tile, target); CommitLayout(); };
                folders.Items.Add(item);
            }
            folders.IsEnabled = folders.Items.Count > 0;
            menu.Items.Add(folders);
        }
        else Add(menu, L.T("フォルダーを展開して戻す"), () =>
        {
            var group = Document.Groups.First(g => g.Tiles.Contains(tile));
            var index = group.Tiles.IndexOf(tile);
            group.Tiles.Remove(tile);
            group.Tiles.InsertRange(index, tile.Children);
            CommitLayout();
        });
        return menu;
    }

    private ContextMenu GroupMenu(TileGroup group)
    {
        var menu = new ContextMenu();
        Add(menu, L.T("グループ名を変更"), () => RenameGroup(group));
        Add(menu, L.T("グループを前へ"), () => MoveGroup(group, -1));
        Add(menu, L.T("グループを後ろへ"), () => MoveGroup(group, 1));
        Add(menu, L.T("グループを削除"), () =>
        {
            if (Confirm(L.F("「{0}」と、そのピン留めを削除しますか？ アプリ自体は削除されません。", group.Name)))
            { Document.Groups.Remove(group); CommitLayout(); }
        });
        return menu;
    }

    private void MoveGroup(TileGroup group, int delta)
    {
        var index = Document.Groups.IndexOf(group);
        var destination = Math.Clamp(index + delta, 0, Document.Groups.Count - 1);
        Document.Groups.Remove(group);
        Document.Groups.Insert(destination, group);
        CommitLayout();
    }

    private static void Add(ContextMenu menu, string label, Action action)
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
        button.ReleaseMouseCapture();
        e.Handled = true;
        try { DragDrop.DoDragDrop(button, new DataObject(format, id), DragDropEffects.Move); }
        finally { ClearDropPreview(); Dispatcher.BeginInvoke(() => _dragging = false, DispatcherPriority.Background); }
    }

    private void WireDrag(Button button, string format, string id)
    {
        button.PreviewMouseLeftButtonDown += (_, e) => { _dragOrigin = e.GetPosition(this); _dragOffset = e.GetPosition(button); };
        // ButtonBase consumes bubbling mouse movement; listen in the tunneling phase, including handled events.
        button.AddHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler((_, e) => BeginDrag(button, e, format, id)), true);
    }

    private static bool HasDrag(DragEventArgs e) => e.Data.GetDataPresent("ReStart.Tile") || e.Data.GetDataPresent("ReStart.App") || e.Data.GetDataPresent("ReStart.Group");
    private Tile? DragTile(DragEventArgs e) => e.Data.GetData("ReStart.Tile") is string id ? FindTile(id) :
        e.Data.GetData("ReStart.App") is string appId && _apps.FirstOrDefault(a => a.Id == appId) is AppEntry app ? Tile.FromApp(app) : null;
    private void ShowDropPreview(TilePanel panel, Tile tile, int col, int row)
    {
        if (_dropAdorner?.AdornedElement != panel)
        {
            ClearDropPreview();
            _dropAdorner = new(panel) { IsHitTestVisible = false };
            AdornerLayer.GetAdornerLayer(panel)?.Add(_dropAdorner);
        }
        var (width, height) = TileLayout.Dimensions(tile.Size);
        _dropAdorner.CellBounds = new(col * Cell, row * Cell, width * Cell - 4, height * Cell - 4);
        _dropAdorner.InvalidateVisual();
    }
    private void ClearDropPreview()
    {
        if (_dropAdorner == null) return;
        AdornerLayer.GetAdornerLayer(_dropAdorner.AdornedElement)?.Remove(_dropAdorner);
        _dropAdorner = null;
    }

    private void DropInGroup(TileGroup group, TilePanel panel, DragEventArgs e)
    {
        e.Handled = true;
        ClearDropPreview();
        if (e.Data.GetData("ReStart.Group") is string groupId)
        {
            var source = Document.Groups.FirstOrDefault(g => g.Id == groupId);
            if (source == null || source == group) return;
            var index = Document.Groups.IndexOf(group);
            Document.Groups.Remove(source);
            Document.Groups.Insert(Math.Min(index, Document.Groups.Count), source);
        }
        else
        {
            var point = e.GetPosition(panel);
            var target = Ancestor<Button>(panel.InputHitTest(point) as DependencyObject)?.Tag as Tile;
            var tile = DragTile(e);
            if (tile == null) return;
            var (col, row) = TileLayout.Snap(point.X - _dragOffset.X, point.Y - _dragOffset.Y, Cell, tile.Size, Document.Preferences.Columns);
            if (e.Data.GetData("ReStart.Tile") is string id)
            {
                var source = FindTile(id);
                if (source == null) return;
                if (target != null && (Keyboard.Modifiers & ModifierKeys.Shift) != 0 && !source.IsFolder && target.Widget.Length == 0)
                    MergeFolder(source, target);
                else
                {
                    LayoutEditor.MoveToCell(Document, source, group, col, row);
                }
            }
            else if (e.Data.GetData("ReStart.App") is string appId)
            {
                var app = _apps.FirstOrDefault(a => a.Id == appId);
                if (app == null) return;
                LayoutEditor.MoveToCell(Document, tile, group, col, row);
            }
            else return;
        }
        CommitLayout();
    }

    private static T? Ancestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source != null) { if (source is T target) return target; source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source); }
        return null;
    }

    private Tile? FindTile(string id) => LayoutEditor.Find(Document, id);
    private void RemoveTile(string id) => LayoutEditor.Remove(Document, id);
    private void MergeFolder(Tile source, Tile target) => LayoutEditor.MergeFolder(Document, source, target);

    private void CommitLayout()
    {
        SaveLayout();
        RenderGroups();
        if (_openFolder != null)
        {
            var current = FindTile(_openFolder.Id);
            if (current?.IsFolder == true) OpenFolder(current);
            else { _openFolder = null; FolderOverlay.Visibility = Visibility.Collapsed; }
        }
    }

    private void RenderApps()
    {
        AppsPanel.Children.Clear();
        _sections.Clear();
        foreach (var section in _apps.GroupBy(a => a.Section).OrderBy(g => g.Key, StringComparer.CurrentCulture))
        {
            var header = new Button { Content = section.Key, Tag = "section", FontSize = 20, FontWeight = FontWeights.Light, Padding = new(8, 8, 0, 8) };
            header.Click += (_, _) => OpenAlphabet();
            AppsPanel.Children.Add(header);
            foreach (var app in section) AppsPanel.Children.Add(AppButton(app));
            _sections[section.Key] = header;
        }
        FitPanels();
    }

    private Button AppButton(AppEntry app)
    {
        var button = new Button { HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new(4, 3, 4, 3), Height = 44, ToolTip = app.Name };
        AutomationProperties.SetName(button, app.Name);
        var panel = new DockPanel();
        var iconBox = new Border { Width = 32, Height = 32, Margin = new(0, 0, 12, 0), Background = (System.Windows.Media.Brush)FindResource("TileBrush") };
        iconBox.Child = CreateIcon(Tile.FromApp(app), 25);
        DockPanel.SetDock(iconBox, Dock.Left);
        panel.Children.Add(iconBox);
        panel.Children.Add(new TextBlock { Text = app.Name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
        button.Content = panel;
        button.Click += (_, _) => { if (!_dragging) Launch(app); };
        var menu = new ContextMenu();
        button.ContextMenu = menu;
        button.ContextMenuOpening += (_, _) =>
        {
            // Resolve groups when opening, after any rename, removal, or layout import.
            menu.Items.Clear();
            foreach (var group in Document.Groups) Add(menu, L.F("「{0}」にピン留め", group.Name), () =>
            {
                if (!Document.Groups.Contains(group)) return;
                group.Tiles.Add(Tile.FromApp(app)); CommitLayout(); SetStatus(L.F("{0} をピン留めしました", app.Name));
            });
            Add(menu, L.T("新しいグループにピン留め"), () =>
            {
                var name = Prompt(L.T("グループの名前"), L.T("新しいグループ"));
                if (name != null) { Document.Groups.Add(new() { Name = name, Tiles = [Tile.FromApp(app)] }); CommitLayout(); }
            });
        };
        WireDrag(button, "ReStart.App", app.Id);
        return button;
    }

    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchPlaceholder == null) return;
        var query = SearchBox.Text.Trim();
        SearchPlaceholder.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResultsPanel.Children.Clear();
        var searching = query.Length > 0;
        UpdateSearchChrome(searching || _searchRequested);
        ResultsPanel.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
        GroupsPanel.Visibility = !searching && !_allApps ? Visibility.Visible : Visibility.Collapsed;
        AppsPanel.Visibility = !searching && _allApps ? Visibility.Visible : Visibility.Collapsed;
        ViewTitle.Text = searching ? L.T("検索") : _allApps ? L.T("すべてのアプリ") : L.T("スタート");
        AddButton.Visibility = searching || _allApps ? Visibility.Collapsed : Visibility.Visible;
        PinnedNav.Background = !_allApps && !searching ? (System.Windows.Media.Brush)FindResource("TileBrush") : Brushes.Transparent;
        AppsNav.Background = _allApps && !searching ? (System.Windows.Media.Brush)FindResource("TileBrush") : Brushes.Transparent;
        if (!searching) return;
        var results = AppSearch.Find(_apps, query);
        ResultsPanel.Children.Add(new TextBlock { Text = results.Count == 0 ? L.T("一致するアプリはありません") : L.T("最も一致するアプリ"), Margin = new(6, 10, 0, 14), Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush") });
        foreach (var result in results) ResultsPanel.Children.Add(AppButton(result));
        if (results.Count == 0)
            ResultsPanel.Children.Add(new TextBlock { Text = L.T("名前の一部を入力して検索してください。"), Margin = new(6), FontSize = 13 });
        ContentScroll.ScrollToTop();
    }

    private void ShowPinned(object sender, RoutedEventArgs e) { _searchRequested = false; _allApps = false; SearchBox.Clear(); SearchChanged(SearchBox, new(TextBox.TextChangedEvent, UndoAction.None)); ExpandedRail.Visibility = Visibility.Collapsed; ContentScroll.ScrollToTop(); }
    private void ShowAllApps(object sender, RoutedEventArgs e) { _searchRequested = false; _allApps = true; SearchBox.Clear(); SearchChanged(SearchBox, new(TextBox.TextChangedEvent, UndoAction.None)); ExpandedRail.Visibility = Visibility.Collapsed; ContentScroll.ScrollToTop(); }
    private void ClearSearch(object sender, RoutedEventArgs e) { SearchBox.Clear(); SearchBox.Focus(); }
    private void ToggleRail(object sender, RoutedEventArgs e) => ExpandedRail.Visibility = ExpandedRail.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    private void RenameGroup(TileGroup group) { var name = Prompt(L.T("グループの名前"), group.Name); if (name != null) { group.Name = name; CommitLayout(); } }
    private void AddGroup() { var name = Prompt(L.T("グループの名前"), L.T("新しいグループ")); if (name != null) { Document.Groups.Add(new() { Name = name }); CommitLayout(); RenderApps(); } }

    private void OpenAddMenu(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        Add(menu, L.T("新しいグループ"), AddGroup);
        Add(menu, L.T("アプリをピン留め"), () => ShowAllApps(this, new()));
        Add(menu, L.T("ファイル・フォルダーをピン留め"), AddCustomTile);
        Add(menu, L.T("Web サイトをピン留め"), () =>
        {
            var url = Prompt(L.T("Web サイトの URL"), "https://");
            if (url == null) return;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) { SetStatus(L.T("http または https の URL を指定してください。")); return; }
            var label = Prompt(L.T("タイルの名前"), uri.Host);
            if (label != null) PinCustom(new() { AppId = url, Label = label, Target = url, IsShellApp = false, Size = TileSize.Wide });
        });
        Add(menu, L.T("JavaScript タイルを作成"), CreateScriptTile);
        Add(menu, L.T("JavaScript タイルをインポート"), ImportScriptTile);
        Add(menu, L.T("時計タイル"), () => PinCustom(new() { AppId = "restart:clock", Label = L.T("クロック"), Widget = "clock", Target = "ms-clock:", IsShellApp = false }));
        Add(menu, L.T("カレンダータイル"), () => PinCustom(new() { AppId = "restart:calendar", Label = L.T("カレンダー"), Widget = "calendar", Target = "", IsShellApp = false, Size = TileSize.Wide }));
        OpenMenu(menu, (Button)sender);
    }

    private void AddCustomTile()
    {
        var menu = new ContextMenu();
        Add(menu, L.T("ファイルを選ぶ"), () =>
        {
            WithDialog(() => { var dialog = new OpenFileDialog { Title = L.T("ピン留めするファイル") }; if (dialog.ShowDialog(this) == true) PinCustom(new() { AppId = dialog.FileName, Target = dialog.FileName, Label = Path.GetFileNameWithoutExtension(dialog.FileName), IsShellApp = false }); });
        });
        Add(menu, L.T("フォルダーを選ぶ"), () =>
        {
            WithDialog(() => { var dialog = new OpenFolderDialog { Title = L.T("ピン留めするフォルダー") }; if (dialog.ShowDialog(this) == true) PinCustom(new() { AppId = dialog.FolderName, Target = dialog.FolderName, Label = Path.GetFileName(dialog.FolderName), IsShellApp = false }); });
        });
        OpenMenu(menu, AddButton);
    }

    private void PinCustom(Tile tile)
    {
        if (Document.Groups.Count == 0) Document.Groups.Add(new());
        Document.Groups[0].Tiles.Add(tile);
        CommitLayout();
    }

    private void OpenFolder(Tile tile)
    {
        RemoveFolderLiveViews();
        _openFolder = tile;
        FolderTitle.Text = tile.Label;
        FolderTiles.Children.Clear();
        foreach (var child in tile.Children)
        {
            var button = CreateTileButton(child);
            button.Width = 100; button.Height = 100; button.Margin = new(2);
            FolderTiles.Children.Add(button);
        }
        FolderOverlay.Visibility = Visibility.Visible;
        UpdateLiveTiles();
    }

    private void RemoveFolderLiveViews()
    {
        if (_openFolder == null) return;
        var ids = _openFolder.Children.Select(t => t.Id).ToHashSet();
        _scriptViews.RemoveAll(v => ids.Contains(v.Tile.Id));
        _liveLabels.RemoveAll(v => ids.Contains(v.Tile.Id));
    }
    private void CloseFolder(object sender, RoutedEventArgs e) { RemoveFolderLiveViews(); FolderOverlay.Visibility = Visibility.Collapsed; _openFolder = null; }
    private void CloseFolderBackground(object sender, MouseButtonEventArgs e) => CloseFolder(sender, e);
    private void KeepFolderOpen(object sender, MouseButtonEventArgs e) => e.Handled = true;
    private void OpenAlphabet()
    {
        AlphabetButtons.Children.Clear();
        foreach (var section in _sections)
        {
            var button = new Button { Content = section.Key, Width = 75, Height = 60, FontSize = 22, HorizontalContentAlignment = HorizontalAlignment.Center };
            button.Click += (_, _) => { AlphabetOverlay.Visibility = Visibility.Collapsed; section.Value.BringIntoView(); };
            AlphabetButtons.Children.Add(button);
        }
        AlphabetOverlay.Visibility = Visibility.Visible;
    }
    private void CloseAlphabet(object sender, RoutedEventArgs e) => AlphabetOverlay.Visibility = Visibility.Collapsed;

    private void Launch(AppEntry app)
    {
        if (app.Id == "restart:calendar")
        {
            WithDialog(() =>
            {
                new CalendarWindow(this).ShowDialog();
            });
            return;
        }
        try { ShellLauncher.Launch(app); Hide(); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or System.IO.FileNotFoundException) { SetStatus(L.T("起動できませんでした: ") + ex.Message); }
    }
    private void OpenDocuments(object sender, RoutedEventArgs e) => LaunchPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
    private void OpenPictures(object sender, RoutedEventArgs e) => LaunchPath(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
    private void LaunchPath(string path) => Launch(new(path, path, path, false));
    private void OpenAccount(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        Add(menu, L.T("アカウント設定の変更"), () => LaunchPath("ms-settings:yourinfo"));
        Add(menu, L.T("ロック"), () => { Hide(); Native.LockWorkStation(); });
        Add(menu, L.T("サインアウト"), () => RunPower("/l", L.T("サインアウト")));
        OpenMenu(menu, (Button)sender);
    }
    private void OpenPower(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        Add(menu, L.T("スリープ"), () => { if (Confirm(L.T("PC をスリープしますか？"))) { Hide(); System.Windows.Forms.Application.SetSuspendState(System.Windows.Forms.PowerState.Suspend, false, false); } });
        Add(menu, L.T("シャットダウン"), () => RunPower("/s", L.T("シャットダウン")));
        Add(menu, L.T("再起動"), () => RunPower("/r", L.T("再起動")));
        OpenMenu(menu, (Button)sender);
    }
    private void RunPower(string operation, string label)
    {
        if (!Confirm(L.F("PC を{0}しますか？ 作業中のファイルを保存してください。", label))) return;
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe")) { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add(operation);
        if (operation != "/l") { info.ArgumentList.Add("/t"); info.ArgumentList.Add("0"); }
        try { Process.Start(info); Hide(); }
        catch (Win32Exception ex) { SetStatus(ex.Message); }
    }

    private static void OpenMenu(ContextMenu menu, Button target) { menu.PlacementTarget = target; menu.IsOpen = true; }
    private void OpenSettings(object sender, RoutedEventArgs e) => ShowSettings();
    public void ShowSettings()
    {
        WithDialog(() => { var settings = new SettingsWindow(this, _store) { Owner = _preview ? null : this }; settings.ShowDialog(); });
        if (_exit) return;
        RefreshLayout();
        PreferencesChanged?.Invoke();
    }
    public void SetStatus(string message) => StatusText.Text = message;
    public void SetSmokeSearch(string query) { SearchBox.Text = query; FocusSearchInput(); }
    public string[] SearchResultNames => ResultsPanel.Children.OfType<Button>().Select(AutomationProperties.GetName).ToArray();
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
    public void ReplaceDocument(LayoutDocument document) { Document = document; SaveLayout(); RefreshLayout(); }

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
    public void DismissFromTaskbar()
    {
        if (IsVisible && _dialogDepth == 0 && !_dragging && Document.Preferences.CloseOnDeactivate) Hide();
    }
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && SearchBox.IsKeyboardFocused && SearchBox.Text.Length == 0 && !_searchRequested)
        { PinnedNav.Focus(); e.Handled = true; return; }

        if (e.Key == Key.Escape)
        {
            if (AlphabetOverlay.Visibility == Visibility.Visible) AlphabetOverlay.Visibility = Visibility.Collapsed;
            else if (FolderOverlay.Visibility == Visibility.Visible) CloseFolder(this, e);
            else if (ExpandedRail.Visibility == Visibility.Visible) ExpandedRail.Visibility = Visibility.Collapsed;
            else if (SearchBox.Text.Length > 0 || _searchRequested) { _searchRequested = false; SearchBox.Clear(); UpdateSearchChrome(false); FocusSearchInput(); }
            else Hide();
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { _searchRequested = true; UpdateSearchChrome(true); SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
        else if (e.Key == Key.Enter && SearchBox.IsKeyboardFocused && SearchBox.Text.Trim().Length > 0)
        {
            var app = AppSearch.Find(_apps, SearchBox.Text, 1).FirstOrDefault();
            if (app != null) Launch(app);
            e.Handled = true;
        }
        else if (e.Key == Key.Down && SearchBox.IsKeyboardFocused && ResultsPanel.Children.Count > 1)
        {
            ((Button)ResultsPanel.Children[1]).Focus(); e.Handled = true;
        }
    }
    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.OriginalSource is TextBox || Ancestor<TextBox>(e.OriginalSource as DependencyObject) != null || Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || e.Text.Length == 0 || e.Text.All(char.IsControl)) return;
        UpdateSearchChrome(true); SearchBox.Focus(); SearchBox.AppendText(e.Text); SearchBox.CaretIndex = SearchBox.Text.Length; e.Handled = true;
    }
    private void OnDeactivated(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_ready && !IsActive && _dialogDepth == 0 && !_dragging && Document.Preferences.CloseOnDeactivate && !_preview && !IsAnyMenuOpen()) Hide();
        }, DispatcherPriority.Background);
    }
    private static bool IsAnyMenuOpen() => Mouse.Captured is ContextMenu || Mouse.Captured is System.Windows.Controls.Primitives.MenuBase;
    private void OnClosing(object? sender, CancelEventArgs e) { if (!_exit) { e.Cancel = true; Hide(); } }
    public void Exit() { _exit = true; _clock.Stop(); PauseScripts(); Close(); }

    public void SaveScreenshot(string path)
    {
        UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Root.ActualWidth, (int)Root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(Root);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path); png.Save(stream);
    }
}

// Keeps the regular WPF UniformGrid type out of generated XAML namespace ambiguities.
internal sealed class UniformGridProxy : System.Windows.Controls.Primitives.UniformGrid;
