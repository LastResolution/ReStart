using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Adapts native shell input to dispatcher callbacks; low-level hooks must remain short and reversible.</summary>
internal sealed class StartIntegration : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Action _toggle;
    private readonly Action _dismiss;
    private readonly Action<Native.Point> _toggleFromStart;
    private readonly Native.HookProc _keyboardCallback;
    private readonly Native.HookProc _mouseCallback;
    private WindowsKeyState _keys = new();
    private nint _keyboard, _mouse;
    private readonly HwndSource _source;
    private readonly StartButtonLocator _startButtons = new();
    private volatile bool _captureStart;
    private readonly StartButtonPress _startPress = new();
    private volatile bool _captureWindows;
    private volatile bool _menuOpen, _dismissOnTaskbar;
    private readonly Thread _hookThread;
    private uint _hookThreadId;
    public string? Status { get; private set; }

    public StartIntegration(nint handle, Dispatcher dispatcher, Action toggle, Action dismiss, Action<Native.Point> toggleFromStart)
    {
        _dispatcher = dispatcher;
        _toggle = toggle;
        _dismiss = dismiss;
        _toggleFromStart = toggleFromStart;
        _keyboardCallback = KeyboardHook;
        _mouseCallback = MouseHook;
        _source = HwndSource.FromHwnd(handle)!;
        _source.AddHook(WindowProc);
        using var ready = new ManualResetEventSlim();
        _hookThread = new Thread(() =>
        {
            _hookThreadId = Native.GetCurrentThreadId();
            Native.PeekMessage(out _, 0, 0, 0, 0);
            ready.Set();
            try
            {
                while (Native.GetMessage(out var message, 0, 0, 0) > 0)
                {
                    if (message.Id == 0x8001) UpdateHooks();
                    else { Native.TranslateMessage(ref message); Native.DispatchMessage(ref message); }
                }
            }
            finally
            {
                if (_keyboard != 0) Native.UnhookWindowsHookEx(_keyboard);
                if (_mouse != 0) Native.UnhookWindowsHookEx(_mouse);
            }
        })
        { IsBackground = true, Name = "ReStart input hooks" };
        _hookThread.Start();
        ready.Wait();
        if (!Native.RegisterHotKey(handle, 0x5253, 0x4003, 0x20))
            Status = L.T(K.ErrorHotkeyInUse);
    }

    public void Apply(Preferences preferences)
    {
        _captureWindows = preferences.CaptureWindowsKey;
        _captureStart = preferences.CaptureStartButton;
        _startButtons.SetEnabled(_captureStart);
        _dismissOnTaskbar = preferences.CloseOnDeactivate;
        Native.PostThreadMessage(_hookThreadId, 0x8001, 0, 0);
    }

    /// <summary>Installs taskbar-dismiss detection only while the menu is visible.</summary>
    public void SetMenuOpen(bool visible)
    {
        _menuOpen = visible;
        Native.PostThreadMessage(_hookThreadId, 0x8001, 0, 0);
    }

    private void UpdateHooks()
    {
        if (_captureWindows && _keyboard == 0)
        {
            _keyboard = Native.SetWindowsHookEx(13, _keyboardCallback, Native.GetModuleHandle(null), 0);
            if (_keyboard == 0) Status = L.T(K.ErrorWindowsKeyCapture) + new Win32Exception(Marshal.GetLastWin32Error()).Message;
        }
        if (!_captureWindows && _keyboard != 0) { Native.UnhookWindowsHookEx(_keyboard); _keyboard = 0; _keys = new(); }
        var needMouse = _captureStart || _startPress.IsPressed || _dismissOnTaskbar && _menuOpen;
        if (needMouse && _mouse == 0) _mouse = Native.SetWindowsHookEx(14, _mouseCallback, Native.GetModuleHandle(null), 0);
        if (!needMouse && _mouse != 0) { Native.UnhookWindowsHookEx(_mouse); _mouse = 0; }
    }

    private nint WindowProc(nint hwnd, int msg, nint wp, nint lp, ref bool handled)
    {
        if (msg == 0x0312 && wp == 0x5253) { handled = true; QueueToggle(); }
        return 0;
    }

    private nint KeyboardHook(int code, nint wp, nint lp)
    {
        if (code < 0) return Native.CallNextHookEx(_keyboard, code, wp, lp);
        var data = Marshal.PtrToStructure<Native.KeyboardData>(lp);
        if ((data.Flags & 0x10) != 0) return Native.CallNextHookEx(_keyboard, code, wp, lp);
        var down = wp is 0x100 or 0x104;
        var modifier = (Native.GetAsyncKeyState(0x10) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x11) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x12) & 0x8000) != 0;
        switch (_keys.Process((int)data.VkCode, down, modifier))
        {
            case WindowsKeyAction.Suppress: return 1;
            case WindowsKeyAction.Toggle: QueueToggle(); return 1;
            case WindowsKeyAction.ReplayWindowsDown:
                Native.SendInput(1, [new Native.Input { Type = 1, Union = new() { Keyboard = new() { Vk = (ushort)_keys.HeldKey } } }], Marshal.SizeOf<Native.Input>());
                break;
        }
        return Native.CallNextHookEx(_keyboard, code, wp, lp);
    }

    private nint MouseHook(int code, nint wp, nint lp)
    {
        if (code >= 0 && wp is 0x201 or 0x202)
        {
            var point = Marshal.PtrToStructure<Native.MouseData>(lp).Point;
            switch (_startPress.Process(wp == 0x201, _captureStart && _startButtons.Contains(point), _captureStart))
            {
                case StartButtonAction.Toggle:
                    _dispatcher.BeginInvoke(() => _toggleFromStart(point), DispatcherPriority.Send);
                    return 1;
                case StartButtonAction.Suppress: return 1;
            }
        }
        if (code >= 0 && _menuOpen && _dismissOnTaskbar && wp is 0x201 or 0x204 or 0x207)
        {
            var data = Marshal.PtrToStructure<Native.MouseData>(lp);
            var window = Native.WindowFromPoint(data.Point);
            var rootName = new System.Text.StringBuilder(128);
            Native.GetClassName(Native.GetAncestor(window, 2), rootName, 128);
            if (rootName.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
                _dispatcher.BeginInvoke(_dismiss, DispatcherPriority.Input);
        }
        return Native.CallNextHookEx(_mouse, code, wp, lp);
    }

    private void QueueToggle() => _dispatcher.BeginInvoke(_toggle, DispatcherPriority.Send);

    public void Dispose()
    {
        _startButtons.Dispose();
        Native.PostThreadMessage(_hookThreadId, 0x12, 0, 0);
        _hookThread.Join(TimeSpan.FromSeconds(1));
        Native.UnregisterHotKey(_source.Handle, 0x5253);
        _source.RemoveHook(WindowProc);
    }
}
