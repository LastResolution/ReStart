# Language and themes

[日本語](ja/localization.md)

Settings → Language offers English, Japanese and system language. Saving switches main UI text and date formatting; reopen Settings to see its new language. Systems outside Japanese use English. App names, saved tile/group names and JavaScript output remain unchanged.

Translations are embedded `src/ReStart.Core/Locales/en.json` and `ja.json`. `TextKey` contains stable semantic identifiers, not source-language sentences.

~~~csharp
TextCatalog.T(TextKey.NavSettings);
TextCatalog.F(TextKey.StatusPinned, appName);
~~~

~~~xml
<TextBlock Text="{local:Loc Key=NavSettings}" />
~~~

Add the enum member and both JSON entries together. Unknown keys fail compilation. Catalog loading checks matching key sets; tests validate nonempty values, format placeholders and fallback. Do not hardcode Japanese UI text in C#/XAML, including generated labels. Do not send user/OS-owned text through translation lookup.

Unsupported layouts are identified by `UnsupportedLayoutVersionException`, never by matching translated error text. Developer comments and primary documentation use English; Japanese documentation lives beside root docs or under `docs/ja`.

Settings navigation separates behavior, appearance, language, JavaScript, updates, layout/data and About; actions stay in a fixed footer. Checkboxes, combo boxes, sliders, scrollbars, dialogs and calendar use shared palette resources. System mode follows the Windows system theme for Start. High contrast takes precedence over wallpaper/effects and tile colors.

Classic app tiles share the selected global accent unless explicitly customized under ReStart → Tile background color. The override survives save/reopen and can be reset to the theme. Modern tiles have uniform translucent backplates and transparent app-list icon backgrounds. The optional accent-colored Start surface is dark-mode only. Individual tile colors are an extension; there is no per-group color setting.

Preview settings are independent fixture windows, with startup, network checks and credential writes disabled. Normal settings are owned by the main window.
