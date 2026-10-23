using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ReStart.App.Controls;

/// <summary>Retargets layout-neutral offsets from their displayed value while preserving a control's own press transform.</summary>
internal sealed class PlacementTransitions
{
    private sealed class Entry(Transform original, TranslateTransform offset)
    {
        internal readonly Transform Original = original;
        internal readonly TranslateTransform Offset = offset;
        internal Vector Target;
        internal int Generation;
    }
    private readonly Dictionary<FrameworkElement, Entry> _entries = [];
    internal int Count => _entries.Count;
    internal long RetargetCount { get; private set; }
    internal static TimeSpan Duration => TimeSpan.FromMilliseconds(200);

    /// <summary>Projects a control relative to its unchanged layout slot; identical targets retain their running clock.</summary>
    /// <param name="element">UI-thread-owned control whose existing transform must be preserved.</param>
    /// <param name="target">Desired translation in parent DIPs; no model coordinates or layout slots are changed.</param>
    /// <param name="animate">Whether to interpolate from the currently displayed offset; false applies immediately.</param>
    internal void Move(FrameworkElement element, Vector target, bool animate)
    {
        if (!_entries.TryGetValue(element, out var entry))
        {
            if (target.LengthSquared < .0001) return;
            var offset = new TranslateTransform(); entry = new(element.RenderTransform, offset);
            var transforms = new TransformGroup(); transforms.Children.Add(entry.Original); transforms.Children.Add(offset);
            element.RenderTransform = transforms; _entries.Add(element, entry);
        }
        if ((entry.Target - target).LengthSquared < .0001)
        { if (!animate) Settle(element, entry); return; }
        Animate(element, entry, new(entry.Offset.X, entry.Offset.Y), target, animate);
    }

    /// <summary>Compensates for an already committed layout change, then settles into the new layout slot.</summary>
    /// <param name="element">Retained or newly created UI-thread-owned control.</param>
    /// <param name="displacement">Previous logical origin minus the new origin, in parent DIPs.</param>
    /// <param name="animate">False removes any offset immediately for reduced motion.</param>
    internal void Rebase(FrameworkElement element, Vector displacement, bool animate)
    {
        if (!animate) { if (_entries.TryGetValue(element, out var old)) Remove(element, old); return; }
        var current = _entries.TryGetValue(element, out var entry) ? new Vector(entry.Offset.X, entry.Offset.Y) : new();
        var from = current + displacement;
        if (from.LengthSquared < .0001) return;
        if (entry == null)
        {
            Move(element, from, false);
            entry = _entries[element];
        }
        Animate(element, entry, from, new(), true);
    }

    /// <summary>Returns all projected controls to their layout slots, optionally from their current displayed positions.</summary>
    /// <param name="animate">True eases a cancelled preview back; false restores original transforms synchronously.</param>
    internal void Reset(bool animate = false)
    {
        foreach (var (element, entry) in _entries.ToArray())
            if (animate) Move(element, new(), true); else Remove(element, entry);
    }

    /// <summary>Stops clocks at their requested targets when animations are disabled during an interaction.</summary>
    internal void Settle()
    {
        foreach (var (element, entry) in _entries.ToArray()) Settle(element, entry);
    }

    private void Animate(FrameworkElement element, Entry entry, Vector from, Vector target, bool animate)
    {
        entry.Generation++; var generation = entry.Generation; RetargetCount++;
        entry.Offset.BeginAnimation(TranslateTransform.XProperty, null); entry.Offset.BeginAnimation(TranslateTransform.YProperty, null);
        entry.Target = target; entry.Offset.X = from.X; entry.Offset.Y = from.Y;
        if (!animate) { Settle(element, entry); return; }
        var horizontal = Animation(from.X, target.X); var vertical = Animation(from.Y, target.Y);
        vertical.Completed += (_, _) => { if (entry.Generation == generation) Settle(element, entry); };
        entry.Offset.BeginAnimation(TranslateTransform.XProperty, horizontal, HandoffBehavior.SnapshotAndReplace);
        entry.Offset.BeginAnimation(TranslateTransform.YProperty, vertical, HandoffBehavior.SnapshotAndReplace);

        static DoubleAnimation Animation(double start, double end) => new(start, end, Duration)
        { FillBehavior = FillBehavior.HoldEnd, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
    }

    private void Settle(FrameworkElement element, Entry entry)
    {
        entry.Generation++;
        entry.Offset.BeginAnimation(TranslateTransform.XProperty, null); entry.Offset.BeginAnimation(TranslateTransform.YProperty, null);
        entry.Offset.X = entry.Target.X; entry.Offset.Y = entry.Target.Y;
        if (entry.Target.LengthSquared < .0001) Remove(element, entry);
    }

    private void Remove(FrameworkElement element, Entry entry)
    {
        entry.Generation++;
        entry.Offset.BeginAnimation(TranslateTransform.XProperty, null); entry.Offset.BeginAnimation(TranslateTransform.YProperty, null);
        element.RenderTransform = entry.Original; _entries.Remove(element);
    }
}
