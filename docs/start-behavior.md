# Start behavior

Right-click menus vary by surface: app-list and pinned-tile Shell actions are under **More**, while search exposes them directly. Existing files and directories can be revealed in Explorer, with the correct file/folder location label; matched app entries reveal their Start-menu shortcut. Live on/off is under **More**. See [context menus](context-menus.md) for the per-surface map and Windows-dependent actions.

Choose **Select tiles** from the sidebar or background context menu, or press **Ctrl+E**, to select several tiles without launching apps. Normal tile/group dragging is always available. **Finish selecting** leaves selection mode. The sidebar keeps the same icon size and position when expanded, and confirmations use the current palette with **No** as the safe default.

Open **ReStart settings → Start behavior** to choose how Start responds. Each switch is saved locally and takes effect after Save. Existing settings for Windows-key/Start-button capture, startup, close-on-deactivate, wallpaper, colors and animation remain in their respective sections.

| Setting | Default | Effect when disabled |
| --- | --- | --- |
| Expand navigation on hover | On | The menu button still expands navigation explicitly |
| Navigation shortcuts | Documents, Pictures | Each folder is independently selectable; none is also supported |
| Search Windows settings | On | Removes settings results; app search stays available |
| Search indexed files | On | Cancels pending file search and removes file results |
| Search indexed file contents | Off | Matches filenames only; enabling it also matches content already in the local Windows index |
| Use Windows advanced file queries | Off | Uses literal filename/content matching instead of the native AQS parser and relevance order |
| Show Windows Search handoff | On | Hides the explicit Windows Search action; typing never opens it |
| Show web search in the default browser | On | Hides the explicit Bing action; typing never sends a query externally |
| Rank equal app matches using enabled app history | On, active only with Most used history | Equal-quality matches use name order instead of enabled app usage; this setting does not start recording history |
| Windows app live tiles (compatibility) | Off | Does not open the Windows notification store; package apps retain their ordinary icons |
| Recent/frequent documents in app menus | On | Does not query Windows document history |
| Application tasks in app menus | On | Hides actions registered by installed applications |
| Application-defined jump-list categories | On | Hides registered categories without deleting their destinations |
| Pinned Windows jump-list items | On | Hides Windows-pinned destinations and their pin/unpin controls without deleting existing pins |
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
| Include existing Windows app usage | Off | Uses only ReStart launch counts; enabling it reads exact installed-app UserAssist observations when Windows tracking policy permits |

**Animation** in Appearance controls opening, closing, tile press transitions, local folder reveal and placement transitions. Windows reduced-motion and high-contrast settings take precedence. Hover brightening has its own switch.

## All apps and alphabet navigation

All apps uses the selected ReStart display language's Windows collation, rather than putting every non-ASCII name under Other. The public [CharacterGroupings API](https://learn.microsoft.com/en-us/uwp/api/windows.globalization.collation.charactergroupings) supplies the alphabet and heading labels; [CompareStringEx](https://learn.microsoft.com/en-us/windows/win32/api/stringapiset/nf-stringapiset-comparestringex) orders app and directory names with the same Windows language rules. Names and launch identities remain as supplied by Windows.

Symbols use **&** and numbers use **#**, including Windows-indexed circled, superscript and Roman numerals. Enclosed Latin letters also follow Windows' letter groups. Windows supplies accented Latin groups (including Spanish **Ñ**), Japanese kana and **漢字**, Korean initials, Simplified Chinese pinyin and Traditional Chinese stroke counts. Names in other scripts can add populated groups to the alphabet. This is based on display names; ReStart does not guess readings for ambiguous names or import Start's private sorting metadata.

Click a section heading to open the alphabet. Empty sections are disabled; long alphabets scroll. Selecting a letter scrolls to and focuses its current heading. While an app-list row has focus, a single Unicode letter or digit cycles through visible rows in that group, including expanded directories. Use <kbd>Ctrl</kbd> + <kbd>F</kbd> to search instead. Rebuilding the list after installation, removal or a language preview refreshes the open picker as well.

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

App results appear immediately. Settings results cover 63 documented Windows pages with names in all eight UI languages and English aliases. Page availability depends on the OS, edition and installed hardware. File search waits 200 ms after typing and queries only the local Windows index for up to fifty existing filesystem entries. It matches literal filenames; optional content matching uses Windows FREETEXT and the installed index filters. Content search starts off, does not enable indexing, and never recursively scans unindexed files. A disabled/unavailable index leaves app and settings search operational and displays a localized explanation. Arrow keys and Enter always operate on the selected visible result; late file results preserve that selection.

After the first query, a background catalog also reads Windows-supplied settings labels/keywords and Control Panel applets. It accepts only documented settings URI identifiers from the OS metadata and restricted Control Panel namespace paths; it never executes arbitrary `DeepLink` commands or `.settingcontent-ms` files. Native labels follow Windows' display language, as installed app names do. The available catalog varies by Windows build; the 63 translated entries remain a fallback. Late results preserve selection and do not delay immediate app matches.

**Use Windows advanced file queries** enables the public Windows AQS parser and native relevance ordering. Canonical queries such as `System.Kind:picture` and `System.Size:>1048576` avoid dependence on localized property aliases. The query stays in the local `SystemIndex`, with a fixed projection, fifty-result bound and existing three-second provider deadline. Turning content matching off rejects generated content/wildcard predicates even when the input explicitly names a content property. Unsupported syntax/providers fall back to literal matching.

**Search the web for…** opens the current query in Bing using the default browser. It is an explicit action, also available when local matches exist. Search text is escaped as one HTTPS query parameter; it is never interpreted as a command or arbitrary URL. Blank, malformed, control-bearing or overlong queries have no action. Typing and local search never contact Bing. The selected query is sent to Bing only after choosing the action; normal browser and search-provider privacy/history settings apply. ReStart requires no Web-search API key and does not embed or scrape provider results.

App, setting, file, Web and Windows/cloud actions share one native result list. **Up / Down**, **Tab / Shift+Tab** and **Enter** move focus, selection and activation together. Tab from the query enters its selected row; Shift+Tab from the first row returns to the query, and Tab after the last row leaves the list. Once local searches finish without a match, the first enabled online action is selected; Enter then invokes that visible action. It does not open while typing. An untouched fallback gives way to a late local match, while a row explicitly selected by keyboard, pointer or accessibility selection remains selected across refreshes.

**Search Windows and cloud** dismisses ReStart and invokes Windows’ documented Win+S shortcut for additional local, Web and account-backed sources. ReStart transfers the current query through the focused Windows Search input’s accessibility Value provider when available. It checks the protected system executable, window and focused editable control first; it does not use the clipboard or send global text keystrokes. If Windows does not expose a writable input, enter the query there. Returning to ReStart cancels pending text transfer and dismisses the foreground Windows Search surface before showing the launcher; unrelated foreground apps are left alone. Windows controls its own sources, accounts, network activity and policies. Cloud availability depends on Windows version, connected accounts and search permissions; this button does not grant access or sign in to an account. Typing in ReStart does not invoke this action. This does not reproduce Windows Search's proprietary ranking or cloud results inside ReStart. See [Windows Search and privacy](https://support.microsoft.com/en-gb/windows/privacy/windows-search-and-privacy) for Microsoft's source and account controls.

Local app matching prioritizes exact display names and executable aliases, then prefix, word-prefix and substring matches. With **Rank equal app matches using enabled app history**, positive launch counts and last launch time break equally strong matches when **Most used** recording is enabled. Its separately enabled Windows usage source can contribute the same read-only observations used by Most used. Turning either history source off stops using that source; personalization itself records no queries and enables no tracking. Final name ties use Windows language collation. A refresh retains the selected app identity so a newly arriving usage snapshot cannot silently change the Enter action. These rules improve local relevance; the complete private Windows Start/search order is not exposed by the file-index relevance API and is not claimed to match.

## Windows app tile compatibility

**Show Windows app live tiles (compatibility)** is disabled by default. When enabled, it opens the current user's existing Windows notification database read-only and selects active tile/badge rows only for pinned package app identities (up to 32 visible identities per read). It does not select toast messages, enumerate unrelated app content, recover deleted rows, copy the database or create notification channels. Reads are bounded, cancelled when disabled/exiting and paused while the launcher is hidden. SQLite may maintain its ordinary shared reader-lock metadata; ReStart issues no notification-store writes.

The renderer supports the common small/medium/wide/large adaptive and legacy bindings: inert text, groups/subgroups, local raster images, exact notification-data placeholders and badges. Missing size bindings, unknown schemas, expired records, unavailable data and unsupported content fall back to the app icon. Folder thumbnails use the small binding. Existing per-tile live-update controls and **Show live tile names** also apply. Package assets and `ms-appdata` local assets stay inside the owning package's roots; reparse points, external URLs, active image formats and oversized images are rejected. No remote images are downloaded. Repeated unchanged frames retain their visuals.

This is compatibility with data Windows and the app still publish. It does not restore discontinued push services, schedule another app's background tasks, reproduce every XML presentation/queue animation, or guarantee that an old app still supplies updates on Windows 11. ReStart's HTML/CSS/JavaScript tiles remain a separate system and receive no Windows notification or app-usage data.

## Per-tile launch options

Right-click an app tile and choose **ReStart → Launch options…**. Enable **Use custom launch options** to set additional arguments, **Start in**, and **Run as administrator**. Turning the switch off preserves the fields and uses the original launch behavior. Saved shortcut arguments remain managed by Windows. Quote paths containing spaces; arguments are passed to the app's Shell verb without adding a command interpreter.

Custom options are available for supported desktop applications and application shortcuts. Administrator execution is selectable only when the app exposes the canonical Windows `runas` action. Other targets display an explanation. An invalid working directory is rejected before saving. Windows owns any UAC consent; an unsupported elevation fails without a fallback launch. Script live tiles and tile folders have their own interactions and do not use app launch options.

## Local activity

`activity.json` is stored beside `layout.json`. It holds a bounded catalog baseline, discovery dates and optional ReStart launch counts. Existing apps are baselined on the first run instead of appearing as newly added. New discoveries appear for seven days, initially three rows with **Show more / Show less** for the remaining eligible entries. Most used includes up to six apps launched through ReStart while recording is enabled. Removed apps disappear with the next catalog refresh. Windows shortcut properties `System.AppUserModel.ExcludeFromShowInNewInstall` and `System.AppUserModel.PreventPinning` exclude entries from Recently added and Most used respectively; these exclusions do not hide search results or remove existing ReStart pins.

Recently added follows the Windows 10 three-row/expandable interaction, but its dates remain ReStart discoveries, not an imported Windows installation timeline. It does not invent installation dates from package or installer update timestamps. Microsoft documents that [Package.InstalledDate](https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.package.installeddate) can reflect an update; using it as an original-install date would incorrectly mark updated apps as new. Exact Windows eligibility, expiration and private Most used ranking remain outside the reproduced behavior.

**Clear app history** clears ReStart counts and discovery dates while retaining the catalog baseline. Activity is separate from layout export and is not uploaded. Disabling Most used preserves existing counts so they can be resumed or explicitly cleared.

**Include existing Windows app usage** is a separate opt-in source for Most used. It reads only exact identities and resolved executable/shortcut paths already present in the installed catalog, including Windows known-folder prefixes. It does not enumerate unrelated UserAssist value names, read document history, enable Windows tracking, write registry values or save the observations to disk. Windows app-launch tracking must be explicitly enabled and the relevant policies must allow it. Unknown record layouts and unavailable/disabled providers contribute no usage. Reads run away from the UI thread, are cached in memory for one minute and are cancelled on disabling the source or exiting. Local exclusions still apply. The larger of the local and Windows counts is used rather than adding counts that may describe the same launch. This approximates usage ordering, not Windows Start's private ranking formula. Clearing ReStart history does not erase Windows history; disable this source to hide it.

Document destinations come from Windows Recent/Frequent lists only when allowed by the Windows document-history setting. ReStart does not copy those lists into its activity file. Registered tasks, Windows pins and up to eight app-defined categories of eight destinations each use the isolated native adapter. Category labels belong to the installed app and are preserved rather than translated by ReStart. Unsupported identities omit these sections. See [compatibility](fidelity.md).

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

## Accounts and power

The account menu provides account settings, Lock, Sign out and **Switch user**, subject to the supported Windows policies. Switching preserves applications in the current local session and hands account selection and authentication to Windows. ReStart does not enumerate a duplicate account picker; Remote Desktop sessions omit this action.

Power offers Shut down and Restart, with Sleep and Hibernate when Windows capabilities and menu settings allow them. A full hibernation file is required for Hibernate. A pending Windows Update restart can add **Update and shut down** and **Update and restart** asynchronously. These commands finish Windows updates; ReStart's own updater remains in Settings. Failure to read Windows Update does not block the ordinary menu. Session-ending actions retain ReStart's themed confirmation; cancelling does not issue a Windows request. Applications may block an interactive shutdown, and ReStart never forces them closed.
