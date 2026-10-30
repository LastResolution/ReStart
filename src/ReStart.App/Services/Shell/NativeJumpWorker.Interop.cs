using System.Runtime.InteropServices;
using System.Text;

namespace ReStart.App.Services;

internal static partial class NativeJumpWorker
{
    // Windows 10 build 10547+ ABI contracts, researched from Open-Shell.
    // Only the disposable worker uses them. See third-party attribution.
    [ComImport, Guid("507101cd-f6ad-46c8-8e20-eeb9e6bac47f"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICustomList
    {
        [PreserveSig] int SetMinItems();
        [PreserveSig] int SetApplicationID([MarshalAs(UnmanagedType.LPWStr)] string owner);
        [PreserveSig] int GetSlotCount();
        [PreserveSig] int GetCategoryCount(out uint count);
        [PreserveSig] int GetCategory(uint index, int flags, ref Category category);
        [PreserveSig] int DeleteCategory();
        [PreserveSig] int EnumerateCategoryDestinations(uint index, ref Guid iid, out IItems items);
    }
    [ComImport, Guid("e9c5ef8d-fd41-4f72-ba87-eb03bad5817c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAutomaticList
    {
        [PreserveSig] int Initialize([MarshalAs(UnmanagedType.LPWStr)] string owner, [MarshalAs(UnmanagedType.LPWStr)] string? link, [MarshalAs(UnmanagedType.LPWStr)] string? unused);
        [PreserveSig] int HasList([MarshalAs(UnmanagedType.Bool)] out bool value);
        [PreserveSig] int GetList(int kind, uint limit, uint flags, ref Guid iid, out IItems items);
        [PreserveSig] int AddUsagePoint();
        [PreserveSig] int PinItem([MarshalAs(UnmanagedType.IUnknown)] object item, int index);
    }
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct Category { [FieldOffset(0)] internal int Type; [FieldOffset(8)] internal nint Data; }
    [ComImport, Guid("92ca9dcd-5622-4bba-a805-5e9f541bd8c9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IItems
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object item);
    }
    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IItem
    {
        [PreserveSig] int BindToHandler(nint context, ref Guid handler, ref Guid iid, out nint value);
        [PreserveSig] int GetParent(out IItem item);
        [PreserveSig] int GetDisplayName(uint format, out nint text);
    }
    [ComImport, Guid("000214f9-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ILink
    {
        [PreserveSig] int GetPath([MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int size, nint data, uint flags);
        [PreserveSig] int GetIDList(out nint pidl);
        [PreserveSig] int SetIDList(nint pidl);
        [PreserveSig] int GetDescription([MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int size);
        [PreserveSig] int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string value);
        [PreserveSig] int GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int size);
        [PreserveSig] int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string value);
        [PreserveSig] int GetArguments([MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int size);
        [PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string value);
        [PreserveSig] int GetHotkey(out ushort value);
        [PreserveSig] int SetHotkey(ushort value);
        [PreserveSig] int GetShowCmd(out int value);
        [PreserveSig] int SetShowCmd(int value);
        [PreserveSig] int GetIconLocation(nint value, int size, out int index);
        [PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        [PreserveSig] int SetRelativePath(nint value, uint reserved);
        [PreserveSig] int Resolve(nint owner, uint flags);
        [PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string value);
    }
    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IProperties
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out Variant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref Variant value);
        [PreserveSig] int Commit();
    }
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { internal Guid Format; internal uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct Variant { [FieldOffset(0)] internal ushort Type; [FieldOffset(8)] internal nint Text; [FieldOffset(8)] internal short Boolean; }
    [ComImport, Guid("000214e4-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMenu
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(ref InvokeInfo info);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct InvokeInfo
    {
        internal uint Size, Mask;
        internal nint Owner, Verb, Parameters, Directory;
        internal int Show;
        internal uint HotKey;
        internal nint Icon, Title, VerbW, ParametersW, DirectoryW, TitleW;
        internal int PointX, PointY;
    }
    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivation
    {
        [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string app, [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint process);
    }
    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint flags);
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref Variant value);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHCreateItemFromParsingName(string path, nint context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object item);
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)] private static extern int SHLoadIndirectString(string source, StringBuilder result, uint size, nint reserved);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern uint GetMenuDefaultItem(nint menu, uint position, uint flags);
}
