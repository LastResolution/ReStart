# Architecture

[日本語](ja/architecture.md)

ReStart is a WPF desktop launcher. It owns its layout, search and live-tile protocol; it does not replace Explorer binaries.

## Components

| Project | Responsibility |
| --- | --- |
| `ReStart.Core` | Models, tile/group geometry, layout mutations, search, localization, persistence, release parsing and input state |
| `ReStart.App` | WPF views, shell catalog/commands, hooks, tray, settings, update checks and worker lifetime |
| `ReStart.TileHost` | Jint worker exchanging bounded JSON messages |
| `ReStart.Core.Tests` | Domain invariants, persistence recovery, update feeds and isolated Windows integration tests |
| `scripts` / `.github` | Native fixtures, portable packages, CI and releases |

Most Core logic is independent of WPF. Windows-only synchronization and test-linked services declare their platform requirements.

## Placement and editing

`TileLayout` reserves explicit cells before packing unplaced or colliding tiles. The four footprints are 1×1, 2×2, 4×2 and 4×4. Layout calculations do not mutate input models.

`GroupLayout` stores group columns and vertical anchors in half-cell steps. Groups retain independent positions and gaps; only intersecting bounds are displaced. A new X anchor stores horizontal logical cells; wide boards retain their positions and scroll rather than rewriting them on narrow screens. Preview and commit use the same calculation, preserving each tile's identity, size and internal coordinates.

A schema-4 `TileGroup.IsStandalone` entry stores exactly one independent tile. It renders without a group header/menu and occupies only that tile's width and height. Its tile column supplies the offset inside the board column; its vertical anchor belongs to the board. This is an explicit ReStart extension requested for detached tiles, not a claim about Windows 10's internal model. Ordinary groups measure occupied content; the configured 6/8 cells limit internal wrapping. Group X anchors snap in half-cell steps.

`GroupBoardPanel` uses the same rectangles for measure and arrange. `LayoutEditor.Detach` freezes visible anchors and moves ownership once. `FolderExpansion` adds temporary inline content without changing saved root rows. Expanded independent folders temporarily use the column width for their children while keeping the root tile's horizontal position.

`LayoutHistory` retains deep snapshots of groups and one-level children only, bounded to 100 edits and 32 MiB. Successful writes record one operation; identical saves retain redo. Editing after undo drops the redo branch. Preferences, upgrade metadata, live-script runtime state and external files remain outside history. Undo/redo persists through the same atomic store and restores its cursor if writing fails. History lives only in memory and survives hiding Start, not process exit.

Edit mode suppresses tile activation and uses stable IDs for selection across redraws and history restoration. Folder parents and children cannot be selected together. `TileBatchMove.Plan` copies the layout, freezes existing anchors, validates a rigid selection and reserves selected cells before reflowing conflicts. Open-space moves reserve all independent tiles before displacing neighbors. Invalid and cancelled plans leave the source unchanged; preview and commit share the plan. A one-entry cache avoids recomputing the same snapped destination. The group menu explicitly compacts selected tiles; dragging preserves their relative positions.

Drop routing distinguishes root group cells, expanded folder children, tile-over-tile collection, and independent board space. Overlapping an independent non-folder tile creates an ordinary group through `TileGrouping.Plan`; it copies the layout, collects roots compactly and reserves earlier neighbors before placing the new header. The target's tile origin is retained when space permits; a header collision moves the new group down minimally instead of pushing an earlier neighbor below it. Grouped targets create folders; existing folders accept children in either container. Single selections can create folders; batches can enter existing folders or collect around an independent target. Nested folders are rejected. A one-child folder remains a folder until its last child is removed.

Independent grouping starts at 40% overlap of the smaller tile area and arms after 300 ms. A tracked target stays eligible down to 30% overlap so small pointer drift does not restart the timer. Leaving that tolerance resets the dwell. Folder creation/addition retains its 65% overlap and 1000 ms dwell. These are ReStart interaction choices, not measured Windows shell constants.

Single-tile preview and commit share one resolver. Root overlap also resolves compact independent targets whose bounds do not contain a larger dragged tile's center. During OLE dragging, the dwell timer retains the latest drag-event position in viewport coordinates and reprojects it after scrolling. It does not poll WPF's mouse device, which can still report the original grab position while OLE owns pointer tracking. Leaving the content or ending/cancelling a drag retires the timer.

## Catalog and shell

`AppCatalog` enumerates AppsFolder and user/common Start-menu .lnk/.url files on dedicated STA threads. User entries win for equal relative paths. SHGetFileInfo supplies localized display names; directory paths remain available for `AppListTree`.

`AppCatalogLayout` matches explicit AUMIDs, unique target-plus-argument identities, then unambiguous names. It preserves existing shell IDs and keeps ambiguous shortcuts distinct. Icons are obtained with IShellItemImageFactory using ICONONLY and SCALEUP, avoiding miniature folder/document thumbnail badges. Images are frozen for WPF sharing and native handles are released.

App directories expand inline. Column balancing keeps headings and initial children together. Search uses the flat catalog, including closed directories. Input preferences are applied before catalog loading; completing the catalog reruns any pending query without taking navigation focus.

Four independent STA workers load icons into frozen images. `IconCache` stores PNG pixels under SHA-256 identities in the user's local data folder, with a one-day refresh, file length/write-time invalidation and a 1,024-entry bound. Damaged or inaccessible cache entries fall back to the Shell. No source paths or app-name index are stored in the cache; deleting it is safe. Catalog/icon refreshes rebuild the board twice during initialization rather than once per icon batch.

Search results use native ListBox selection. The first result is highlighted while the native TextBox retains its caret; arrows move row focus and selection, and Enter invokes the selected rendered action. A same-query refresh retains the selected app identity even if ranking changes.

`AppShellCommands` reads IContextMenu canonical verbs on an STA and displays supported enabled commands. Invocation re-queries the command ID. Preview never invokes them. App launch targets are passed as shell identities or individual arguments, not evaluated as shell command text.

## Lifetime and input

A per-user/session mutex prevents duplicate normal instances across versions and paths. A named event activates the existing instance. Preview has a separate scope. Background duplicate launches do not open the menu.

The initial foreground presentation uses `StartupWindow` while catalog, icons and initial tile frames are prepared. Its native TextBox accepts typeahead and IME composition; the query and an optional Enter intent transfer to the prepared launcher. Worker startup overlaps icon loading. Hidden content is measured before script bounds are sent, and chosen photos/system-rate samples are allowed to settle. External data and failed scripts produce bounded loading/error states rather than blocking Start indefinitely. Esc, a second toggle, taskbar dismissal or deactivation cancels presentation without a later unsolicited popup. Sign-in background initialization shows no splash and executes no tile scripts or data reads until Start is requested. Subsequent presentations reuse loaded icons and cached frames.

Low-level hooks run a short Win32 message loop on a dedicated thread. A bare Windows key toggles ReStart; combinations replay the pending Windows-key down. UI work is dispatched. Hooks, hotkeys and workers are released on exit. Native Start-button capture uses recent UI Automation bounds scoped to Explorer's primary/secondary taskbars; no DLL injection or guessed hit regions are used.

While Start is visible and close-on-deactivation is enabled, an out-of-context `EVENT_SYSTEM_FOREGROUND` subscription observes subsequent activations even if a taskbar thumbnail already deactivated WPF. The callback rechecks the current window, ownership, visibility generation and event time before dismissing. Owned dialogs and transient taskbar/menu surfaces are excluded; taskbar thumbnail clicks also use the click-dismiss path. Observation is unregistered when hidden, disabled or disposed. Dialog and drag guards still apply.

Sign-in startup manages one current-user Run value, reconciles its actual state, and restores it if settings saving fails.

## Persistence, updates and credentials

Schema 4 stores tiles, preferences, independent entries, producer version and the last upgrade record. Schemas 1–3 migrate on load; their original bytes are preserved before the first new save. Same-schema application upgrades also receive durable backups. Normal writes use a temporary file and File.Replace with a rotating .bak. Future schemas or newer producer versions are read-only. See [migration](migration.md).

`ReleaseClient` validates bounded public GitHub release pages, semantic versions and architecture-matching ZIP/checksum pairs. The app owns a timeout-limited HTTP client with redirects disabled. Checks are single-flight, canceled at exit and disabled in preview. No update credentials are accepted or sent. See [updates](updates.md).

## Rendering and resources

The native window uses the pointer monitor's working area and PerMonitorV2. WPF content uses DIPs and logical cells. The wallpaper blur is an approximation of the original composition.

Theme resources cover controls, scrollbars, dialogs and calendar. High contrast overrides effects. Modern app tiles use uniform translucent plates; Classic tiles share a global accent. Explicit per-tile color overrides are a ReStart extension and can be reset; high contrast still wins. Optional Start-surface accent applies to dark mode.

Typed `TextKey` references resolve eight embedded JSON catalogs. Custom labels and OS app names are preserved; default live-tile titles resolve in the selected language. Public/protected APIs require XML documentation; CS1591 is a build error.

## JavaScript boundary

Visible live tiles share four workers, a fresh Jint realm per render, and bounded JSON input/output over current-user-only asynchronous named pipes. The parent enforces a timeout independently of pipe cancellation and owns resource-limited Job Objects. Hiding Start retires workers; successful JSON state survives hiding but resets on reload/exit. Capability-gated host samplers and selected-folder images never expose general file/network APIs. A watched library discovers metadata without executing sources. This is process isolation, not an OS privilege sandbox. See [the API guide](javascript-tiles.md).

The optional data.sources path is owned by the UI host, outside Jint: TileDataTransport shares four bounded asynchronous read slots; TileDataSource coalesces requests, revalidates, backs off and retains the last good snapshot. ScriptDataRuntime adapts named inputs to JSON context and cancels them when hidden. ScriptDataBindings encrypts explicit connections with current-user DPAPI in data-bindings.dat. Import/export never transfers those bindings. No JS-provided runtime URL is accepted and no response triggers shell execution.
