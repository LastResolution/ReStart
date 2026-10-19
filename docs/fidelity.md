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
| Placement extensions | Independent tiles, overlap-to-collect ordinary groups, content-sized group width, half-cell horizontal anchors, horizontal scrolling and individual colors |
| Editing extensions | Explicit edit mode, multi-selection moves and session-only layout undo/redo; these are ReStart additions |
| Search | Immediate app-name search, including during catalog loading; no file/settings index, Web or Cortana |
| Catalog | AppsFolder and Start-menu links with native display names and icons; ambiguous shortcuts may appear twice |
| Menus | Pin/unpin and available canonical Shell commands; Jump Lists are not implemented |
| Desktop integration | Reversible Windows-key and Start-button capture, tray, optional sign-in startup and Explorer pin commands; no shell injection |
| Motion | Separate window fade, bottom-anchored zoom and bottom-to-top tile reveal; animated dismissal, interrupted reopen and reduced-motion support |
| Appearance | Dark, light, system and high contrast; wallpaper blur/tint approximates the original composition |
| Live tiles | Local clock/calendar and JavaScript packages, including opt-in cached Web API/feed/file inputs; these are ReStart extensions, without legacy UWP notifications or background delivery |
| Recommendations | Windows recent/most-used history is not imported |
| Data | Schema migration with original-file backups and protection against newer producers; no direct Windows layout import |
| Updates | Public GitHub Releases without an API key; manual installation |

The [test guide](testing.md) describes isolated native fixtures and manual checks. Power, elevation and uninstall actions are not executed by routine automated checks.

## Opening and closing

ReStart collects tiles into an ordinary group when the overlap target is an independent non-folder tile; grouped targets create tile folders. Existing folders accept children regardless of their surrounding container. This independent-tile collection is a ReStart extension. A folder with one child is retained; it is removed after its last child leaves, following the documented [Windows 10 folder removal procedure](https://support.microsoft.com/en-au/windows/experience/personalization/customize-the-windows-start-menu).

The [Microsoft full-screen Start recording](https://blogs.windows.com/windowsexperience/2017/09/11/windows-10-tip-make-start-full-screen/) shows a gradual, bottom-anchored expansion and a lower-to-upper tile reveal. ReStart uses a 367 ms content entrance, a separate 120 ms window fade and up to 80 ms of row staggering. Exit recedes and fades over 167 ms. These are independently tuned WPF values based on the recording and [Microsoft motion guidance](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/motion), not recovered shell constants. The current guidance is newer than Windows 10 and does not establish its exact timing. A Windows 10 22H2 device comparison remains necessary for frame-exact fidelity.

Typing or clicking completes an entrance immediately so input does not wait for animation. Closing blocks further content clicks and hides the HWND only after exit. Reopening cancels the old exit callback and resumes from its visible opacity/scale. The app animation setting, Windows client-area animation setting and high contrast can disable motion; session actions and application shutdown dismiss immediately. Native fixtures seek real WPF clocks for frame checks and also await actual completion to cover stale callbacks and rapid toggles.

## Reference material

- [Microsoft: Make Start full screen](https://blogs.windows.com/windowsexperience/2017/09/11/windows-10-tip-make-start-full-screen/)
- [Microsoft: Full-screen Start layout changes in build 10547](https://blogs.windows.com/windows-insider/2015/09/18/announcing-windows-10-insider-preview-build-10547/)
- [Microsoft: Windows 10 Start layout](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/configuration/customize-and-export-start-layout)
