using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Reflection;
using Microsoft.Win32;
using ReStart.Core;

namespace ReStart.App.Services;

internal enum StartSessionAction { Lock, SignOut, SwitchUser, Sleep, Hibernate, Shutdown, Restart, UpdateShutdown, UpdateRestart }
internal sealed record StartSessionState(bool Sleep, bool Hibernate, bool Power, bool Lock, bool SignOut, bool SwitchUser, bool UpdateRequired);

/// <summary>Reads local power capabilities and policies without changing them. Session effects run only after an explicit menu command.</summary>
internal static class StartSessionCommands
{
    internal static StartSessionState Read()
    {
        var known = GetPwrCapabilities(out var power);
        return new(known && (power.S1 != 0 || power.S2 != 0 || power.S3 != 0 || power.ModernStandby != 0)
                && Option("ShowSleepOption", true),
            known && power.S4 != 0 && power.HiberFile != 0 && power.HiberFileType == 2 && Option("ShowHibernateOption", true),
            !Policy(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoClose"),
            !Policy(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableLockWorkstation"),
            !Policy(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoLogoff"),
            GetSystemMetrics(0x1000) == 0 && !Policy(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", "HideFastUserSwitching"), false);
    }

    internal static IReadOnlyList<(StartSessionAction Action, TextKey Label)> PowerItems(StartSessionState state)
    {
        if (!state.Power) return [];
        var items = new List<(StartSessionAction, TextKey)>();
        if (state.Sleep) items.Add((StartSessionAction.Sleep, K.PowerSleep));
        if (state.Hibernate) items.Add((StartSessionAction.Hibernate, K.PowerHibernate));
        items.Add((StartSessionAction.Shutdown, K.PowerShutdown));
        items.Add((StartSessionAction.Restart, K.PowerRestart));
        if (state.UpdateRequired)
        {
            items.Add((StartSessionAction.UpdateShutdown, K.PowerUpdateShutdown));
            items.Add((StartSessionAction.UpdateRestart, K.PowerUpdateRestart));
        }
        return items;
    }

    internal static Task<bool> UpdateRequiredAsync() => AppShellCommands.OnSta(() =>
    {
        object? information = null;
        try
        {
            information = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.SystemInfo", true)!);
            return information!.GetType().InvokeMember("RebootRequired", BindingFlags.GetProperty, null, information, null) is true;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or TargetInvocationException or MissingMethodException) { return false; }
        finally { if (information != null && Marshal.IsComObject(information)) Marshal.ReleaseComObject(information); }
    });

    internal static void Execute(StartSessionAction action)
    {
        var state = Read();
        if (action == StartSessionAction.SwitchUser)
        {
            if (!state.SwitchUser) throw new InvalidOperationException("Switching users is disabled by Windows policy.");
            if (!WTSDisconnectSession(0, uint.MaxValue, false)) throw new Win32Exception();
            return;
        }
        if (action == StartSessionAction.Lock)
        {
            if (!state.Lock) throw new InvalidOperationException("Locking is disabled by Windows policy.");
            if (!LockWorkStation()) throw new Win32Exception();
            return;
        }
        if (action == StartSessionAction.SignOut)
        {
            if (!state.SignOut) throw new InvalidOperationException("Signing out is disabled by Windows policy.");
            if (!ExitWindowsEx(0, 0x80000000)) throw new Win32Exception();
            return;
        }
        if (!PowerItems(state with { UpdateRequired = true }).Any(item => item.Action == action))
            throw new InvalidOperationException("This power action is unavailable.");
        // Enable only the current process's existing shutdown right, and restore it
        // immediately after the API call. Never force users or applications closed.
        if (!OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle, 0x28, out var token)) throw new Win32Exception();
        try
        {
            if (!LookupPrivilegeValue(null, "SeShutdownPrivilege", out var id)) throw new Win32Exception();
            var requested = new TokenPrivilege { Count = 1, Id = id, Attributes = 2 };
            if (!AdjustTokenPrivileges(token, false, ref requested, (uint)Marshal.SizeOf<TokenPrivilege>(), out var previous, out _)) throw new Win32Exception();
            var privilegeError = Marshal.GetLastWin32Error();
            try
            {
                if (privilegeError != 0) throw new Win32Exception(privilegeError);
                if (action is StartSessionAction.Sleep or StartSessionAction.Hibernate)
                {
                    if (!SetSuspendState(action == StartSessionAction.Hibernate, false, false)) throw new Win32Exception();
                    return;
                }
                var result = InitiateShutdown(null, null, 0, ShutdownFlags(action), 0x80000000);
                if (result != 0) throw new Win32Exception((int)result);
            }
            finally { AdjustTokenPrivileges(token, false, ref previous, (uint)Marshal.SizeOf<TokenPrivilege>(), out _, out _); }
        }
        finally { CloseHandle(token); }
    }

    internal static uint ShutdownFlags(StartSessionAction action) => action switch
    {
        StartSessionAction.Shutdown => 0x208, // power off, preserving Windows fast startup
        StartSessionAction.Restart => 4,
        StartSessionAction.UpdateShutdown => 0x48, // install updates + full power off
        StartSessionAction.UpdateRestart => 0x44,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static bool Policy(string path, string name) => ReadValue(Registry.CurrentUser, path, name) == 1 || ReadValue(Registry.LocalMachine, path, name) == 1;
    private static bool Option(string name, bool fallback)
    {
        var policy = ReadValue(Registry.LocalMachine, @"Software\Policies\Microsoft\Windows\Explorer", name);
        var setting = policy ?? ReadValue(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings", name);
        return setting is null ? fallback : setting != 0;
    }
    private static int? ReadValue(RegistryKey root, string path, string name)
    {
        try { using var key = root.OpenSubKey(path); return key?.GetValue(name) is int value ? value : null; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { return null; }
    }

    // Windows SDK SYSTEM_POWER_CAPABILITIES (76 bytes); BOOLEAN fields are one byte.
    [StructLayout(LayoutKind.Explicit, Size = 76)]
    private struct PowerCapabilities
    {
        [FieldOffset(3)] internal byte S1;
        [FieldOffset(4)] internal byte S2;
        [FieldOffset(5)] internal byte S3;
        [FieldOffset(6)] internal byte S4;
        [FieldOffset(8)] internal byte HiberFile;
        [FieldOffset(20)] internal byte ModernStandby;
        [FieldOffset(22)] internal byte HiberFileType;
    }
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct TokenPrivilege { [FieldOffset(0)] internal uint Count; [FieldOffset(4)] internal long Id; [FieldOffset(12)] internal uint Attributes; }
    [DllImport("powrprof.dll")][return: MarshalAs(UnmanagedType.U1)] private static extern bool GetPwrCapabilities(out PowerCapabilities capabilities);
    [DllImport("powrprof.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.U1)] private static extern bool SetSuspendState([MarshalAs(UnmanagedType.U1)] bool hibernate, [MarshalAs(UnmanagedType.U1)] bool force, [MarshalAs(UnmanagedType.U1)] bool disableWake);
    [DllImport("wtsapi32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTSDisconnectSession(nint server, uint session, [MarshalAs(UnmanagedType.Bool)] bool wait);
    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool LockWorkStation();
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ExitWindowsEx(uint flags, uint reason);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "InitiateShutdownW")] private static extern uint InitiateShutdown(string? machine, string? message, uint seconds, uint flags, uint reason);
    [DllImport("advapi32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool LookupPrivilegeValue(string? system, string name, out long id);
    [DllImport("advapi32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool AdjustTokenPrivileges(nint token, [MarshalAs(UnmanagedType.Bool)] bool disableAll, ref TokenPrivilege state, uint size, out TokenPrivilege previous, out uint returned);
    [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
}
