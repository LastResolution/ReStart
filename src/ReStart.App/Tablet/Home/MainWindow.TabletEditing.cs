using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ReStart.App.Controls;
using ReStart.App.Tablet;
using ReStart.Core;

namespace ReStart.App;

// Tablet edition: iPad-style rearranging of Home with a finger or the pen. A long press enters the
// "jiggle" edit mode (ReStart's layout editing): tiles wiggle, carry a remove and a resize badge,
// and a floating Undo / Done bar appears. In that mode a finger drag moves a tile or a group at
// once. The gesture runs on the raw WM_POINTER stream (see MainWindow.TabletTouch.cs) instead of
// OLE drag and drop, so the ScrollViewer's touch panning and WPF's touch-to-mouse promotion can
// neither steal nor delay it; previews, folders and commits reuse the upstream placement code.
// The mouse keeps the upstream behavior.
public partial class MainWindow
{
    private const int TabletLongPressMs = 500, TabletEditIdleSeconds = 30;
    private const double TabletSlop = 10; // DIP before a still press becomes a move

    private enum TouchTarget { None, Board, Tile, Group }

    private sealed class TouchTrack
    {
        internal uint Id;
        internal Point Down; // window DIP
        internal long At;
        internal TouchTarget Target;
        internal Tile? Tile;
        internal TileGroup? Group;
        internal Button? Source;
        internal Point Grab; // pointer offset inside Source at the press
        internal bool Picked, Dragging, Moved;
    }

    private TouchTrack? _touch;
    private Point? _pendingTouchPoint;
    private DispatcherTimer? _longPressTimer, _editIdleTimer;
    private Border? _tabletEditBar;
    private Button? _tabletUndo;
    private long _suppressEditTapUntil, _lastTouchAt;
    private bool _touchDragFrames;
    private long _touchFrameTicks;
    private readonly HashSet<TileSurfaceButton> _wiggling = [];

    /// <summary>Whether the board is in the touch ("jiggle") edit mode: layout editing on tablet Home.</summary>
    private bool TabletEditing => _layoutEditing && _tabletHome && TabletShell.IsTablet;

    private void InstallTabletEditing()
    {
        // A long press on the board is the edit gesture, never a right click.
        Stylus.SetIsPressAndHoldEnabled(GroupsPanel, false);
        AddHandler(ContextMenuOpeningEvent, new ContextMenuEventHandler((_, e) =>
        {
            if (TabletShell.IsTablet && Environment.TickCount64 - _lastTouchAt < 1500 && e.OriginalSource is DependencyObject source && Ancestor<GroupBoardPanel>(source) != null) e.Handled = true;
        }), true);
        // Mouse input clears the touch flag, so the mouse keeps selection clicks in edit mode.
        PreviewMouseDown += (_, e) => { if (!FromTouch(e) && Environment.TickCount64 - _lastTouchAt > 500) _lastTouchAt = 0; };
        TabletShell.TabletChanged += tablet => { if (!tablet) EndTabletEditing(force: true); };
        // WM_POINTER is exact but a contact can reach the window only through WPF's stylus stack
        // (seen with a second contact right after a release); whichever source reports a contact first owns it.
        PreviewStylusDown += (_, e) => StylusContact(e, 0);
        PreviewStylusMove += (_, e) => StylusContact(e, 1);
        PreviewStylusUp += (_, e) => StylusContact(e, 2);
    }

    private void StylusContact(StylusEventArgs e, int phase)
    {
        if (!TabletShell.IsTablet || PresentationSource.FromVisual(this) == null) return;
        var id = 0x80000000u | (uint)e.StylusDevice.Id;
        if (phase != 0 && _touch?.Id != id) return;
        var screen = PointToScreen(e.GetPosition(this));
        if (phase == 0) TabletPointerPressed(id, screen);
        else if (phase == 1) TabletPointerMoved(id, screen);
        else TabletPointerReleased(id, screen);
    }

    private static bool Within(DependencyObject? source, DependencyObject ancestor)
    {
        while (source != null) { if (source == ancestor) return true; source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source); }
        return false;
    }

    // ---- pointer stream (called from PointerHook) ----

    private void TabletPointerPressed(uint id, Point screen)
    {
        _lastTouchAt = Environment.TickCount64;
        if (_touch is { } stale)
        {
            var age = Environment.TickCount64 - stale.At;
            // The same contact from the other source, or a second finger during a move: ignored.
            if (stale.Id != id && age < 250 && PresentationSource.FromVisual(this) != null && (PointFromScreen(screen) - stale.Down).Length < 24) return;
            if (stale.Id != id && (stale.Dragging || stale.Picked) && age < 10000) return;
            // Otherwise the previous contact's release never arrived.
            TabletLog.Write("home", $"pointer {stale.Id} release missed; replaced by {id}");
            TabletPointerCancelled(stale.Id);
        }
        if (!_tabletHome || !IsVisible || _dialogDepth > 0 || _dragging || !GroupsPanel.IsVisible || PresentationSource.FromVisual(this) == null)
        { if (_layoutEditing) TabletLog.Write("home", $"edit mode: pointer {id} ignored (dragging={_dragging} dialog={_dialogDepth} board={GroupsPanel.IsVisible})"); return; }
        var point = PointFromScreen(screen);
        var hit = InputHitTest(point) as DependencyObject;
        if (hit == null || !Within(hit, ContentScroll)) { if (_layoutEditing) TabletLog.Write("home", $"edit mode: pointer {id} outside the board ({hit?.GetType().Name})"); return; }
        var track = new TouchTrack { Id = id, Down = point, At = Environment.TickCount64, Target = TouchTarget.Board };
        var button = Ancestor<Button>(hit);
        if (button?.Tag is TabletBadge || Ancestor<TextBox>(hit) != null) return;
        if (button?.Tag is Tile tile && _selectionViews.TryGetValue(tile.Id, out var view) && view.Button == button)
        { track.Target = TouchTarget.Tile; track.Tile = tile; track.Source = button; }
        else if (Ancestor<GroupHeader>(hit) is { Tag: TileGroup group } header)
        {
            if (header.IsEditing || button == header.EditButton) return;
            track.Target = TouchTarget.Group; track.Group = group; track.Source = header.Handle;
        }
        else if (button != null) return; // other controls (empty-board button, inline folder chrome)
        if (track.Source != null) track.Grab = TranslatePoint(point, track.Source);
        _touch = track;
        if (TabletEditing)
        {
            TabletLog.Write("home", $"edit mode: pointer {id} down on {track.Target} {(track.Tile != null ? TileDisplayName(track.Tile) : track.Group?.Name)}");
            RestartEditIdle();
            // A finger on a tile or header moves it; only the board background pans.
            if (track.Target is TouchTarget.Tile or TouchTarget.Group) ContentScroll.PanningMode = PanningMode.None;
            return;
        }
        if (!CanEditLayout) return;
        _longPressTimer ??= new DispatcherTimer(DispatcherPriority.Input, Dispatcher) { Interval = TimeSpan.FromMilliseconds(TabletLongPressMs) };
        _longPressTimer.Tick -= OnTabletLongPress; _longPressTimer.Tick += OnTabletLongPress;
        _longPressTimer.Stop(); _longPressTimer.Start();
    }

    private void TabletPointerMoved(uint id, Point screen)
    {
        if (_touch is not { } track || track.Id != id || PresentationSource.FromVisual(this) == null) return;
        _lastTouchAt = Environment.TickCount64;
        var point = PointFromScreen(screen);
        if (track.Dragging)
        {
            // The ghost follows every sample; the (heavier) drop preview runs once per input pass.
            MoveDragGhost(TranslatePoint(point, Root));
            var pending = _pendingTouchPoint != null;
            _pendingTouchPoint = point;
            if (!pending) Dispatcher.BeginInvoke(() => { if (_pendingTouchPoint is { } latest && _touch is { Dragging: true }) MoveTouchDrag(latest); _pendingTouchPoint = null; }, DispatcherPriority.Input);
            return;
        }
        if ((point - track.Down).Length < TabletSlop) return;
        track.Moved = true;
        if (!TabletEditing) { _longPressTimer?.Stop(); return; } // a scroll or a swipe, not a long press
        if (track.Target is TouchTarget.Tile or TouchTarget.Group) StartTouchDrag(track, point);
    }

    private void TabletPointerReleased(uint id, Point screen)
    {
        if (_touch is not { } track || track.Id != id) return;
        _touch = null; _pendingTouchPoint = null;
        _lastTouchAt = Environment.TickCount64;
        _longPressTimer?.Stop();
        var point = PresentationSource.FromVisual(this) != null ? PointFromScreen(screen) : track.Down;
        if (track.Dragging) { FinishTouchDrag(track, point, commit: true); _suppressEditTapUntil = Environment.TickCount64 + 400; }
        else if (track.Picked) _suppressEditTapUntil = Environment.TickCount64 + 400; // the long press itself is no tap
        else if (TabletEditing && track.Target == TouchTarget.Board && !track.Moved && Environment.TickCount64 - track.At < 600)
        {
            TabletLog.Write("home", "edit mode: tap on empty space");
            EndTabletEditing();
        }
        RestoreTabletPanning();
    }

    private void TabletPointerCancelled(uint id)
    {
        if (_touch is not { } track || track.Id != id) return;
        TabletLog.Write("home", $"pointer {id} cancelled{(track.Dragging ? " during a drag" : "")}");
        _touch = null; _longPressTimer?.Stop();
        if (track.Dragging) FinishTouchDrag(track, track.Down, commit: false);
        RestoreTabletPanning();
    }

    private void OnTabletLongPress(object? sender, EventArgs e)
    {
        _longPressTimer?.Stop();
        if (_touch is not { } track || track.Moved || track.Dragging || !IsVisible || _dragging) return;
        if (track.Target == TouchTarget.Tile && track.Tile != null && !CanEditTile(track.Tile.Id)) return;
        track.Picked = true;
        _suppressEditTapUntil = long.MaxValue; // until the finger lifts
        EnterTabletEditing();
        if (!TabletEditing) { _touch = null; return; }
        // The finger now owns the tile: stop any pan the ScrollViewer started for this contact.
        if (track.Target is TouchTarget.Tile or TouchTarget.Group) ContentScroll.PanningMode = PanningMode.None;
        if (track.Tile != null && _selectionViews.TryGetValue(track.Tile.Id, out var view)) { track.Source = view.Button; Pop(view.Button); }
        TabletLog.Write("home", "edit mode: long press on " + (track.Tile != null ? TileDisplayName(track.Tile) : track.Group?.Name ?? "board"));
    }

    // ---- edit mode ----

    /// <summary>Enters layout editing in place: no view reset, no edit bar pushing the board down.</summary>
    private void EnterTabletEditing()
    {
        if (_layoutEditing || !_ready || !CanEditLayout || !_tabletHome) return;
        _editingGroupHeader?.EndEdit(true);
        _layoutEditing = true;
        _selectionPlanCache = null;
        _selectedTileIds.Clear();
        ShowGroupBoundaries(true);
        RefreshTileSelection(); // → SyncTabletEditing
        RestartEditIdle();
    }

    /// <summary>Leaves the touch edit mode (Done, a tap on empty space, going Home, idle).</summary>
    /// <param name="force">Also ends layout editing that began outside tablet Home (the posture just changed).</param>
    internal void EndTabletEditing(bool force = false)
    {
        if (_touch is { Dragging: true } track) { _touch = null; FinishTouchDrag(track, track.Down, commit: false); }
        _touch = null; _longPressTimer?.Stop();
        RestoreTabletPanning();
        if (!TabletEditing && !(force && _layoutEditing)) return;
        SetLayoutEditing(false);
        SyncTabletEditing(); // posture changes leave TabletEditing false before the refresh
    }

    private void RestartEditIdle()
    {
        _editIdleTimer ??= new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromSeconds(TabletEditIdleSeconds) };
        _editIdleTimer.Tick -= OnEditIdle; _editIdleTimer.Tick += OnEditIdle;
        _editIdleTimer.Stop(); _editIdleTimer.Start();
    }

    private void OnEditIdle(object? sender, EventArgs e)
    {
        _editIdleTimer?.Stop();
        if (_touch != null || _dragging || IsAnyMenuOpen()) { RestartEditIdle(); return; }
        TabletLog.Write("home", "edit mode: idle, done");
        EndTabletEditing();
    }

    private void RestoreTabletPanning()
    {
        if (_touch == null) ContentScroll.PanningMode = TabletShell.IsTablet ? PanningMode.VerticalFirst : PanningMode.Both;
    }

    /// <summary>Applies or removes the edit-mode chrome on the rendered board. Idempotent; runs after every selection refresh and render.</summary>
    private void SyncTabletEditing()
    {
        var editing = TabletEditing;
        if (editing) EditBar.Visibility = Visibility.Collapsed; // the floating bar replaces it on Home
        else _editIdleTimer?.Stop();
        ShowTabletEditBar(editing);
        var motion = editing && MotionEnabled;
        var rendered = new HashSet<TileSurfaceButton>();
        foreach (var (id, view) in _selectionViews)
        {
            if (view.Button is not TileSurfaceButton button || button.Content is not Grid content) continue;
            rendered.Add(button);
            var badges = editing && CanEditTile(id) && FindTile(id) is { } tile ? EnsureBadges(content, tile) : null;
            foreach (var badge in content.Children.OfType<Button>().Where(b => b.Tag is TabletBadge))
                badge.Visibility = badges != null ? Visibility.Visible : Visibility.Collapsed;
            if (motion && _wiggling.Add(button)) Wiggle(button.Tilt);
        }
        // Forever clocks must not outlive the edit mode or a replaced (re-rendered) button.
        foreach (var button in _wiggling.Where(b => !motion || !rendered.Contains(b)).ToArray())
        { button.Tilt.BeginAnimation(RotateTransform.AngleProperty, null); button.Tilt.Angle = 0; _wiggling.Remove(button); }
        if (_tabletUndo != null) _tabletUndo.IsEnabled = _historyReady && _layoutHistory.CanUndo && CanEditLayout;
    }

    private static void Wiggle(RotateTransform tilt)
    {
        var period = 120 + Random.Shared.Next(40);
        var angle = Random.Shared.Next(2) == 0 ? 1.1 : -1.1;
        var animation = new DoubleAnimation(-angle, angle, TimeSpan.FromMilliseconds(period))
        {
            AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = TimeSpan.FromMilliseconds(-Random.Shared.Next(period * 2)),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Timeline.SetDesiredFrameRate(animation, 30);
        tilt.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private static void Pop(Button button)
    {
        if (button is not TileSurfaceButton { MotionAllowed: true } tile || tile.RenderTransform is not TransformGroup { Children: [ScaleTransform scale, ..] }) return;
        foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
            scale.BeginAnimation(property, new DoubleAnimation(1.06, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    // ---- badges ----

    /// <summary>Marks the corner buttons of a tile in edit mode.</summary>
    private sealed record TabletBadge(string Kind);

    private Grid EnsureBadges(Grid content, Tile tile)
    {
        if (content.Children.OfType<Button>().Any(b => b.Tag is TabletBadge)) return content;
        content.Children.Add(Badge("", "Remove", HorizontalAlignment.Left, VerticalAlignment.Top, () =>
        {
            if (!TabletEditing || !CanEditTile(tile.Id)) return;
            TabletLog.Write("home", "edit mode: removed " + TileDisplayName(tile));
            RemoveTile(tile.Id); CommitLayout();
        }));
        content.Children.Add(Badge("", "Resize", HorizontalAlignment.Right, VerticalAlignment.Bottom, () =>
        {
            if (!TabletEditing || !CanEditTile(tile.Id) || FindTile(tile.Id) is not { } current) return;
            current.Size = current.Size switch { TileSize.Small => TileSize.Medium, TileSize.Medium => TileSize.Wide, TileSize.Wide => TileSize.Large, _ => TileSize.Small };
            TabletLog.Write("home", $"edit mode: {TileDisplayName(current)} → {current.Size}");
            CommitLayout();
        }));
        return content;
    }

    private Button Badge(string glyph, string name, HorizontalAlignment horizontal, VerticalAlignment vertical, Action action)
    {
        var face = new Border
        {
            Width = 24, Height = 24, CornerRadius = new(12), BorderThickness = new(1.5),
            Child = new TextBlock { Text = glyph, FontFamily = new("Segoe MDL2 Assets"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        face.SetResourceReference(Border.BackgroundProperty, "PanelBrush");
        face.SetResourceReference(Border.BorderBrushProperty, "TextBrush");
        face.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        // A larger transparent hit area around the visible badge.
        var badge = new Button
        {
            Tag = new TabletBadge(name), Width = 34, Height = 34, Margin = new(-6), Padding = new(0), Focusable = false,
            HorizontalAlignment = horizontal, VerticalAlignment = vertical, Visibility = Visibility.Collapsed,
            Template = BadgeTemplate, Content = face, ToolTip = name
        };
        System.Windows.Automation.AutomationProperties.SetName(badge, name);
        badge.Click += (_, e) => { e.Handled = true; RestartEditIdle(); action(); };
        Panel.SetZIndex(badge, 10);
        return badge;
    }

    private static readonly ControlTemplate BadgeTemplate = CreateBadgeTemplate();
    private static ControlTemplate CreateBadgeTemplate()
    {
        var root = new FrameworkElementFactory(typeof(Border));
        root.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        root.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = root };
        template.Seal();
        return template;
    }

    /// <summary>A touch tap on a tile in edit mode: folders open, other tiles show their menu. False lets the upstream selection toggle run (mouse).</summary>
    private bool TabletEditTap(Tile tile, Button button)
    {
        if (!TabletEditing || _lastTouchAt == 0) return false;
        if (Environment.TickCount64 < _suppressEditTapUntil) return true;
        RestartEditIdle();
        if (tile.IsFolder) { if (_openFolder?.Id == tile.Id) CloseFolder(this, new()); else OpenFolder(tile); return true; }
        if (button.ContextMenu is { } menu) { menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; OpenMenu(menu, button); }
        return true;
    }

    // ---- floating Undo / Done bar ----

    private void ShowTabletEditBar(bool show)
    {
        if (!show) { if (_tabletEditBar != null) _tabletEditBar.Visibility = Visibility.Collapsed; return; }
        if (_tabletEditBar == null)
        {
            _tabletUndo = BarButton("", "Undo", false, () => { RestartEditIdle(); RestoreLayoutHistory(false); });
            var done = BarButton("", "Done", true, () => { TabletLog.Write("home", "edit mode: done"); EndTabletEditing(); });
            _tabletEditBar = new Border
            {
                CornerRadius = new(26), Padding = new(6), Margin = new(0, 14, 18, 0),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { _tabletUndo, done } },
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 2, Opacity = .35 }
            };
            _tabletEditBar.SetResourceReference(Border.BackgroundProperty, "PanelBrush");
            Panel.SetZIndex(_tabletEditBar, 1000);
            Root.Children.Add(_tabletEditBar);
        }
        _tabletEditBar.Visibility = Visibility.Visible;
    }

    private Button BarButton(string glyph, string label, bool primary, Action action)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock { Text = glyph, FontFamily = new("Segoe MDL2 Assets"), FontSize = 15, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 8, 0) });
        content.Children.Add(new TextBlock { Text = label, FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
        var button = new Button { Content = content, Height = 44, MinWidth = 96, Padding = new(18, 0, 18, 0), Margin = new(2, 0, 2, 0), HorizontalContentAlignment = HorizontalAlignment.Center };
        if (primary) button.SetResourceReference(BackgroundProperty, "TileBrush");
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action();
        return button;
    }

    // ---- the drag itself ----

    private void StartTouchDrag(TouchTrack track, Point point)
    {
        if (_dragging || !CanEditLayout || track.Source == null) return;
        var format = track.Target == TouchTarget.Group ? "ReStart.Group" : "ReStart.Tile";
        var id = track.Group?.Id ?? track.Tile?.Id;
        if (id == null || format == "ReStart.Tile" && !CanEditTile(id) || track.Group != null && !CanEditGroup(track.Group)) return;
        _dragging = true;
        track.Dragging = true;
        ContentScroll.PanningMode = PanningMode.None;
        _editIdleTimer?.Stop();
        _freePlacementGesture = false; _reorderGesture = false;
        ShowGroupBoundaries(true);
        _folderHover.Reset();
        if (format == "ReStart.Group" && _openFolder != null)
        {
            CloseFolder(this, new()); UpdateLayout();
            if (GroupsPanel.Children.OfType<StackPanel>().FirstOrDefault(s => s.Tag == track.Group)?.Children.OfType<GroupHeader>().FirstOrDefault() is { } header) track.Source = header.Handle;
        }
        // A render since the press (a commit) replaced the pressed button; follow the tile.
        if (track.Tile != null && _selectionViews.TryGetValue(track.Tile.Id, out var view)) track.Source = view.Button;
        _dragOrigin = track.Down;
        _dragOffset = track.Grab;
        PreserveDragExtent();
        _activeDragFormat = format; _activeDragId = id;
        var grab = format == "ReStart.Group" ? track.Source.TranslatePoint(_dragOffset, Root) : TranslatePoint(track.Down, Root);
        StartDragGhost(track.Source, format, id, grab);
        // The upstream frame loop follows the system cursor, which touch does not move reliably.
        if (_dragFrameSubscribed) { CompositionTarget.Rendering -= OnDragFrame; _dragFrameSubscribed = false; }
        HideDragShortcutHint();
        _touchFrameTicks = Stopwatch.GetTimestamp();
        if (!_touchDragFrames) { CompositionTarget.Rendering += OnTouchDragFrame; _touchDragFrames = true; }
        TabletLog.Write("home", $"touch drag started: {(track.Tile != null ? TileDisplayName(track.Tile) : "group " + track.Group?.Name)}");
        MoveTouchDrag(point);
    }

    private void MoveTouchDrag(Point point)
    {
        if (_activeDragId == null) return;
        MoveDragGhost(TranslatePoint(point, Root));
        _dragViewportPoint = TranslatePoint(point, ContentScroll);
        _dragScrolling = Document.Preferences.DragEdgeScrolling;
        var board = TranslatePoint(point, GroupsPanel);
        if (_activeDragFormat == "ReStart.Group")
        {
            if (Document.Groups.FirstOrDefault(g => g.Id == _activeDragId) is { } group) PreviewGroupOnBoard(group, board);
        }
        else if (FindTile(_activeDragId) is { } tile) PreviewSingleTileDrop(tile, board, tile.Id);
    }

    private void OnTouchDragFrame(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        var seconds = Math.Clamp(Stopwatch.GetElapsedTime(_touchFrameTicks, now).TotalSeconds, 0, .05);
        _touchFrameTicks = now;
        if (_dragScrolling && _dragging && IsVisible) ScrollDragAt(_dragViewportPoint, seconds);
    }

    private void FinishTouchDrag(TouchTrack track, Point point, bool commit)
    {
        var format = _activeDragFormat; var id = _activeDragId;
        try
        {
            var viewport = TranslatePoint(point, ContentScroll);
            // Like an OLE drop, a release outside the board (over the dock, the search field) cancels.
            var inside = new Rect(ContentScroll.RenderSize).Contains(viewport);
            if (commit && inside && id != null)
            {
                var board = TranslatePoint(point, GroupsPanel);
                if (format == "ReStart.Group" && Document.Groups.FirstOrDefault(g => g.Id == id) is { } group)
                { ClearDropPreview(); StopFolderDwell(); MoveGroupOnBoard(group, board); }
                else if (FindTile(id) is { } tile) CommitSingleTileDrop(tile, board, id);
                TabletLog.Write("home", "touch drag dropped");
            }
            else TabletLog.Write("home", "touch drag cancelled");
        }
        finally
        {
            track.Dragging = false;
            if (_touchDragFrames) { CompositionTarget.Rendering -= OnTouchDragFrame; _touchDragFrames = false; }
            StopDragGhost();
            _dragScrolling = false; _activeDragFormat = null; _activeDragId = null;
            ShowGroupBoundaries(_layoutEditing);
            ClearDropPreview();
            _folderHover.Reset();
            StopFolderDwell();
            _selectionPlanCache = null;
            GroupsPanel.DragMinimumHeight = 0;
            GroupsPanel.DragMinimumWidth = 0;
            GroupsPanel.InvalidateMeasure();
            Dispatcher.BeginInvoke(() => _dragging = false, DispatcherPriority.Background);
            if (TabletEditing) RestartEditIdle();
        }
    }
}
