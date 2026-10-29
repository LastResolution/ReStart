# Start behavior

Choose **Select tiles** from the sidebar or background context menu, or press **Ctrl+E**, to select several tiles without launching apps. Normal tile/group dragging is always available. **Finish selecting** leaves selection mode. The sidebar keeps the same icon size and position when expanded, and confirmations use the current palette with **No** as the safe default.

Open **ReStart settings → Start behavior** to choose how Start responds. Each switch is saved locally and takes effect after Save. Existing settings for Windows-key/Start-button capture, startup, close-on-deactivate, wallpaper, colors and animation remain in their respective sections.

| Setting | Default | Effect when disabled |
| --- | --- | --- |
| Expand navigation on hover | On | The menu button still expands navigation explicitly |
| Navigation shortcuts | Documents, Pictures | Each folder is independently selectable; none is also supported |
| Search Windows settings | On | Removes settings results; app search stays available |
| Search indexed files | On | Cancels pending file search and removes file results |
| Recent/frequent documents in app menus | On | Does not query Windows document history |
| Highlight tiles on hover | On | Retains keyboard focus and press feedback |
| Group boundaries on hover | On | Boundaries remain available during editing and dragging |
| Use free tile placement | Off | Empty-space drops and pins create unnamed groups; existing independent entries are retained |
| Create groups by overlap (free placement only) | On, active only in free mode | Independent overlaps use folder behavior instead of collecting an ordinary group |
| Create folders by overlapping tiles | On | Existing folders and explicit menu actions remain usable |
| Scroll automatically near board edges | On | Dragging does not scroll or extend empty board space |
| Drop hints and hover progress | On | Destination geometry remains visible without captions/progress |
| Show shortcut tips while dragging | On | Hides the five-second Alt/Shift toast; drag behavior is unchanged |
| Recently added apps | On | Hides the optional app-list section |
| Most used apps | Off | Stops recording successful ReStart launches and hides the section |

**Animation** in Appearance controls opening, closing, tile press transitions, local folder reveal and placement transitions. Windows reduced-motion and high-contrast settings take precedence. Hover brightening has its own switch.

## Dragging, groups and folders

By default, dropping a tile into empty board space creates an **unnamed group**. Drag its header to move the group, or use the pencil to name it. Overlap another tile and wait for the folder preview to create a **tile folder**; a closed folder accepts more children. Groups and folders are separate containers, as in late Windows 10 Start.

Drop near a tile's edge to rearrange it without waiting. Hold <kbd>Shift</kbd> during a tile or selection drag to suppress overlap grouping and folder entry/creation completely; the requested cells are reserved and displaced tiles reflow into available positions. They may move sideways or below, depending on space and size. Releasing Shift starts a fresh folder dwell if the pointer is still over a tile. An already open folder remains a normal placement surface.

An internal rearrangement first reuses the space released by the move when the displaced tile fits. Other conflicts use nearby room before extending the container. Equal-sized neighbors yield a whole slot after half-overlap, keeping partial-cell previews from pushing them into a spare column. Moving in from another group or folder has no released slot in the destination.

Hold <kbd>Alt</kbd> while dragging to place a tile independently in empty space without changing your saved mode. Enable **Use free tile placement** to make independent empty-space drops and new pins the normal behavior. Free mode can optionally collect overlapping independent tiles into ordinary groups. Alt alone in the default mode does not change tile-over-tile folder behavior. Shift and Alt can be combined.

A translucent snapshot follows the pointer on display frames. Displaced neighbors ease to their proposed slots and retarget from their displayed positions; cancellation returns them to the saved layout. Folder recipients stay still while intent is being detected. Preview animation changes neither ownership nor saved coordinates. Existing layouts are not reorganized when upgrading; see [migration](migration.md#version-18-placement-defaults).

A noninteractive toast at the bottom explains Alt and Shift when a tile, selection or app drag starts. After five seconds it fades out over 220 ms; ending or cancelling the drag also dismisses it. It does not appear for group drags. Disable **Show shortcut tips while dragging** in Start behavior to hide it independently of destination captions. Reduced-motion settings skip the fade.

Press <kbd>F1</kbd> while Start is open, or choose **ReStart settings → Start behavior → Keyboard shortcuts**, for the localized reference. It groups launcher navigation, drag modifiers, selection-mode commands and Tile Studio commands.

## Initial layout and reset

The first run creates two groups from the installed app catalog. **Everyday** selects at most one browser, mail, photo, music, store, messaging and video app; **Tools** selects up to four editor, calculator, code, drawing and terminal apps, then adds File Explorer and Windows Settings. Known app identities take priority over exact English or translated app names. Helpers, installers and unrelated apps do not qualify merely because their names contain a familiar word. Selection is deterministic and preserves each selected catalog entry's launch target.

Month dots and Orbit clock are included in Everyday. Each group has at least six tiles: when installed apps are missing, distinct bundled examples fill the remaining places using local time or system metrics. Weather, feeds, arbitrary Web APIs and photo sources are not configured automatically. ReStart does not inspect document contents, scan personal folders or read Windows app-usage history to choose initial pins. File Explorer opens the current user's home folder, resolved locally at runtime.

Tile and group positions are packed automatically for the available board width. This seed runs only when no saved layout exists, or when **Reset layout** is explicitly confirmed. An upgrade never replaces existing pins; a deliberately empty saved layout remains empty. Reset preserves preferences while replacing pins.

## Search

App search matches the OS display name and additional executable filenames, with or without their extensions. Search-only metadata comes from Start-menu link targets, App Paths registrations matching those targets, and each installed package application's executable and declared execution aliases. For example, `notepad`, `cmd` or `wt` can find a localized app when that metadata is available. A package declaration can remain searchable even when its command-line execution alias is disabled in Windows; Enter still uses the original catalog launch identity. Registrations never create extra apps. Exact display names rank first, then exact argument-free executable aliases; developer/profile shortcuts sharing an executable but supplying launch arguments rank below ordinary app matches. Directory names, shortcut arguments and arbitrary command text are not indexed or executed by this feature. Metadata is refreshed with the existing catalog, so removed apps lose their aliases too.

App results appear immediately. Settings results cover fifteen Windows pages with translated names and English aliases. File search waits 200 ms after typing and queries only the local Windows index for up to twenty existing filesystem entries. It matches literal filenames and never recursively scans disks. A disabled/unavailable index leaves app and settings search operational and displays a localized explanation. Arrow keys and Enter always operate on the selected visible result; late file results preserve that selection.

## Per-tile launch options

Right-click an app tile and choose **ReStart → Launch options…**. Enable **Use custom launch options** to set additional arguments, **Start in**, and **Run as administrator**. Turning the switch off preserves the fields and uses the original launch behavior. Saved shortcut arguments remain managed by Windows. Quote paths containing spaces; arguments are passed to the app's Shell verb without adding a command interpreter.

Custom options are available for supported desktop applications and application shortcuts. Administrator execution is selectable only when the app exposes the canonical Windows `runas` action. Other targets display an explanation. An invalid working directory is rejected before saving. Windows owns any UAC consent; an unsupported elevation fails without a fallback launch. Script live tiles and tile folders have their own interactions and do not use app launch options.

## Local activity

`activity.json` is stored beside `layout.json`. It holds a bounded catalog baseline, discovery dates and optional ReStart launch counts. Existing apps are baselined on the first run instead of appearing as newly added. New discoveries appear for seven days; Most used includes up to six apps launched through ReStart while recording is enabled. Removed apps disappear with the next catalog refresh.

**Clear app history** clears counts and discovery dates while retaining the catalog baseline. Activity is separate from layout export and is not uploaded. Disabling Most used preserves existing counts so they can be resumed or explicitly cleared. Windows app-usage history is not imported.

Document destinations come from Windows Recent/Frequent lists only when allowed by the Windows document-history setting. ReStart does not copy those lists into its activity file. Public Windows APIs do not expose application task lists or custom/pinned destination categories; unsupported identities produce no history menu. See [compatibility](fidelity.md).

## Placement protection and keyboard editing

**Lock layout** protects placement, size and membership globally; a group menu can lock one group. Both start off. Locks also cover import/reset, undo/redo and indirect neighbor displacement. Launching, data connections and appearance remain available. Ctrl+Shift+L toggles the global lock.

**Finish selecting** and the **Pinned tiles** navigation action leave selection mode and return directly to the pinned home screen. The empty-home **Create a group and pin apps** action is aligned with the Pinned tiles row across tile scales and window sizes.

In Select tiles mode (Ctrl+E), arrows choose a tile, Space changes selection, Ctrl+Arrow moves the focused selection by one small cell, Ctrl+Shift+Arrow moves its group, and Alt+1–4 selects small/medium/wide/large. Ctrl+G collects the selection into a group; Shift+F10 opens its context menu. Valid edits are atomic undo steps. Locked or invalid destinations leave the original layout unchanged.

**Show live tile names**, under JavaScript tiles, defaults on. Turning it off hides native live-widget labels and bundled web-tile title markers; normal app labels and accessibility/tooltip names remain. Custom web packages receive `ctx.appearance.showTitle` and should mark their own name with `data-restart-title`.

Appearance can use the desktop wallpaper or a separately imported image. ReStart stores its own decoded copy and does not retain the original path. Background tint ranges from 10–100%; wallpaper blur is independently adjustable from 0–50 device-independent pixels, with a default of 12. A value of 0 disables blur. These effects apply to the background image, keeping foreground controls sharp. High contrast takes precedence.

## Settings preview and close decisions

Appearance, wallpaper, tile scale, group width, sidebar presentation and language preview before saving. Changes are coalesced while a control is being adjusted. An open settings editor relabels itself in the selected language while retaining its draft and section. Invalid in-progress RGB text remains an unsaved edit; it cannot be saved.

Closing through Close, Escape, Alt+F4 or the title bar offers **Save and close**, **Discard and close**, and **Keep editing** when there are changes. The default and Escape keep editing. Discard restores the latest committed palette, geometry and language; save errors leave the editor and draft available. An unchanged editor closes without a prompt. Imported background copies that were created by that editor and never saved are removed on close.

Preview uses a detached presentation overlay. It does not change saved preferences, tile coordinates, history, startup registration, Explorer integration or input hooks; unrelated layout saves cannot capture it. Explicit Start-board edits, layout import/reset and live-tile library actions keep their existing separately confirmed/immediate behavior. They do not become part of the settings draft. External folder/support actions, application exit and update restart resolve unsaved settings before proceeding.
