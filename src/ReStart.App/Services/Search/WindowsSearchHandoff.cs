namespace ReStart.App.Services;

/// <summary>Reports whether an explicit handoff opened, transferred text, or was superseded.</summary>
internal enum WindowsSearchHandoffResult { Failed, Transferred, OpenedWithoutQuery, Cancelled }

/// <summary>Separates the Windows-owned surface from cancellable launcher lifetime decisions.</summary>
internal interface IWindowsSearchHandoffBackend
{
    bool Open();
    Task<bool> TransferAsync(string query, CancellationToken cancellation);
    bool ModifiersHeld { get; }
    Task<bool> DismissForegroundAsync();
}

/// <summary>Owns only an explicit search handoff; returning cancels pending text before dismissing the foreground search surface.</summary>
internal sealed class WindowsSearchHandoff(IWindowsSearchHandoffBackend backend) : IDisposable
{
    private CancellationTokenSource? _pending;
    private int _generation;
    private bool _disposed;
    internal bool HasHandoff { get; private set; }

    internal static string? NormalizeQuery(string? query)
    {
        var text = query?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 2048 || query!.Any(char.IsControl)) return null;
        for (var i = 0; i < text.Length; i++)
            if (char.IsSurrogate(text[i]) && (!char.IsHighSurrogate(text[i]) || ++i >= text.Length || !char.IsLowSurrogate(text[i]))) return null;
        return text;
    }

    internal async Task<WindowsSearchHandoffResult> OpenAsync(string? query)
    {
        if (_disposed || NormalizeQuery(query) is not { } text) return WindowsSearchHandoffResult.Failed;
        _pending?.Cancel();
        var generation = ++_generation;
        using var pending = new CancellationTokenSource(); _pending = pending;
        try
        {
            if (!backend.Open()) return WindowsSearchHandoffResult.Failed;
            HasHandoff = true;
            var transferred = await backend.TransferAsync(text, pending.Token).WaitAsync(TimeSpan.FromSeconds(3), pending.Token);
            return generation != _generation ? WindowsSearchHandoffResult.Cancelled
                : transferred ? WindowsSearchHandoffResult.Transferred : WindowsSearchHandoffResult.OpenedWithoutQuery;
        }
        catch (OperationCanceledException) { return WindowsSearchHandoffResult.Cancelled; }
        catch (TimeoutException) { pending.Cancel(); return WindowsSearchHandoffResult.OpenedWithoutQuery; }
        finally { if (ReferenceEquals(_pending, pending)) _pending = null; }
    }

    internal async Task ReturnAsync()
    {
        _pending?.Cancel();
        var generation = ++_generation;
        if (!HasHandoff || _disposed) return;
        HasHandoff = false;
        // A hotkey may arrive while Ctrl/Alt/Win is still physically down. Never turn Escape into a different system chord.
        var deadline = Environment.TickCount64 + 600;
        while (backend.ModifiersHeld && generation == _generation && !_disposed && Environment.TickCount64 < deadline) await Task.Delay(20);
        if (generation == _generation && !_disposed && !backend.ModifiersHeld) await backend.DismissForegroundAsync();
    }

    public void Dispose() { _disposed = true; ++_generation; _pending?.Cancel(); HasHandoff = false; }
}
