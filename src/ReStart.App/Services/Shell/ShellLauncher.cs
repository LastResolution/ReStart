using System.Diagnostics;
using ReStart.Core;

namespace ReStart.App.Services;

internal static class ShellLauncher
{
    public static void Launch(AppEntry app) => Launch(app.LaunchTarget, app.IsShellApp);

    public static void Launch(string target, bool shellApp = false, bool elevated = false)
    {
        if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException(L.T(K.ErrorNoTarget));
        // Tablet edition: Home is not topmost, so let an already-running app (single-instance
        // handoff) take the foreground and come up over Home.
        if (Tablet.TabletShell.IsTablet) AllowSetForegroundWindow(-1);
        if (shellApp)
        {
            // ArgumentList keeps the complete AUMID/path as one argument; never interpret it as a command.
            var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            info.ArgumentList.Add("shell:AppsFolder\\" + target);
            Process.Start(info);
        }
        else
        {
            var info = new ProcessStartInfo(target) { UseShellExecute = true };
            if (elevated) info.Verb = "runas";
            Process.Start(info);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
