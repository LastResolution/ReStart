using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Localized, version-compatible Windows settings entry points; no Windows settings are changed by searching.</summary>
internal static class SettingsSearch
{
    private static readonly (TextKey Label, string Page)[] Pages =
    [
        (K.WindowsDisplay, "display"), (K.WindowsSound, "sound"), (K.WindowsNetwork, "network"),
        (K.WindowsBluetooth, "bluetooth"), (K.WindowsApps, "appsfeatures"), (K.WindowsStorage, "storagesense"),
        (K.WindowsUpdates, "windowsupdate"), (K.WindowsPrivacy, "privacy"), (K.WindowsPersonalization, "personalization"),
        (K.WindowsBackground, "personalization-background"), (K.WindowsColors, "colors"), (K.WindowsDateTime, "dateandtime"),
        (K.WindowsLanguage, "regionlanguage"), (K.WindowsAccessibility, "easeofaccess"), (K.WindowsPower, "powersleep")
    ];
    internal static IReadOnlyList<AppEntry> Find(string query)
    {
        var localized = Pages.Select(p => new AppEntry("restart:setting:" + p.Page, L.T(p.Label), "ms-settings:" + p.Page, false)).ToArray();
        var english = Pages.Select(p => new AppEntry("restart:setting:" + p.Page, TextCatalog.Translate(p.Label, "en"), "ms-settings:" + p.Page, false));
        return AppSearch.Find(localized, query, 6).Concat(AppSearch.Find(english, query, 6)
            .Select(a => localized.Single(l => l.Id == a.Id))).DistinctBy(a => a.Id).Take(6).ToArray();
    }
}
