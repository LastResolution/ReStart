# Compatibility

ReStart is an independent implementation inspired by late Windows 10 full-screen Start. It does not include Microsoft Start code or replace Explorer binaries. Matching features does not imply identical animation, geometry or internal behavior.

## Platform

Portable builds target Windows 10/11 on x64 and ARM64. Development and native automated checks run on Windows 11 x64. Connected-monitor placement and accessibility providers have reproducible native checks; their observed coverage is recorded below. Windows 10 22H2 and ARM64 hardware remain separate device targets. Automated routed/composition events and UI Automation providers do not establish physical input or spoken screen-reader behavior.

### Observed platform coverage

On October 22, 2026, `test-platform.ps1` passed 341 checks per run on Windows 11 x64 using fictional apps and isolated settings. Both hidden native hosts and explicitly shown WPF launcher windows were exercised. The default hidden run leaves foreground focus unchanged. Neither mode loads a user catalog or account picture.

| Area | Checked | Still to exercise |
| --- | --- | --- |
| Connected monitors | Native host and visible WPF launcher placement across two displays and back: 100%/100%, 100%/125%, 100%/150%, plus 100% landscape with 150% portrait (1080×1920). Checks cover working-area bounds, native/WPF DPI agreement, DIP-to-pixel fit and point-to-monitor mapping, including negative desktop coordinates | 200% scaling, display reconnects, changing configuration while the launcher remains open, production Start-button/foreground/taskbar interactions and other device/driver combinations |
| Accessibility providers | Sixteen language/theme cases: all eight languages in light and dark; named invokable navigation/app-tile buttons, editable search and the named selected search result | Full reading/navigation order, announcement timing, dialogs, folders and browser tile content with an actual screen reader |
| Narrator | Actual Windows Narrator speech recap reported Search as an edit control, then Pinned tiles and All apps as buttons during Tab navigation in an English fictional fixture | Listening to audio, pronunciation/clarity, the rest of the navigation order, other languages and complex interaction flows |
| Touch | Windows reports no digitizer and zero touch contacts on the test machine | Physical tap, long press, dragging, panning and edge scrolling on a touch device |

The mixed-DPI runs exposed WPF's suggested DPI-scaled window size overriding physical monitor bounds. Placement now lets WPF update its visual DPI and then coalesces a working-area correction on the dispatcher; both host types passed the configurations above. Windows display settings were restored after testing. Narrator was stopped afterward. Speech recap verifies actual announced text rather than sound quality; UI Automation provider checks alone establish neither. Physical touch and cable removal cannot be substituted with mouse input or synthetic geometry. Rerun the [platform fixture](testing.md#platform-and-accessibility-checks) on the actual configuration being evaluated.

## Behavior

| Area | ReStart behavior |
| --- | --- |
| Home | Full-screen working area, rail, pinned/all-apps views; reopening resets search, overlays and scroll. Optional hover expansion and configurable known-folder shortcuts |
| Tiles | Four sizes; movable rectangular groups; empty-space drops create unnamed groups by default; overlap creates expandable tile folders; inline names |
| Placement extensions | Optional independent placement, Alt-drag override, Shift-drag rearrangement without folders, optional free-mode overlap grouping, content-sized group width, half-cell horizontal anchors, horizontal scrolling and individual colors |
| Editing extensions | Explicit edit mode, multi-selection moves and session-only layout undo/redo; these are ReStart additions |
| Search | Immediate app-name, executable-name and OS-declared alias results, fifteen localized Windows settings entry points and cancellable local Windows-indexed filename results; selection matches Enter. Settings and files can be disabled independently. No Web or Cortana |
| Catalog | AppsFolder and Start-menu links with native display names and icons; ambiguous shortcuts may appear twice |
| Menus | [Context-dependent menus and coverage audit](context-menus.md): direct search actions, Start More submenus, physical shortcut/file/folder locations, package settings/review and available canonical Shell actions; optional Windows Recent/Frequent destinations with document actions/removal, local app-history exclusions and navigation-list customization. Registered Tasks and Windows-synchronized pinned destinations/pin controls use an isolated native adapter. Arbitrary custom categories are not enumerated; Windows may withhold taskbar/share verbs or change the adapter's undocumented contracts |
| Launching | Optional per-tile arguments, working directory and canonical administrator launch for supported desktop apps/shortcuts; Windows retains elevation consent |
| Desktop integration | Reversible Windows-key and Start-button capture, tray, optional sign-in startup and Explorer pin commands; no shell injection |
| Motion | Separate window fade, bottom-anchored zoom and bottom-to-top tile reveal; animated dismissal, interrupted reopen and reduced-motion support |
| Appearance | Dark, light, system and high contrast; 10–100% background tint and independently adjustable wallpaper blur (0–50, default 12) approximate the original composition |
| Live tiles | Local clock/calendar, native JS compatibility and HTML/CSS/browser JavaScript packages, including opt-in cached Web API/feed/file inputs; these are ReStart extensions, without legacy UWP notifications or background delivery |
| App history | Optional Recently added and Most used, based on local discovery dates and opt-in successful ReStart launches. Existing apps are baselined; history can be cleared. Windows usage history is not imported |
| Data | Schema migration with original-file backups and protection against newer producers; no direct Windows layout import |
| Updates | Public GitHub Releases without an API key; explicit verified portable updates with startup acknowledgment and rollback; manual replacement remains available |

The [test guide](testing.md) describes isolated native fixtures and manual checks. Power, elevation and uninstall actions are not executed by routine automated checks.

## Opening and closing

Default drag behavior follows the Windows 10 distinction: empty space creates an unnamed group, while a sustained tile overlap creates a tile folder. Microsoft documents [group creation and naming](https://blogs.windows.com/windowsexperience/2016/11/07/windows-10-tip-four-ways-to-customize-the-start-menu/) and [tile folders introduced with the Creators Update](https://blogs.windows.com/windowsexperience/2017/06/05/windows-10-tip-organize-start-files-new-tile-folders/). Existing folders accept children regardless of their surrounding container. Free placement, Alt-drag, Shift-drag and optional independent-tile collection are ReStart extensions. A folder with one child is retained; it is removed after its last child leaves, following the documented [Windows 10 folder removal procedure](https://support.microsoft.com/en-au/windows/experience/personalization/customize-the-windows-start-menu).

The [Microsoft full-screen Start recording](https://blogs.windows.com/windowsexperience/2017/09/11/windows-10-tip-make-start-full-screen/) shows a gradual, bottom-anchored expansion and a lower-to-upper tile reveal. ReStart uses a 367 ms content entrance, a separate 120 ms window fade and up to 80 ms of row staggering. Exit recedes and fades over 167 ms. These are independently tuned WPF values based on the recording and [Microsoft motion guidance](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/motion), not recovered shell constants. The current guidance is newer than Windows 10 and does not establish its exact timing. A Windows 10 22H2 device comparison remains necessary for frame-exact fidelity.

Typing or clicking completes an entrance immediately so input does not wait for animation. Closing blocks further content clicks and hides the HWND only after exit. Reopening cancels the old exit callback and resumes from its visible opacity/scale. The app animation setting, Windows client-area animation setting and high contrast can disable motion; session actions and application shutdown dismiss immediately. Native fixtures seek real WPF clocks for frame checks and also await actual completion to cover stale callbacks and rapid toggles.

## Reference material

The [November 2019 announcement](https://blogs.windows.com/windowsexperience/2019/11/12/how-to-get-the-windows-10-november-2019-update/) documents Start navigation expanding on hover. ReStart uses an independently chosen 600 ms delay. Tile press transitions and the 140 ms local folder reveal respect reduced motion. Hover brightness has its own switch.

Drag intent uses a 600 ms dwell for folders. The optional free-mode independent-tile group gesture uses 300 ms. Folder overlap enters at 65% and retains the current target down to 55%; free-mode groups use 40%/30%. Shift suppresses overlap intent and allows ordinary displacement at the same location. These are ReStart choices, not recovered shell constants. Preview captions and progress can be hidden. Edge scrolling follows a stationary viewport pointer without committing preview coordinates.

Ordinary single-tile drops project displaced tiles and neighboring groups before committing, including inside open folders. The projection uses the same layout operations as a drop, keeps live browser instances, and restores the original visuals on cancellation. Overlap recipients remain stationary during group/folder intent detection so that moving feedback cannot change the target underneath the pointer.

The [Acer Support recording](https://www.youtube.com/watch?v=UlU-Ato4fdA) shows moving a tile to empty space and making a new group. [Chris' Tutorials' Windows 10 recording](https://www.youtube.com/watch?v=RLibs5ByNvw&t=122s) shows rearrangement at 2:02, group creation around 3:00 and folders from 3:57. These are recordings of the original interface, rather than a replacement launcher. They establish the broad interactions; the observed scenes do not establish a universal two-tile swap rule or every mixed-size collision case.

ReStart reuses cells vacated within the same destination when displaced tiles fit there. Remaining conflicts use nearby free space inside the existing content width before adding rows; tiles outside the conflict retain their anchors. An external insertion has no source vacancy inside the destination. During a drag over equal-sized neighbors, a half-overlap chooses the complete occupied slot instead of evicting a neighbor at an intermediate small-cell position. This prevents transient container growth and unrelated group displacement. The half-overlap threshold and local reflow policy are independent implementation choices; exact Windows 10 collision ordering still needs a matching device comparison. Normal dragging, selection dragging, open folders, cancellation and undo use the same planning operations.

Aligned group drags reorder a single overlapping horizontal or vertical stack when overlap reaches half the smaller group's length along that axis. Recipients reuse the vacated span and its slot gaps instead of continually moving away from the pointer. Before that threshold, the recipient stays stationary; after passing it, it stays in the vacated space. Planning always uses the committed baseline, keeps local tile coordinates, and rejects a rotation that would displace an unrelated group.

For diagonal moves or stacks that cannot rotate, local displacement prefers the incoming group's source side: an arrival from the right first tries space on the right. The dominant direction is measured from the committed source to the proposed anchor, normalized by the dragged group's width and height, rather than transient animation positions or noisy pointer velocity. A candidate must clear fixed neighbors and fit the board's existing horizontal extent or available lanes; otherwise ordinary downward packing remains the fallback. Discrete keyboard nudges retain exact-anchor collision handling. These thresholds and direction preferences are ReStart choices, not recovered Windows 10 constants.

Pointer ghosts and edge scrolling share a display-frame callback. Neighbor displacement, group preview and committed placement use retained translation animations with a 200 ms ease-out; new targets continue from the currently displayed value. Logical hit testing excludes those transient transforms. Native checks cover intermediate states, rapid retargeting, cancellation, reduced motion and stationary-preview reuse. These checks establish behavior, not a hardware frame-rate guarantee or an exact match to Windows shell timing.

Tiles, groups, app-list rows and multiple selections use a translucent pointer-following snapshot while their original views are dimmed. The grab position and relative selection geometry are retained; oversized selections scale down to a bounded preview. This is a ReStart interaction improvement. The overlay never receives input, and leaving the launcher or cancelling a drag hides it without changing the layout.

File queries use the local Windows SystemIndex with a 200 ms debounce, literal filename matching and a maximum of twenty existing local filesystem entries. There is no recursive fallback scan or remote search. The fifteen settings entry points support localized names and English aliases, rather than reproducing the complete Windows settings index.

The public [IApplicationDocumentLists interface](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-iapplicationdocumentlists) exposes Recent/Frequent only and needs an authoritative app identity. Unknown identities, disabled Windows history and unsupported command-bearing links contribute no destinations. Registered tasks and Windows-pinned destinations use a separate, bounded helper around undocumented Shell contracts; their availability depends on the app and Windows build. Arbitrary custom categories are not enumerated. Current Windows 11 and CI fixture coverage does not establish every Windows 10 build or physical ARM64 behavior. Canonical app commands remain available separately.

See [Start behavior](start-behavior.md) for feature switches, defaults and local data storage.

- [Microsoft: Make Start full screen](https://blogs.windows.com/windowsexperience/2017/09/11/windows-10-tip-make-start-full-screen/)
- [Microsoft: Full-screen Start layout changes in build 10547](https://blogs.windows.com/windows-insider/2015/09/18/announcing-windows-10-insider-preview-build-10547/)
- [Microsoft: Windows 10 Start layout](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/configuration/customize-and-export-start-layout)

Placement locks, full keyboard placement editing, Tile Studio, separate owned background images and browser live tiles are ReStart extensions. They are not claims about the Windows 10 shell. Nineteen web examples are checked at four sizes and in eight languages through a real hidden browser fixture; browser sandbox restrictions and runtime requirements are documented in the tile guide.

## Additional settings workflow

ReStart previews appearance, geometry and language without saving them, and offers save/discard/continue on closing a changed settings editor. It also retains a downloaded update across ordinary launches with a persistent restart reminder. These are ReStart conveniences, rather than claims about a native Windows 10 settings or update interface.

## Reliability extensions

Private profile backups, privacy-minimized bounded logs, viewport suspension and bounded browser recovery are ReStart additions. Browser/renderer crash recovery and 50/100-tile scheduling have isolated native checks. Physical hardware coverage remains as recorded above; no new Windows 10, ARM64 or touch-device verification is implied.
