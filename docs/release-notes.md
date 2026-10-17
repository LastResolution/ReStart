# ReStart v0.6.0

ReStart's first public release adds a twelve-tile JavaScript gallery, eight UI languages and keyless public update checks, with fixes to placement, startup input and desktop integration.

## Live tiles

Add Orbit, calendar, CPU, memory, focus timer, year progress, clock, countdown, network, battery, storage and photo slideshow from the background/group right-click menu or Settings. Native gradients, rings, bars, sparklines and month dots provide styling examples. Clocks use PC time and time zone; system tiles use aggregate read-only host metrics. Select a local image folder for photos and click to advance. Normal tile clicks stay inside the tile; JS editing is an explicit right-click action.

A watched local tile library discovers package folders automatically; click to add without selecting JSON each time. Any number of tiles share four isolated workers. Click events and JSON state support pause/resume and display modes. New JS/custom tiles are placed independently in free space, preserving existing groups and item coordinates. See the JavaScript guide for capabilities, units, bounds and examples.

Live tiles can choose centered, split, stacked, caption or overlay native layouts using the current size and measured bounds. All twelve examples adapt their content across the four tile sizes. Library reconciliation recovers from coalesced copy notifications without replacing unchanged buttons.

## Placement and input

- Groups use content-sized rectangular footprints with a soft hover surface, drag guides and matching collision previews. Folder expansion keeps its opening anchor and preserves unrelated live-tile column spacing.
- Pencil buttons rename groups and expanded folders inline. Group-to-independent-tile spacing now snaps consistently in either direction.
- Folder/file icons use full Shell icons instead of thumbnail badges. Default file/app pins use free space; selecting a group remains explicit. Operation messages clear after five seconds.
- Vertical scrolling no longer creates a spurious horizontal bar; both axes use themed controls.
- Detached tile baselines align with grouped tiles. Nearby empty cells can target a group, with a labeled destination preview and clear spacing between groups.
- Moving a bottom/rightmost group preserves the drag extent so scroll-offset coercion does not feed back into jumping previews. Identical anchors reuse the preview without another layout pass.
- Opening immediately activates search, including before catalog completion; losing focus closes Start when enabled.
- The taskbar Start-button option identifies Explorer accessibility bounds on primary/secondary taskbars, without guessed screen positions or Explorer patching.
- Explorer **Pin to ReStart** handles files, directories and shortcuts, forwards requests to the running instance and preserves shortcut launch behavior. Windows 11 exposes this under **Show more options**.
- Desktop wallpaper changes, same-file replacements and per-monitor images refresh without saving Settings again.

## Languages and maintenance

English, Japanese, German, French, Spanish, Korean, Simplified Chinese and Traditional Chinese are included with regional system-language resolution. Default live-tile names and captions follow language changes; explicitly customized names are preserved.

Updates use public GitHub Releases without a key or saved token; legacy ReStart credentials are removed without reading their values. About provides version/build and licenses without development-scope UI. Future release notes are English only.

Settings move to **schema 4**. Original schema 1–3 files are retained in durable rollback backups before writing, including the independent-tile alignment migration. Newer schemas/producers remain protected. Layout export excludes installed JS sources and photo-folder bindings; keep the tiles directory separately.

## Install or update

1. Download the x64 or ARM64 ZIP and its SHA-256 sidecar.
2. Exit ReStart through the tray menu and extract the complete ZIP, retaining TileHost, Examples, third-party and notices.
3. Run ReStart.exe. Normal use requires no administrator rights or separate .NET installation.
4. Inspect the migration record in Settings. Refresh enabled startup/Explorer registration if you moved the installation.

Packages are unsigned. Updates are checked in-app and installed manually. The repository's testing and validation records distinguish executed Windows 11 x64 checks from ARM64 cross-builds and hardware-specific input coverage.
