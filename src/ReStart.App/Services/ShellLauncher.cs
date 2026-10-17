using System.Diagnostics;
using ReStart.Core;

namespace ReStart.App.Services;

internal static class ShellLauncher
{
    public static void Launch(AppEntry app) => Launch(app.LaunchTarget, app.IsShellApp);

    public static void Launch(string target, bool shellApp = false, bool elevated = false)
    {
        if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("起動先がありません。");
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
}
