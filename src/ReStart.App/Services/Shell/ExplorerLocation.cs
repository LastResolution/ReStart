using System.Runtime.InteropServices;

namespace ReStart.App.Services;

/// <summary>Uses Shell PIDLs to reveal a local item without constructing an explorer.exe command line.</summary>
internal static class ExplorerLocation
{
    internal static void Open(string path)
    {
        var full = Path.GetFullPath(path);
        if (!Path.IsPathFullyQualified(path) || !File.Exists(full) && !Directory.Exists(full)) throw new FileNotFoundException("The item is no longer available.");
        nint item = 0;
        try
        {
            Marshal.ThrowExceptionForHR(SHParseDisplayName(full, 0, out item, 0, out _));
            Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(item, 0, 0, 0));
        }
        finally { if (item != 0) Marshal.FreeCoTaskMem(item); }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, nint binding, out nint item, uint mask, out uint attributes);
    [DllImport("shell32.dll")]
    private static extern int SHOpenFolderAndSelectItems(nint item, uint count, nint selection, uint flags);
}
