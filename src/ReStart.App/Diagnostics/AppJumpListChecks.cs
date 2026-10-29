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
        var shortcut = Path.Combine(directory, "History sample.lnk");
        object? script = null, shortcutObject = null, shellLink = null;
        nint linkPointer = 0, idPointer = 0;
        var added = false;
        try
        {
            // Cast and query through the production reader. An empty result
            // must not hide a wrong COM class or unsupported interface.
            if (ReadOnSta(appId).Count != 0) throw new InvalidOperationException("The unique history fixture was not empty.");
            if (!HistoryEnabled()) return (1, false);
            script = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            shortcutObject = ((dynamic)script!).CreateShortcut(shortcut);
            ((dynamic)shortcutObject).TargetPath = target; ((dynamic)shortcutObject).Save();
            shellLink = Activator.CreateInstance(Type.GetTypeFromCLSID(new("00021401-0000-0000-c000-000000000046"))!);
            ((System.Runtime.InteropServices.ComTypes.IPersistFile)shellLink!).Load(shortcut, 0);
            linkPointer = Marshal.GetComInterfaceForObject(shellLink, typeof(IShellLink));
            idPointer = Marshal.StringToCoTaskMemUni(appId);
            var info = new HistoryAppIdLink { Link = linkPointer, AppId = idPointer };
            SHAddToRecentDocs(7, ref info); added = true;
            IReadOnlyList<JumpDestination> entries = [];
            for (var i = 0; i < 30; i++)
            {
                entries = ReadOnSta(appId);
                if (entries.Any(d => d.Target.Equals(target, StringComparison.OrdinalIgnoreCase))) break;
                Thread.Sleep(100);
            }
            if (!entries.Any(d => d.Target.Equals(target, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("The native reader did not discover its owned automatic destination.");
            RemoveOnSta(appId, target); added = false;
            if (ReadOnSta(appId).Any(d => d.Target.Equals(target, StringComparison.OrdinalIgnoreCase)) || !File.Exists(target))
                throw new InvalidOperationException("Native removal retained its automatic destination or deleted the file.");
            return (3, true);
        }
        finally
        {
            try { if (added) RemoveOnSta(appId, target); }
            finally
            {
                if (idPointer != 0) Marshal.FreeCoTaskMem(idPointer);
                if (linkPointer != 0) Marshal.Release(linkPointer);
                Release(shellLink); Release(shortcutObject); Release(script);
            }
        }
    });

    [StructLayout(LayoutKind.Sequential)] private struct HistoryAppIdLink { internal nint Link; internal nint AppId; }
    [DllImport("shell32.dll")] private static extern void SHAddToRecentDocs(uint flags, ref HistoryAppIdLink info);
}
