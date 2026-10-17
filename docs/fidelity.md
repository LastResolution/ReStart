# Compatibility

[日本語](ja/fidelity.md)

ReStart is an independent implementation inspired by late Windows 10 full-screen Start. It does not include Microsoft Start code or replace Explorer binaries. Matching features does not imply identical animation, geometry or internal behavior.

## Platform

Portable builds target Windows 10/11 on x64 and ARM64. Development and native automated checks run on Windows 11 x64. ARM64 hardware, Windows 10 22H2, mixed-DPI/multiple-monitor combinations, touch, Narrator and all IME/physical shortcut sequences need device testing. Automated routed/composition events establish app behavior, not physical OS input compatibility.

## Behavior

| Area | ReStart behavior |
| --- | --- |
| Home | Full-screen working area, rail, pinned/all-apps views; reopening resets search, overlays and scroll |
| Tiles | Four sizes; movable rectangular groups; inline names and expandable tile folders |
| Placement extensions | Independent tiles, content-sized group width, half-cell horizontal anchors, horizontal scrolling and individual colors |
| Search | Immediate app-name search, including during catalog loading; no file/settings index, Web or Cortana |
| Catalog | AppsFolder and Start-menu links with native display names and icons; ambiguous shortcuts may appear twice |
| Menus | Pin/unpin and available canonical Shell commands; Jump Lists are not implemented |
| Desktop integration | Reversible Windows-key and Start-button capture, tray, optional sign-in startup and Explorer pin commands; no shell injection |
| Appearance | Dark, light, system and high contrast; wallpaper blur/tint approximates the original composition |
| Live tiles | Local clock/calendar and JavaScript packages; legacy UWP notifications and background delivery are not supported |
| Recommendations | Windows recent/most-used history is not imported |
| Data | Schema migration with original-file backups and protection against newer producers; no direct Windows layout import |
| Updates | Public GitHub Releases without an API key; manual installation |

The [test guide](testing.md) describes isolated native fixtures and manual checks. Power, elevation and uninstall actions are not executed by routine automated checks.

## Reference material

- [Microsoft: Make Start full screen](https://blogs.windows.com/windowsexperience/2017/09/11/windows-10-tip-make-start-full-screen/)
- [Microsoft: Full-screen Start layout changes in build 10547](https://blogs.windows.com/windows-insider/2015/09/18/announcing-windows-10-insider-preview-build-10547/)
- [Microsoft: Windows 10 Start layout](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/configuration/customize-and-export-start-layout)
