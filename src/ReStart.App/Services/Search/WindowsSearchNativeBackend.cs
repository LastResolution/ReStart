using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace ReStart.App.Services;

/// <summary>Transfers text only to a focused edit owned by the exact Windows Search system executable; never uses the clipboard or types global text.</summary>
internal sealed class WindowsSearchNativeBackend : IWindowsSearchHandoffBackend
{
    private int _workerBusy;
    public bool Open() => WindowsSearchLauncher.TryOpen();
    public bool ModifiersHeld => new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(key => (Native.GetAsyncKeyState(key) & 0x8000) != 0);

    public Task<bool> TransferAsync(string query, CancellationToken cancellation)
    {
        if (Interlocked.CompareExchange(ref _workerBusy, 1, 0) != 0) return Task.FromResult(false);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new Thread(() =>
        {
            try { completion.TrySetResult(Transfer(query, cancellation)); }
            catch (Exception ex) when (Unavailable(ex)) { completion.TrySetResult(false); }
            finally { Interlocked.Exchange(ref _workerBusy, 0); }
        })
        { IsBackground = true, Name = "ReStart Windows Search handoff" };
        worker.SetApartmentState(ApartmentState.MTA); worker.Start();
        return completion.Task;
    }

    private static bool Transfer(string query, CancellationToken cancellation)
    {
        var deadline = Environment.TickCount64 + 2500;
        nint observed = 0;
        while (!cancellation.IsCancellationRequested && Environment.TickCount64 < deadline)
        {
            var target = ForegroundSearch();
            if (target != 0)
            {
                observed = target;
                if (TrySetFocusedQuery(query, target, ForegroundSearch, cancellation)) return true;
            }
            else if (observed != 0) return false; // The user left search; do not redirect focus or wait for them to come back.
            Thread.Sleep(40);
        }
        return false;
    }

    // The injectable identity read permits owned-window tests without launching or enumerating Windows Search.
    internal static bool TrySetFocusedQuery(string query, nint target, Func<nint> foregroundSearch, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested || target == 0 || foregroundSearch() != target) return false;
        var input = AutomationElement.FocusedElement;
        if (input == null || !IsSearchEdit(input, target) || !input.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)
            || pattern is not ValuePattern value || value.Current.IsReadOnly) return false;
        if (cancellation.IsCancellationRequested || foregroundSearch() != target || !IsSearchEdit(input, target)) return false;
        value.SetValue(query);
        return !cancellation.IsCancellationRequested && value.Current.Value == query;
    }

    private static bool IsSearchEdit(AutomationElement input, nint window)
    {
        Native.GetWindowThreadProcessId(window, out var process);
        var current = input.Current;
        if (current.ProcessId != process || current.ControlType != ControlType.Edit || !current.HasKeyboardFocus
            || !current.IsEnabled || current.IsOffscreen || current.IsPassword) return false;
        var parent = input;
        for (var depth = 0; parent != null && depth < 32; depth++, parent = TreeWalker.RawViewWalker.GetParent(parent))
            if (parent.Current.NativeWindowHandle == window) return true;
        return false;
    }

    public async Task<bool> DismissForegroundAsync()
    {
        var target = ForegroundSearch();
        if (target == 0 || ModifiersHeld || ForegroundSearch() != target) return false;
        var inputs = WindowsSearchLauncher.CreateDismissInputs();
        var sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.Input>());
        if (sent == 1) Native.SendInput(1, [inputs[1]], Marshal.SizeOf<Native.Input>());
        if (sent != inputs.Length) return false;
        // Allow Windows to consume Escape before ReStart takes foreground ownership.
        var deadline = Environment.TickCount64 + 400;
        while (ForegroundSearch() == target && Environment.TickCount64 < deadline) await Task.Delay(20);
        return ForegroundSearch() != target;
    }

    private static nint ForegroundSearch()
    {
        var window = Native.GetForegroundWindow();
        if (window == 0) return 0;
        var name = new System.Text.StringBuilder(128); Native.GetClassName(window, name, name.Capacity);
        if (name.ToString() != "Windows.UI.Core.CoreWindow") return 0;
        Native.GetWindowThreadProcessId(window, out var processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return WindowsSearchLauncher.IsSearchExecutable(process.MainModule?.FileName, Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ? window : 0;
        }
        catch (Exception ex) when (Unavailable(ex)) { return 0; }
    }

    private static bool Unavailable(Exception ex) => ex is ElementNotAvailableException or ElementNotEnabledException or COMException
        or InvalidOperationException or ArgumentException or Win32Exception or UnauthorizedAccessException or NotSupportedException;
}
