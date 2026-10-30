using System.Runtime.InteropServices;

namespace ReStart.App.Services;

internal static partial class NativeJumpWorker
{
    internal static IDisposable CreateOwnedFixture(string owner, string probe, string marker, string workingDirectory, string action = "--jump-fixture")
    {
        if (!owner.StartsWith("ReStart.JumpFixture.", StringComparison.Ordinal) || !File.Exists(probe)
            || !Path.GetFileName(probe).Equals("ReStart.ShellProbe.exe", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(marker) || !Directory.Exists(workingDirectory)) throw new ArgumentException("An owned jump-list fixture is required.");
        object? writerObject = null, arrayObject = null, linkObject = null;
        var begun = false;
        try
        {
            writerObject = Create(CustomClass);
            var writer = (IFixtureWriter)writerObject!;
            Marshal.ThrowExceptionForHR(writer.SetAppID(owner));
            var arrayId = typeof(IItems).GUID;
            Marshal.ThrowExceptionForHR(writer.BeginList(out _, ref arrayId, out var removed));
            Release(removed); begun = true;
            arrayObject = Create("2d3468c1-36a7-43b6-ac24-d3f02fd9607a");
            var array = (IFixtureCollection)arrayObject!;
            linkObject = Create("00021401-0000-0000-c000-000000000046");
            var link = (ILink)linkObject!;
            Marshal.ThrowExceptionForHR(link.SetPath(probe));
            Marshal.ThrowExceptionForHR(link.SetArguments(action + " \"" + marker + "\""));
            Marshal.ThrowExceptionForHR(link.SetWorkingDirectory(workingDirectory));
            Marshal.ThrowExceptionForHR(link.SetIconLocation(probe, 0));
            var titleKey = new PropertyKey { Format = new("f29f85e0-4ff9-1068-ab91-08002b27b3d9"), Id = 2 };
            var title = new Variant { Type = 31, Text = Marshal.StringToCoTaskMemUni("Owned sample action") };
            try
            {
                var properties = (IProperties)linkObject!;
                Marshal.ThrowExceptionForHR(properties.SetValue(ref titleKey, ref title));
                Marshal.ThrowExceptionForHR(properties.Commit());
            }
            finally { Marshal.FreeCoTaskMem(title.Text); }
            Marshal.ThrowExceptionForHR(array.AddObject(linkObject!));
            Marshal.ThrowExceptionForHR(writer.AddUserTasks((IItems)arrayObject!));
            Marshal.ThrowExceptionForHR(writer.CommitList()); begun = false;
            return new OwnedFixture(owner);
        }
        finally
        {
            if (begun && writerObject is IFixtureWriter writer) writer.AbortList();
            Release(linkObject); Release(arrayObject); Release(writerObject);
        }
    }

    private sealed class OwnedFixture(string owner) : IDisposable
    {
        public void Dispose()
        {
            object? writer = null;
            try { writer = Create(CustomClass); Marshal.ThrowExceptionForHR(((IFixtureWriter)writer!).DeleteList(owner)); }
            finally { Release(writer); }
        }
    }
    [ComImport, Guid("6332debf-87b5-4670-90c0-5e57b408a49e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFixtureWriter
    {
        [PreserveSig] int SetAppID([MarshalAs(UnmanagedType.LPWStr)] string owner);
        [PreserveSig] int BeginList(out uint slots, ref Guid iid, out IItems removed);
        [PreserveSig] int AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string name, IItems items);
        [PreserveSig] int AppendKnownCategory(int category);
        [PreserveSig] int AddUserTasks(IItems items);
        [PreserveSig] int CommitList();
        [PreserveSig] int GetRemovedDestinations(ref Guid iid, out IItems removed);
        [PreserveSig] int DeleteList([MarshalAs(UnmanagedType.LPWStr)] string owner);
        [PreserveSig] int AbortList();
    }
    [ComImport, Guid("5632b1a4-e38a-400a-928a-d4cd63230295"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFixtureCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object item);
        [PreserveSig] int AddObject([MarshalAs(UnmanagedType.IUnknown)] object item);
    }
}
