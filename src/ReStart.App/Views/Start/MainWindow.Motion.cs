using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ReStart.App.Controls;

namespace ReStart.App;

public partial class MainWindow
{
    // The official full-screen Start recording shows a bottom-anchored zoom and
    // bottom-to-top tile reveal. These are independently tuned WPF timings, not
    // constants extracted from the Windows shell. See docs/fidelity.md.
    private static readonly TimeSpan EntranceDuration = TimeSpan.FromMilliseconds(367);
    private static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(167);
    // Retain a nonzero native alpha while requesting foreground ownership.
    private const double EntranceOpacity = 1d / 255;
    private Storyboard? _motion;
    private bool _dismissing;
    private bool _verifyMotion;
    private double _entranceFromOpacity, _entranceFromScale;
    private readonly List<(FrameworkElement Element, double Opacity)> _motionTiles = [];

    private ScaleTransform ContentScale => (ScaleTransform)MainContent.RenderTransform;

    private bool MotionEnabled => DisplayPreferences.Animate &&
        (_preview && _verifyMotion || SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast);

    /// <summary>Dismisses Start with its exit transition, or immediately when motion is disabled or an OS session action requires it.</summary>
    /// <param name="immediately">Whether to bypass animation, for example before a session or power action.</param>
    public void Dismiss(bool immediately = false)
    {
        if (_startupWindow != null) { CancelStartupPresentation(); return; }
        if (!IsVisible || _dismissing && !immediately) return;
        if (immediately || !MotionEnabled)
        {
            StopMotion();
            Hide();
            return;
        }
        BeginMotion(closing: true);
    }

    private void BeginMotion(bool closing, double? initialOpacity = null, double? initialScale = null, TimeSpan? elapsed = null)
    {
        var opacity = initialOpacity ?? Opacity;
        var scale = initialScale ?? ContentScale.ScaleX;
        var tileOpacity = _motionTiles.ToDictionary(item => item.Element, item => item.Element.Opacity);
        StopMotion();
        if (!MotionEnabled)
        {
            if (closing) Hide();
            return;
        }
        UpdateLayout();
        _dismissing = closing;
        if (!closing) { _entranceFromOpacity = opacity; _entranceFromScale = scale; }
        Root.IsHitTestVisible = !closing;
        var storyboard = new Storyboard();
        _motion = storyboard;
        var duration = closing ? ExitDuration : EntranceDuration;
        var spline = closing ? new KeySpline(0.7, 0, 1, 0.5) : new KeySpline(0.1, 0.9, 0.2, 1);
        AddSpline(this, OpacityProperty, opacity, closing ? 0 : 1, closing ? ExitDuration : TimeSpan.FromMilliseconds(120), null);
        AddSpline(ContentScale, ScaleTransform.ScaleXProperty, scale, closing ? 0.94 : 1, duration, spline);
        AddSpline(ContentScale, ScaleTransform.ScaleYProperty, scale, closing ? 0.94 : 1, duration, spline);

        var targets = VisibleMotionTiles();
        var bottom = targets.Count == 0 ? 1 : targets.Max(target => target.Bounds.Bottom);
        var top = targets.Count == 0 ? 0 : targets.Min(target => target.Bounds.Top);
        foreach (var (element, bounds) in targets)
        {
            _motionTiles.Add((element, element.Opacity));
            var from = tileOpacity.GetValueOrDefault(element, closing ? element.Opacity : 0);
            element.Opacity = from;
            var delay = closing ? TimeSpan.Zero : TimeSpan.FromMilliseconds(80 * (bottom - bounds.Bottom) / Math.Max(1, bottom - top));
            var fade = new DoubleAnimation(from, closing ? 0 : 1, TimeSpan.FromMilliseconds(120)) { BeginTime = delay };
            Storyboard.SetTarget(fade, element);
            Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(fade);
        }
        storyboard.Completed += (_, _) =>
        {
            // A superseded close must never hide a newly opened launcher.
            if (!ReferenceEquals(_motion, storyboard)) return;
            StopMotion();
            if (closing) Hide();
        };
        storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace, isControllable: true);
        if (elapsed != null) storyboard.SeekAlignedToLastTick(this, elapsed.Value, TimeSeekOrigin.BeginTime);

        void AddSpline(DependencyObject target, DependencyProperty property, double from, double to, TimeSpan time, KeySpline? easing)
        {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(easing == null
                ? new LinearDoubleKeyFrame(to, KeyTime.FromTimeSpan(time))
                : new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(time), easing));
            // Target the owning visual: WPF may clone a XAML Freezable when
            // applying clocks, so a cached transform reference can be stale.
            Storyboard.SetTarget(animation, target is ScaleTransform ? MainContent : target);
            Storyboard.SetTargetProperty(animation, target is ScaleTransform
                ? new PropertyPath("(0).(1)", RenderTransformProperty, property)
                : new PropertyPath(property));
            storyboard.Children.Add(animation);
        }
    }

    private List<(FrameworkElement Element, Rect Bounds)> VisibleMotionTiles()
    {
        var result = new List<(FrameworkElement, Rect)>();
        var viewport = new Rect(0, 0, ContentScroll.ActualWidth, ContentScroll.ActualHeight);
        if (GroupsPanel.Visibility != Visibility.Visible || viewport.IsEmpty) return result;
        foreach (var group in GroupsPanel.Children.OfType<StackPanel>())
            foreach (var element in group.Children.OfType<GroupHeader>().Cast<FrameworkElement>()
                .Concat(group.Children.OfType<TilePanel>().SelectMany(panel => panel.Children.OfType<FrameworkElement>())))
            {
                var bounds = element.TransformToAncestor(ContentScroll).TransformBounds(new Rect(element.RenderSize));
                if (bounds.IntersectsWith(viewport)) result.Add((element, bounds));
            }
        return result;
    }

    private void RefreshEntranceTargets()
    {
        if (_motion == null || _dismissing) return;
        var elapsed = _motion.GetCurrentTime(this) ?? TimeSpan.Zero;
        // Icon/catalog completion may replace buttons while entrance is running.
        // Rebind at the current clock time rather than replaying the entrance.
        BeginMotion(false, initialOpacity: _entranceFromOpacity, initialScale: _entranceFromScale, elapsed: elapsed);
    }

    private void StopMotion()
    {
        var previous = _motion;
        _motion = null;
        previous?.Remove(this);
        foreach (var (element, opacity) in _motionTiles)
        {
            element.BeginAnimation(OpacityProperty, null);
            element.Opacity = opacity;
        }
        _motionTiles.Clear();
        _dismissing = false;
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        MainContent.RenderTransform = new ScaleTransform();
        Root.IsHitTestVisible = true;
    }

    private void CompleteEntrance()
    {
        if (_motion != null && !_dismissing) StopMotion();
    }

    private void RespectMotionPreference()
    {
        if (!MotionEnabled) { _tileTransitions.Settle(); _groupTransitions.Settle(); }
        if (_motion == null || MotionEnabled) return;
        var hide = _dismissing;
        StopMotion();
        if (hide) Hide();
    }
}
