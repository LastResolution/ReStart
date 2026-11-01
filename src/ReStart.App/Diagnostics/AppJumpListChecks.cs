using System.Runtime.InteropServices;

namespace ReStart.App.Services;

internal static partial class AppJumpLists
{
    // The unique AppID and owned link never touch an installed app's history.
    // SHARD_APPIDINFOLINK does not add an entry to the user's Recent folder.
    internal static Task<(int Checks, bool RoundTrip)> VerifyOwnedHistoryAsync(string directory) => AppShellCommands.OnSta(() =>
    {
        var appId = "ReStart.MenuFixture." + Guid.NewGuid().ToString("N");
        var target = Path.Combine(directory, "History sample 日本語.txt"); File.WriteAllText(target, "owned history fixture");
        object? shellLink = null;
        nint linkPointer = 0, idPointer = 0;
        var added = false;
        var stage = "bind the document reader";
        Exception? failure = null;
        try
        {
            // Cast and query through the production reader. An empty result
            // must not hide a wrong COM class or unsupported interface.
            if (ReadOnSta(appId).Count != 0) throw new InvalidOperationException("The unique history fixture was not empty.");
            if (!HistoryEnabled()) return (1, false);
            stage = "construct the owned link";
            shellLink = Activator.CreateInstance(Type.GetTypeFromCLSID(new("00021401-0000-0000-c000-000000000046"))!)
                ?? throw new InvalidOperationException("The native Shell link class was unavailable.");
            var nativeLink = (IShellLink)shellLink;
            Marshal.ThrowExceptionForHR(nativeLink.SetPath(target));
            Marshal.ThrowExceptionForHR(nativeLink.SetArguments(""));
            Marshal.ThrowExceptionForHR(nativeLink.SetIconLocation(Path.Combine(Environment.SystemDirectory, "shell32.dll"), 0));
            var titleKey = new HistoryPropertyKey { Format = new("f29f85e0-4ff9-1068-ab91-08002b27b3d9"), Id = 2 };
            var title = new HistoryStringVariant { Type = 31, Text = Marshal.StringToCoTaskMemUni("Owned history sample") };
            try
            {
                var properties = (IHistoryPropertyStore)shellLink;
                Marshal.ThrowExceptionForHR(properties.SetValue(ref titleKey, ref title));
                Marshal.ThrowExceptionForHR(properties.Commit());
            }
            finally { Marshal.FreeCoTaskMem(title.Text); }
            linkPointer = Marshal.GetComInterfaceForObject(shellLink, typeof(IShellLink));
            idPointer = Marshal.StringToCoTaskMemUni(appId);
            var info = new HistoryAppIdLink { Link = linkPointer, AppId = idPointer };
            stage = "add the owned automatic destination";
            SHAddToRecentDocs(7, ref info); added = true;
            stage = "read the owned automatic destination";
            IReadOnlyList<JumpDestination> entries = [];
            for (var i = 0; i < 30; i++)
            {
                entries = ReadOnSta(appId);
                if (entries.Any(d => d.Target.Equals(target, StringComparison.OrdinalIgnoreCase))) break;
                Thread.Sleep(100);
            }
            if (!entries.Any(d => d.Target.Equals(target, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("The native reader did not discover its owned automatic destination.");
            stage = "remove the owned automatic destination";
            RemoveOnSta(appId, target); added = false;
            if (ReadOnSta(appId).Any(d => d.Target.Equals(target, StringComparison.OrdinalIgnoreCase)) || !File.Exists(target))
                throw new InvalidOperationException("Native removal retained its automatic destination or deleted the file.");
            return (3, true);
        }
        catch (Exception ex)
        {
            failure = ex;
            throw new InvalidOperationException("Owned native history check failed while trying to " + stage + ".", ex);
        }
        finally
        {
            try
            {
                if (added)
                {
                    object? cleanup = null;
                    try
                    {
                        cleanup = Activator.CreateInstance(Type.GetTypeFromCLSID(new("86c14003-4d6b-4ef3-a7b4-0506663b2e68"))!);
                        var destinations = (IApplicationDestinations)cleanup!;
                        // Only this diagnostic's fresh random identity is cleared.
                        Marshal.ThrowExceptionForHR(destinations.SetAppID(appId));
                        Marshal.ThrowExceptionForHR(destinations.RemoveAllDestinations());
                    }
                    catch (Exception ex) when (failure != null && ex is COMException or InvalidOperationException)
                    { failure.Data["OwnedHistoryCleanup"] = ex.Message; }
                    finally { Release(cleanup); }
                }
            }
            finally
            {
                if (idPointer != 0) Marshal.FreeCoTaskMem(idPointer);
                if (linkPointer != 0) Marshal.Release(linkPointer);
                Release(shellLink);
            }
        }
    });

    internal static Task<int> VerifyOwnedAppFlagsAsync(string directory) => AppShellCommands.OnSta(() =>
    {
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "Owned app metadata.txt");
        File.WriteAllText(target, "owned metadata fixture; never executed");
        var checks = 0;
        for (var state = 0; state < 4; state++)
        {
            object? shellLink = null;
            try
            {
                shellLink = Activator.CreateInstance(Type.GetTypeFromCLSID(new("00021401-0000-0000-c000-000000000046"))!)!;
                Marshal.ThrowExceptionForHR(((IShellLink)shellLink).SetPath(target));
                var properties = (IHistoryPropertyStore)shellLink;
                for (uint id = 8; id <= 9; id++)
                {
                    var key = new HistoryPropertyKey { Format = new("9f4c2855-9f79-4b39-a8d0-e1d42de1d5f3"), Id = id };
                    var value = new HistoryStringVariant { Type = 11, Boolean = (short)((state & (id == 8 ? 1 : 2)) != 0 ? -1 : 0) };
                    Marshal.ThrowExceptionForHR(properties.SetValue(ref key, ref value));
                }
                Marshal.ThrowExceptionForHR(properties.Commit());
                var path = Path.Combine(directory, "Owned app flags " + state + ".lnk");
                ((System.Runtime.InteropServices.ComTypes.IPersistFile)shellLink).Save(path, true);
                var metadata = AppCatalog.ReadShortcutMetadata(path);
                if (metadata.ExcludeFromRecentlyAdded != ((state & 1) != 0) || metadata.ExcludeFromMostUsed != ((state & 2) != 0)
                    || !metadata.TargetPath.Equals(target, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The owned shortcut did not retain its Windows history exclusions and target.");
                checks++;
            }
            finally { Release(shellLink); }
        }
        return checks;
    });

    [StructLayout(LayoutKind.Sequential)] private struct HistoryAppIdLink { internal nint Link; internal nint AppId; }
    [StructLayout(LayoutKind.Sequential)] private struct HistoryPropertyKey { internal Guid Format; internal uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct HistoryStringVariant { [FieldOffset(0)] internal ushort Type; [FieldOffset(8)] internal nint Text; [FieldOffset(8)] internal short Boolean; }
    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IHistoryPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out HistoryPropertyKey key);
        [PreserveSig] int GetValue(ref HistoryPropertyKey key, out HistoryStringVariant value);
        [PreserveSig] int SetValue(ref HistoryPropertyKey key, ref HistoryStringVariant value);
        [PreserveSig] int Commit();
    }
    [DllImport("shell32.dll")] private static extern void SHAddToRecentDocs(uint flags, ref HistoryAppIdLink info);
}
