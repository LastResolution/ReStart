using System.Runtime.InteropServices;
using ReStart.Core;

namespace ReStart.App.Services;

internal sealed record JumpDestination(string Name, string Target, bool Frequent, bool IsDirectory = false);

/// <summary>Reads public Recent/Frequent destination lists without decoding private shell databases.</summary>
internal static partial class AppJumpLists
{
    internal static Task<IReadOnlyList<JumpDestination>> ReadAsync(AppEntry app) => AppShellCommands.OnSta<IReadOnlyList<JumpDestination>>(() =>
    {
        try
        {
            if (!HistoryEnabled()) return [];
            var appId = ResolveAppId(app);
            if (string.IsNullOrWhiteSpace(appId)) return [];
            return ReadOnSta(appId);
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidCastException or UnauthorizedAccessException
            or System.Security.SecurityException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        { return []; }
    });

    private static IReadOnlyList<JumpDestination> ReadOnSta(string appId, Action<object, string>? visit = null)
    {
        object? instance = null;
        try
        {
            instance = Activator.CreateInstance(Type.GetTypeFromCLSID(new("86bec222-30f2-47e0-9f25-60d11cd75c28"))!);
            var lists = (IApplicationDocumentLists)instance!;
            Marshal.ThrowExceptionForHR(lists.SetAppID(appId));
            var entries = new List<JumpDestination>();
            for (var category = 0; category < 2; category++)
            {
                IObjectArray? array = null;
                try
                {
                    var arrayId = typeof(IObjectArray).GUID;
                    if (lists.GetList(category, 8, ref arrayId, out array) < 0) continue;
                    array.GetCount(out var count);
                    for (uint i = 0; i < Math.Min(count, 8); i++)
                    {
                        object? item = null;
                        try
                        {
                            var itemId = new Guid("00000000-0000-0000-c000-000000000046"); // IUnknown; entries can also be IShellLink.
                            if (array.GetAt(i, ref itemId, out item) < 0) continue;
                            IShellItem? shell = item as IShellItem;
                            object? resolved = null;
                            nint pidl = 0;
                            try
                            {
                                if (shell == null)
                                {
                                    // Resolve document links without interpreting command strings.
                                    // Links containing application arguments are not filesystem destinations.
                                    if (item is not IShellLink link) continue;
                                    var arguments = new System.Text.StringBuilder(32768);
                                    if (link.GetArguments(arguments, arguments.Capacity) < 0 || arguments.Length > 0) continue;
                                    if (link.GetIDList(out pidl) < 0 || pidl == 0) continue;
                                    var shellId = typeof(IShellItem).GUID;
                                    if (SHCreateItemFromIDList(pidl, ref shellId, out resolved) < 0) continue;
                                    shell = resolved as IShellItem;
                                }
                                if (shell == null) continue;
                                var target = Display(shell, 0x80028000); // SIGDN_DESKTOPABSOLUTEPARSING
                                var name = Display(shell, 0);
                                var isDirectory = Directory.Exists(target);
                                if (name.Length > 0 && (isDirectory || File.Exists(target)))
                                {
                                    visit?.Invoke(item, target);
                                    if (entries.All(e => !e.Target.Equals(target, StringComparison.OrdinalIgnoreCase))) entries.Add(new(name, target, category == 1, isDirectory));
                                }
                            }
                            finally { if (pidl != 0) Marshal.FreeCoTaskMem(pidl); Release(resolved); }
                        }
                        finally { Release(item); }
                    }
                }
                finally { Release(array); }
            }
            return entries;
        }
        finally { Release(instance); }
    }

    /// <summary>Removes a displayed automatic destination from Windows Recent/Frequent history; never deletes the file or changes pinned/custom tasks.</summary>
    /// <param name="app">Installed app whose resolved AppUserModelID owns the destination history.</param>
    /// <param name="target">Existing absolute filesystem destination, rechecked against the app's current automatic history.</param>
    /// <returns>A task completing after Windows accepts removal; missing, unavailable or no-longer-listed destinations fail without changing other history.</returns>
    internal static Task RemoveAsync(AppEntry app, string target) => AppShellCommands.OnSta(() =>
    {
        var id = ResolveAppId(app);
        if (!HistoryEnabled() || string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException(L.T(K.ErrorAppCommandUnavailable));
        RemoveOnSta(id, target); return true;
    });

    private static void RemoveOnSta(string appId, string target)
    {
        object? instance = null;
        try
        {
            instance = Activator.CreateInstance(Type.GetTypeFromCLSID(new("86c14003-4d6b-4ef3-a7b4-0506663b2e68"))!);
            var destinations = (IApplicationDestinations)instance!;
            Marshal.ThrowExceptionForHR(destinations.SetAppID(appId));
            var removed = false;
            ReadOnSta(appId, (item, path) =>
            {
                if (removed || !path.Equals(target, StringComparison.OrdinalIgnoreCase)) return;
                // Preserve the actual IShellItem/IShellLink identity returned
                // by Windows rather than fabricating an equivalent path item.
                Marshal.ThrowExceptionForHR(destinations.RemoveDestination(item)); removed = true;
            });
            if (!removed) throw new InvalidOperationException(L.T(K.ErrorAppCommandUnavailable));
        }
        finally { Release(instance); }
    }

    private static bool HistoryEnabled()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
        return key?.GetValue("Start_TrackDocs") is not int value || value != 0;
    }

    private static string ResolveAppId(AppEntry app)
    {
        if (app.IsShellApp) return app.LaunchTarget;
        if (!File.Exists(app.LaunchTarget)) return "";
        object? shell = null, folder = null, item = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            folder = ((dynamic)shell!).NameSpace(Path.GetDirectoryName(app.LaunchTarget));
            if (folder == null) return "";
            item = ((dynamic)folder).ParseName(Path.GetFileName(app.LaunchTarget));
            return item == null ? "" : ((dynamic)item).ExtendedProperty("System.AppUserModel.ID") as string ?? "";
        }
        finally { Release(item); Release(folder); Release(shell); }
    }

    private static string Display(IShellItem item, uint format)
    {
        if (item.GetDisplayName(format, out var pointer) < 0) return "";
        try { return Marshal.PtrToStringUni(pointer) ?? ""; }
        finally { Marshal.FreeCoTaskMem(pointer); }
    }
    private static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }

    [ComImport, Guid("3c594f9f-9f30-47a1-979a-c9e83d3d0a06"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationDocumentLists
    {
        [PreserveSig] int SetAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);
        [PreserveSig] int GetList(int type, uint count, ref Guid iid, out IObjectArray array);
    }
    [ComImport, Guid("12337d35-94c6-48a0-bce7-6a9c69d4d600"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationDestinations
    {
        [PreserveSig] int SetAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);
        [PreserveSig] int RemoveDestination([MarshalAs(UnmanagedType.IUnknown)] object destination);
        [PreserveSig] int RemoveAllDestinations();
    }
    [ComImport, Guid("92ca9dcd-5622-4bba-a805-5e9f541bd8c9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IObjectArray
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object value);
    }
    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig] int BindToHandler(nint context, ref Guid handler, ref Guid iid, out nint result);
        [PreserveSig] int GetParent(out IShellItem parent);
        [PreserveSig] int GetDisplayName(uint format, out nint text);
    }
    [ComImport, Guid("000214f9-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLink
    {
        [PreserveSig] int GetPath(nint path, int length, nint data, uint flags);
        [PreserveSig] int GetIDList(out nint pidl);
        [PreserveSig] int SetIDList(nint pidl);
        [PreserveSig] int GetDescription(nint text, int length);
        [PreserveSig] int SetDescription(nint text);
        [PreserveSig] int GetWorkingDirectory(nint path, int length);
        [PreserveSig] int SetWorkingDirectory(nint path);
        [PreserveSig] int GetArguments([MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder arguments, int length);
        [PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        [PreserveSig] int GetHotkey(out ushort value);
        [PreserveSig] int SetHotkey(ushort value);
        [PreserveSig] int GetShowCmd(out int value);
        [PreserveSig] int SetShowCmd(int value);
        [PreserveSig] int GetIconLocation(nint path, int length, out int index);
        [PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        [PreserveSig] int SetRelativePath(nint path, uint reserved);
        [PreserveSig] int Resolve(nint owner, uint flags);
        [PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
    [DllImport("shell32.dll")] private static extern int SHCreateItemFromIDList(nint pidl, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object item);
}
