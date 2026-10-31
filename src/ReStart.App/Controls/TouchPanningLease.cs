using System.Windows.Controls;

namespace ReStart.App.Controls;

/// <summary>Temporarily lets a draggable control receive touch-to-mouse input instead of its enclosing scroll manipulation.</summary>
internal sealed class TouchPanningLease
{
    private ScrollViewer? _viewer;
    private object? _contact;
    private PanningMode _previous;

    /// <summary>Suspends panning for one contact, restoring any previous lease first.</summary>
    /// <param name="viewer">Viewer on the UI thread; its previous panning value is restored on release.</param>
    /// <param name="contact">Contact identity, compared by reference; does not transfer device ownership or capture input.</param>
    internal void Begin(ScrollViewer viewer, object contact)
    {
        if (ReferenceEquals(_viewer, viewer) && ReferenceEquals(_contact, contact)) return;
        Release();
        _viewer = viewer; _contact = contact; _previous = viewer.PanningMode;
        viewer.PanningMode = PanningMode.None;
    }

    /// <summary>Restores panning for the owning contact, or unconditionally when no identity is supplied.</summary>
    /// <param name="contact">Contact to release; null cancels any active lease. Unrelated contacts do not end the lease.</param>
    internal void Release(object? contact = null)
    {
        if (_viewer == null || contact != null && !ReferenceEquals(_contact, contact)) return;
        var viewer = _viewer;
        _viewer = null; _contact = null;
        viewer.PanningMode = _previous;
    }
}
