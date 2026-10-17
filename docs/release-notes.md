# ReStart v0.5.0

[日本語](https://github.com/LastResolution/ReStart/blob/v0.5.0/docs/ja/release-notes.md)

This private development prerelease improves placement and startup search, and adds the information needed to inspect and maintain an installation.

## Placement and appearance

Empty-board drops now detach independent tiles without headers or a full group footprint. Groups measure their content instead of reserving a fixed minimum width, move at half-cell horizontal anchors and support a wider horizontally scrollable board (Shift+wheel). Drag space grows only when reaching an edge, and unnecessary vertical padding is removed. These flexible placement choices are explicit ReStart extensions.

Empty-board drops now detach independent tiles without headers or a full group footprint. Groups measure their content instead of reserving a fixed minimum width, move at half-cell horizontal anchors and support a wider horizontally scrollable board (Shift+wheel). Drag space grows only when reaching an edge, and unnecessary vertical padding is removed. These flexible placement choices are explicit ReStart extensions.

Small-only groups no longer reserve a medium tile's height. Explicit group collisions exclude trailing spacing, and dragging previews the destinations of displaced groups. Preview and commit share one calculation and preserve child tile coordinates. Unnamed group prompts appear on hover/focus.

Tile-local renaming and background colors are available under right-click → ReStart. Choose a palette/custom #RRGGBB or reset to the theme; text contrast follows the plate. High contrast takes precedence.

The D&D folder path checks a continuous one-second center hover. The original expansion animation was reviewed again: the parent becomes an up-chevron and contents expand beneath a complete row. The extra upper divider and small-child row minimum are removed.

Classic app tiles share the selected accent unless explicitly overridden. The dark Start surface can also use the accent, while modern tiles use uniform translucent backplates. Exact Windows 10 acrylic composition and animation remain approximations.

## Search and app menus

Typing E before the catalog finishes loading now retains the input and refreshes results when apps arrive. Continued input, reopening and navigation focus have dedicated regression checks. Input integration preferences apply before catalog enumeration.

App menus offer Pin to Start / Unpin from Start, including pins inside folders. More and Uninstall appear only when the app exposes the corresponding enabled canonical shell command. Preview disables external commands; elevation and uninstall were not executed during verification.

## Version, licenses, migration and updates

Settings → About shows the running version/build, architecture and full license texts. Layout and data shows the last upgrade and rollback file.

Settings → Updates supports manual and optional startup checks. Private-release access uses a repository-scoped GitHub read token stored in Windows Credential Manager. Authentication, rate-limit and network failures are distinct from “up to date.” Checks do not send app lists, layouts or usage history. Download and installation are manual.

**Settings move to schema 3.** Before replacing schemas 1/2, ReStart preserves their original bytes in a durable versioned backup. Application upgrades within the same schema are also backed up. The last upgrade is recorded in the document. Older builds reject schema 3; follow [rollback instructions](https://github.com/LastResolution/ReStart/blob/v0.5.0/docs/migration.md) before downgrading.

Documentation is now English-first, with Japanese counterparts and both portable readmes.

## Install or update

1. Exit the old app from the tray.
2. Extract the entire architecture-matching ZIP into a new folder; keep TileHost, Examples, third-party and the notices.
3. Start ReStart.exe. Settings remain in your local application-data folder.
4. If the path changed and sign-in startup is enabled, save that setting again to update the registration.
5. Inspect Settings → Layout and data for the migration record and retained backup.

SHA-256 files accompany both ZIPs. Packages are unsigned.

## Verification and limits

Release build: zero warnings/errors; 151 Core tests. Native fixtures cover 31 placement/name/folder checks, cold-start input, app directories/menus, migration, licenses, both languages/themes and accent behavior. CI also packages x64 and ARM64 from clean checkouts.

Physical dragging is still awaiting confirmation after the desktop helper returned `foreground window did not report a process id`. Physical Windows-key/taskbar sequences, full IME/Narrator coverage, mixed DPI, ARM64 hardware and side-by-side Windows 10 22H2 comparison remain unverified. Legacy UWP notifications, full Windows Search, usage history and Jump Lists are not implemented. See [the behavior audit](https://github.com/LastResolution/ReStart/blob/v0.5.0/docs/behavior-audit.md) and [fidelity](https://github.com/LastResolution/ReStart/blob/v0.5.0/docs/fidelity.md).

[README](https://github.com/LastResolution/ReStart/blob/v0.5.0/README.md) · [Changes since v0.4.0](https://github.com/LastResolution/ReStart/compare/v0.4.0...v0.5.0)
