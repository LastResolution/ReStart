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
| Create groups by overlapping independent tiles | On | Overlap does not collect independent tiles into a group |
| Create folders by overlapping grouped tiles | On | Existing folders and explicit menu actions remain usable |
| Scroll automatically near board edges | On | Dragging does not scroll or extend empty board space |
| Drop hints and hover progress | On | Destination geometry remains visible without captions/progress |
| Recently added apps | On | Hides the optional app-list section |
| Most used apps | Off | Stops recording successful ReStart launches and hides the section |

**Animation** in Appearance controls opening, closing, tile press transitions and local folder reveal. Windows reduced-motion and high-contrast settings take precedence. Hover brightening has its own switch.

## Search

App results appear immediately. Settings results cover fifteen Windows pages with translated names and English aliases. File search waits 200 ms after typing and queries only the local Windows index for up to twenty existing filesystem entries. It matches literal filenames and never recursively scans disks. A disabled/unavailable index leaves app and settings search operational and displays a localized explanation. Arrow keys and Enter always operate on the selected visible result; late file results preserve that selection.

## Per-tile launch options

Right-click an app tile and choose **ReStart → Launch options…**. Enable **Use custom launch options** to set additional arguments, **Start in**, and **Run as administrator**. Turning the switch off preserves the fields and uses the original launch behavior. Saved shortcut arguments remain managed by Windows. Quote paths containing spaces; arguments are passed to the app's Shell verb without adding a command interpreter.

Custom options are available for supported desktop applications and application shortcuts. Administrator execution is selectable only when the app exposes the canonical Windows `runas` action. Other targets display an explanation. An invalid working directory is rejected before saving. Windows owns any UAC consent; an unsupported elevation fails without a fallback launch. Script live tiles and tile folders have their own interactions and do not use app launch options.

## Local activity

`activity.json` is stored beside `layout.json`. It holds a bounded catalog baseline, discovery dates and optional ReStart launch counts. Existing apps are baselined on the first run instead of appearing as newly added. New discoveries appear for seven days; Most used includes up to six apps launched through ReStart while recording is enabled. Removed apps disappear with the next catalog refresh.

**Clear app history** clears counts and discovery dates while retaining the catalog baseline. Activity is separate from layout export and is not uploaded. Disabling Most used preserves existing counts so they can be resumed or explicitly cleared. Windows app-usage history is not imported.

Document destinations come from Windows Recent/Frequent lists only when allowed by the Windows document-history setting. ReStart does not copy those lists into its activity file. Public Windows APIs do not expose application task lists or custom/pinned destination categories; unsupported identities produce no history menu. See [compatibility](fidelity.md).
