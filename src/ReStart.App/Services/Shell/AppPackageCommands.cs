using System.Runtime.InteropServices;
using ReStart.Core;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace ReStart.App.Services;

/// <summary>Resolves installed current-user packages for Windows app-management and Store links.</summary>
internal static class AppPackageCommands
{
    internal sealed record PackageInfo(string Family, bool StoreSigned);

    internal static PackageInfo? Read(AppEntry app)
    {
        if (!app.IsShellApp) return null;
        var separator = app.LaunchTarget.IndexOf('!');
        if (separator <= 0 || separator == app.LaunchTarget.Length - 1) return null;
        try
        {
            var family = app.LaunchTarget[..separator];
            var package = new PackageManager().FindPackagesForUser("", family).FirstOrDefault(p => !p.IsFramework && !p.IsResourcePackage);
            return package == null || package.SignatureKind == PackageSignatureKind.System ? null
                : new(package.Id.FamilyName, package.SignatureKind == PackageSignatureKind.Store && !package.IsDevelopmentMode);
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException or UnauthorizedAccessException or System.Security.SecurityException) { return null; }
    }

    internal static string? Target(PackageInfo package, string verb) => verb switch
    {
        "manageapps" => "ms-settings:appsfeatures",
        "review" when package.StoreSigned => "ms-windows-store://review/?PFN=" + Uri.EscapeDataString(package.Family),
        _ => null
    };

    internal static void Invoke(AppEntry app, string verb)
    {
        var package = Read(app);
        var target = package == null ? null : Target(package, verb);
        if (target == null) throw new InvalidOperationException(L.T(K.ErrorAppCommandUnavailable));
        ShellLauncher.Launch(target);
    }
}
