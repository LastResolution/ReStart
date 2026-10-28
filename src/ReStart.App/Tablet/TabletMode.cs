using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ReStart.App.Tablet;

/// <summary>
/// Tracks the device posture. Tablet means slate posture (Z13 keyboard detached), unless
/// %LOCALAPPDATA%\ReStart\tablet-override.txt says "tablet" or "laptop" (written by
/// <c>ReStart.exe --tablet on|off|auto</c>), which lets tablet mode be tested remotely.
/// </summary>
internal sealed class TabletMode : IDisposable
{
    private const int SmConvertibleSlateMode = 0x2003; // 0 = slate
    private const int WmSettingChange = 0x001A;
    internal static readonly string OverridePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReStart", "tablet-override.txt");

    private readonly Dispatcher _dispatcher;
    private readonly HwndSource _listener;
    private readonly DispatcherTimer _poll;
    private bool _disposed;
    private bool? _lastSlate;

    /// <summary>Whether the shell currently behaves as a tablet.</summary>
    internal bool IsTablet { get; private set; }
    /// <summary>Raised on the UI thread when <see cref="IsTablet"/> changes.</summary>
    internal event Action<bool>? Changed;

    internal TabletMode(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        // A hidden top-level window: posture changes arrive as a WM_SETTINGCHANGE
        // ("ConvertibleSlateMode") broadcast, which message-only windows never get.
        _listener = new HwndSource(new HwndSourceParameters("ReStartTabletMode") { WindowStyle = 0, Width = 0, Height = 0 });
        _listener.AddHook(Hook);
        _poll = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Evaluate(), dispatcher);
        _poll.Start(); // fallback for a missed broadcast, and for override-file changes
        IsTablet = Compute();
    }

    /// <summary>Physical posture, ignoring the override.</summary>
    internal static bool IsSlate => GetSystemMetrics(SmConvertibleSlateMode) == 0;

    /// <summary>How long a forced posture lasts. It is a test aid, so a run that dies mid-test must not leave the user stuck in tablet mode.</summary>
    private static readonly TimeSpan OverrideLifetime = TimeSpan.FromMinutes(20);

    /// <summary>"tablet", "laptop" or null (follow the hardware). Expired overrides are deleted.</summary>
    internal static string? ReadOverride()
    {
        try
        {
            if (!File.Exists(OverridePath)) return null;
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(OverridePath) > OverrideLifetime) { ClearOverride("expired"); return null; }
            return File.ReadAllText(OverridePath).Trim().ToLowerInvariant() switch { "tablet" => "tablet", "laptop" => "laptop", _ => null };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    private static void ClearOverride(string why)
    {
        try { File.Delete(OverridePath); TabletLog.Write("mode", "override cleared: " + why); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Writes the override for <c>--tablet on|off|auto</c>.</summary>
    internal static void WriteOverride(string mode)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OverridePath)!);
        switch (mode.ToLowerInvariant())
        {
            case "on" or "tablet": File.WriteAllText(OverridePath, "tablet"); break;
            case "off" or "laptop": File.WriteAllText(OverridePath, "laptop"); break;
            default: if (File.Exists(OverridePath)) File.Delete(OverridePath); break;
        }
    }

    private static bool Compute() => ReadOverride() switch { "tablet" => true, "laptop" => false, _ => IsSlate };

    /// <summary>Re-reads the posture and raises <see cref="Changed"/> if it differs.</summary>
    internal void Evaluate()
    {
        if (_disposed) return;
        // Attaching or detaching the keyboard is the user speaking: it always beats a forced posture.
        var slate = IsSlate;
        if (_lastSlate is bool previous && previous != slate && File.Exists(OverridePath)) ClearOverride("keyboard " + (slate ? "detached" : "attached"));
        _lastSlate = slate;
        var value = Compute();
        if (value == IsTablet) return;
        IsTablet = value;
        TabletLog.Write("mode", (value ? "tablet" : "laptop") + (ReadOverride() is { } o ? $" (override {o})" : ""));
        Changed?.Invoke(value);
    }

    private nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmSettingChange) _dispatcher.BeginInvoke(Evaluate, DispatcherPriority.Background);
        return 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _poll.Stop();
        _listener.RemoveHook(Hook);
        _listener.Dispose();
    }

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
}
