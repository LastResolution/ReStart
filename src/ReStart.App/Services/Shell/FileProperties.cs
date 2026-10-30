using System.Runtime.InteropServices;
using System.Windows.Threading;
using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Opens the actual filesystem item's property sheet on the application's persistent STA.</summary>
internal static class FileProperties
{
    internal static void Show(AppEntry app)
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA || Dispatcher.FromThread(Thread.CurrentThread) == null)
            throw new InvalidOperationException("File properties require the persistent UI apartment.");
        var location = AppFileLocation.Find(app) ?? throw new InvalidOperationException(L.T(K.ErrorAppCommandUnavailable));
        // An independent native sheet remains visible when full-screen Start
        // dismisses. A hidden Start HWND must not own a disappearing dialog.
        var root = Path.GetPathRoot(location.Path);
        var volume = string.Equals(Path.TrimEndingDirectorySeparator(root ?? ""), location.Path, StringComparison.OrdinalIgnoreCase);
        if (!SHObjectProperties(0, volume ? 4u : 2u, location.Path, null))
            throw new InvalidOperationException(L.T(K.ErrorAppCommandUnavailable));
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SHObjectProperties(nint owner, uint type, string path, string? page);
}
