# API and state contracts

[日本語](ja/api-contracts.md)

App, Core and TileHost emit XML documentation. Missing public/protected comments fail the build (CS1591). Describe preconditions, ownership and side effects rather than repeating a member's name.

| Boundary | Contract |
| --- | --- |
| TextCatalog / TextKey | Stable language-independent keys; only app-owned UI text is translated |
| LayoutDocument | Accept supported schemas; bound presentation values and IDs; reject nested folders |
| TileGroup.IsStandalone | Exactly one root tile, no group name/header; malformed multi-item entries become ordinary groups without discarding items |
| TileLayout | Pure cell packing; reserve valid explicit positions before auto placement |
| GroupLayout | Pure bounds and previews; Move alone commits anchors/collision displacement, never child coordinates |
| FolderExpansion | Temporary inline geometry; preserve persisted root rows |
| LayoutEditor | Transfer ownership while preserving IDs; Detach creates an independent entry; no nested folders |
| LayoutStore | Preserve original migration/upgrade bytes before atomic replacement; protect unsupported/newer producers |
| AppCatalog | Shell COM stays on STA threads; share only frozen WPF images; dispatch UI updates |
| AppPinning | One pin per app identity, including folder children; independent tiles cannot accept another app as a group destination |
| AppShellCommands | Read enabled canonical verbs; re-query on invocation; never invoke from preview |
| SingleInstanceGate | Per-user/session identity independent of version/path; the gate owns its activation handle |
| StartIntegration | Short hooks; dispatch WPF work; release registrations on exit |
| StartupRegistration | Update one current-user Run value; preserve value and kind for rollback |
| ReleaseVersion / ReleaseClient | Validate versions and bounded release data; require architecture ZIP plus checksum; distinguish access, rate, network and invalid-data failures |
| ScriptTilePackages | Resolve installed GUIDs; a layout cannot execute an arbitrary JS path |
| JsTileRunner | New realm each render; expose JSON only |
| ScriptTileSession | One request at a time; bounded asynchronous IPC and an independent parent deadline |
| WorkerJob | Memory/process/lifetime limits; no claim of reduced OS privileges |

Screen coordinates use WPF DIPs; persisted tile cells are logical integers; group rows use half-cell steps; JavaScript progress is 0–1. Display-only reflow, preview and folder expansion must not modify persistence.

Changes to schemas or protocols require round-trip and backward-input tests, documented migration/rollback behavior, and an explicit compatibility decision. UI strings must never determine whether a file is safe to overwrite.

## Live-tile host additions (v1.0)

See [the complete JS contract](javascript-tiles.md) for optional capability-gated metrics, opaque image binding, PC time, local click events and validated native visuals. Four workers serve every visible tile. Source/manifest import never copies host-bindings.json. New custom pins are placed in free independent board space.
