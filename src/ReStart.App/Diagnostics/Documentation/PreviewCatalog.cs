using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Matches curated preview labels to installed app identities without exposing the real catalog.</summary>
internal static class PreviewCatalog
{
    internal static AppEntry? Match(AppEntry sample, IEnumerable<AppEntry> installed)
    {
        var apps = installed.ToArray();
        var package = sample.Id switch
        {
            "edge" => "MSEdge",
            "mail" => "Microsoft.OutlookForWindows_",
            "photos" => "Microsoft.Windows.Photos_",
            "store" => "Microsoft.WindowsStore_",
            "notepad" => "Microsoft.WindowsNotepad_",
            "calc" => "Microsoft.WindowsCalculator_",
            "paint" => "Microsoft.Paint_",
            "terminal" => "Microsoft.WindowsTerminal",
            _ => ""
        };
        var match = package.Length == 0 ? null : apps.FirstOrDefault(a => a.LaunchTarget.StartsWith(package, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;
        var executable = sample.Id switch
        {
            "edge" => "msedge.exe",
            "notepad" => "notepad.exe",
            "calc" => "calc.exe",
            "paint" => "mspaint.exe",
            "terminal" => "wt.exe",
            "code" => "Code.exe",
            _ => ""
        };
        match = executable.Length == 0 ? null : apps.FirstOrDefault(a => Path.GetFileName(a.LaunchTarget).Equals(executable, StringComparison.OrdinalIgnoreCase));
        return match ?? apps.FirstOrDefault(a => a.Name.Contains(sample.Name, StringComparison.OrdinalIgnoreCase));
    }
}
