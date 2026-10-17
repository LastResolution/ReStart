# Language and themes

[日本語](ja/localization.md)

Settings offers system language and en, ja, de, fr, es, ko, zh-Hans, zh-Hant. Regional tags resolve to the supported language; zh-TW/HK/MO/Hant select Traditional Chinese, other Chinese regions select Simplified Chinese. Unsupported languages fall back to English. Save switches main UI and formatting; reopen Settings for its new language. OS names and custom tile/group names remain user-owned. Default live-tile names follow language changes, while explicitly renamed tiles retain their custom label. Active script workers refresh their language context immediately.

TextKey defines stable semantic identifiers. All eight embedded Locales/*.json catalogs must contain the exact same keys and matching placeholders. Use TextCatalog.T / F and the Loc XAML extension. Unknown enum identifiers fail compilation; tests validate keys, nonempty translations, regional resolution and format arguments. No Japanese UI literals belong in C#/XAML. Developer/API comments are English; Japanese docs are counterparts. Translations have automated coverage, not professional linguistic review.

Settings uses a fixed footer with wrapping navigation and scrollable content. Checkboxes, combo boxes, sliders, scrollbars, dialogs and calendar share palette resources. Start system mode follows the Windows system theme. High contrast overrides wallpaper, effects and custom colors.

Classic app tiles share the global accent unless customized per tile. Modern backplates are uniform/translucent; app-list icons have transparent plates. Start accent surfaces are optional in dark mode. Per-tile colors and independent pins are ReStart extensions; per-group color is not provided. Wallpaper changes follow desktop notifications and refresh on opening, including same-file replacements and per-monitor images.

Preview fixtures do not change real startup, shell registration or network settings. Rendered tests cover all sixteen language/theme combinations.
