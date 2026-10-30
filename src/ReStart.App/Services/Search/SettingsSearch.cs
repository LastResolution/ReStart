using ReStart.Core;

namespace ReStart.App.Services;

/// <summary>Localized, version-compatible Windows settings entry points; no Windows settings are changed by searching.</summary>
internal static class SettingsSearch
{
    private static readonly (TextKey Label, string Page)[] Pages =
    [
        (TextKey.WindowsDisplay, "display"), (TextKey.WindowsSound, "sound"), (TextKey.WindowsNetwork, "network"),
        (TextKey.WindowsBluetooth, "bluetooth"), (TextKey.WindowsApps, "appsfeatures"), (TextKey.WindowsStorage, "storagesense"),
        (TextKey.WindowsUpdates, "windowsupdate"), (TextKey.WindowsPrivacy, "privacy"), (TextKey.WindowsPersonalization, "personalization"),
        (TextKey.WindowsBackground, "personalization-background"), (TextKey.WindowsColors, "colors"), (TextKey.WindowsDateTime, "dateandtime"),
        (TextKey.WindowsLanguage, "regionlanguage"), (TextKey.WindowsAccessibility, "easeofaccess"), (TextKey.WindowsPower, "powersleep"),
        (TextKey.WindowsSettingNetworkWifi, "network-wifi"),
        (TextKey.WindowsSettingNetworkEthernet, "network-ethernet"),
        (TextKey.WindowsSettingNetworkVpn, "network-vpn"),
        (TextKey.WindowsSettingNetworkProxy, "network-proxy"),
        (TextKey.WindowsSettingNetworkAirplanemode, "network-airplanemode"),
        (TextKey.WindowsSettingNetworkMobilehotspot, "network-mobilehotspot"),
        (TextKey.WindowsSettingMousetouchpad, "mousetouchpad"),
        (TextKey.WindowsSettingTyping, "typing"),
        (TextKey.WindowsSettingPrinters, "printers"),
        (TextKey.WindowsSettingUsb, "usb"),
        (TextKey.WindowsSettingAutoplay, "autoplay"),
        (TextKey.WindowsSettingDefaultapps, "defaultapps"),
        (TextKey.WindowsSettingOptionalfeatures, "optionalfeatures"),
        (TextKey.WindowsSettingStartupapps, "startupapps"),
        (TextKey.WindowsSettingSigninoptions, "signinoptions"),
        (TextKey.WindowsSettingYourinfo, "yourinfo"),
        (TextKey.WindowsSettingOtherusers, "otherusers"),
        (TextKey.WindowsSettingWorkplace, "workplace"),
        (TextKey.WindowsSettingActivation, "activation"),
        (TextKey.WindowsSettingAbout, "about"),
        (TextKey.WindowsSettingNotifications, "notifications"),
        (TextKey.WindowsSettingMultitasking, "multitasking"),
        (TextKey.WindowsSettingClipboard, "clipboard"),
        (TextKey.WindowsSettingRemotedesktop, "remotedesktop"),
        (TextKey.WindowsSettingRecovery, "recovery"),
        (TextKey.WindowsSettingTroubleshoot, "troubleshoot"),
        (TextKey.WindowsSettingTaskbar, "taskbar"),
        (TextKey.WindowsSettingFonts, "fonts"),
        (TextKey.WindowsSettingLockscreen, "lockscreen"),
        (TextKey.WindowsSettingThemes, "themes"),
        (TextKey.WindowsSettingPersonalizationStart, "personalization-start"),
        (TextKey.WindowsSettingRegionformatting, "regionformatting"),
        (TextKey.WindowsSettingSpeech, "speech"),
        (TextKey.WindowsSettingEaseofaccessMagnifier, "easeofaccess-magnifier"),
        (TextKey.WindowsSettingEaseofaccessNarrator, "easeofaccess-narrator"),
        (TextKey.WindowsSettingEaseofaccessKeyboard, "easeofaccess-keyboard"),
        (TextKey.WindowsSettingEaseofaccessMouse, "easeofaccess-mouse"),
        (TextKey.WindowsSettingEaseofaccessClosedcaptioning, "easeofaccess-closedcaptioning"),
        (TextKey.WindowsSettingEaseofaccessHighcontrast, "easeofaccess-highcontrast"),
        (TextKey.WindowsSettingPrivacyLocation, "privacy-location"),
        (TextKey.WindowsSettingPrivacyWebcam, "privacy-webcam"),
        (TextKey.WindowsSettingPrivacyMicrophone, "privacy-microphone"),
        (TextKey.WindowsSettingPrivacyFeedback, "privacy-feedback"),
        (TextKey.WindowsSettingWindowsdefender, "windowsdefender"),
        (TextKey.WindowsSettingWindowsupdateHistory, "windowsupdate-history"),
        (TextKey.WindowsSettingWindowsupdateOptions, "windowsupdate-options"),
        (TextKey.WindowsSettingGamingGamebar, "gaming-gamebar"),
        (TextKey.WindowsSettingGamingGamemode, "gaming-gamemode")
    ];
    internal static IReadOnlyList<AppEntry> Find(string query, string? language = null)
    {
        language ??= TextCatalog.Language;
        var localized = Pages.Select(p => new AppEntry("restart:setting:" + p.Page, TextCatalog.Translate(p.Label, language), "ms-settings:" + p.Page, false)).ToArray();
        var english = Pages.Select(p => new AppEntry("restart:setting:" + p.Page, TextCatalog.Translate(p.Label, "en"), "ms-settings:" + p.Page, false));
        return AppSearch.Find(localized, query, 6).Concat(AppSearch.Find(english, query, 6)
            .Select(a => localized.Single(l => l.Id == a.Id))).DistinctBy(a => a.Id).Take(6).ToArray();
    }
}
