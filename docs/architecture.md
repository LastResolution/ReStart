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

A schema-3 `TileGroup.IsStandalone` entry stores exactly one independent tile. It renders without a group header/menu and occupies only that tile's width and height. Its tile column supplies the offset inside the board column; its vertical anchor belongs to the board. This is an explicit ReStart extension requested for detached tiles, not a claim about Windows 10's internal model. Ordinary groups measure occupied content; the configured 6/8 cells limit internal wrapping. Group X anchors snap in half-cell steps.

`GroupBoardPanel` uses the same rectangles for measure and arrange. `LayoutEditor.Detach` freezes visible anchors and moves ownership once. `FolderExpansion` adds temporary inline content without changing saved root rows. Expanded independent folders temporarily use the column width for their children while keeping the root tile's horizontal position.

## Catalog and shell

`AppCatalog` enumerates AppsFolder and user/common Start-menu .lnk/.url files on dedicated STA threads. User entries win for equal relative paths. SHGetFileInfo supplies localized display names; directory paths remain available for `AppListTree`.

`AppCatalogLayout` matches explicit AUMIDs, unique target-plus-argument identities, then unambiguous names. It preserves existing shell IDs and keeps ambiguous shortcuts distinct. Icons are obtained with IShellItemImageFactory, frozen for WPF sharing, and their native handles released.

App directories expand inline. Column balancing keeps headings and initial children together. Search uses the flat catalog, including closed directories. Input preferences are applied before catalog loading; completing the catalog reruns any pending query without taking navigation focus.

`AppShellCommands` reads IContextMenu canonical verbs on an STA and displays supported enabled commands. Invocation re-queries the command ID. Preview never invokes them. App launch targets are passed as shell identities or individual arguments, not evaluated as shell command text.

## Lifetime and input

A per-user/session mutex prevents duplicate normal instances across versions and paths. A named event activates the existing instance. Preview has a separate scope. Background duplicate launches do not open the menu.

Low-level hooks run a short Win32 message loop on a dedicated thread. A bare Windows key toggles ReStart; combinations replay the pending Windows-key down. UI work is dispatched. Hooks, hotkeys and workers are released on exit. Native Start-button capture only targets a discoverable Start class; no DLL injection or guessed XAML hit regions are used.

Sign-in startup manages one current-user Run value, reconciles its actual state, and restores it if settings saving fails.

## Persistence, updates and credentials

Schema 3 stores tiles, preferences, independent entries, producer version and the last upgrade record. Schemas 1/2 migrate on load; their original bytes are preserved before the first new save. Same-schema application upgrades also receive durable backups. Normal writes use a temporary file and File.Replace with a rotating .bak. Future schemas or newer producer versions are read-only. See [migration](migration.md).

`ReleaseClient` validates bounded GitHub release pages, semantic versions and architecture-matching ZIP/checksum pairs. The app owns a timeout-limited HTTP client with automatic redirects disabled. Checks are single-flight, canceled at exit, and disabled in preview. Credentials are stored by `ReleaseCredentialStore` in Windows Credential Manager, never in layout JSON. See [updates](updates.md).

## Rendering and resources

The native window uses the pointer monitor's working area and PerMonitorV2. WPF content uses DIPs and logical cells. The wallpaper blur is an approximation of the original composition.

Theme resources cover controls, scrollbars, dialogs and calendar. High contrast overrides effects. Modern app tiles use uniform translucent plates; Classic tiles share a global accent. Explicit per-tile color overrides are a ReStart extension and can be reset; high contrast still wins. Optional Start-surface accent applies to dark mode.

Typed `TextKey` references resolve English/Japanese embedded JSON. Saved user labels and OS app names are preserved. Public/protected APIs require XML documentation; CS1591 is a build error.

## JavaScript boundary

Each live tile has a separate worker, a fresh Jint realm per render, and bounded JSON input/output over current-user-only asynchronous named pipes. The parent enforces a timeout independently of pipe cancellation and owns a resource-limited Job Object. Hiding Start ends workers; successful JSON state survives hiding but resets on reload/exit. This is process isolation, not an OS privilege sandbox. See [the API guide](javascript-tiles.md).
