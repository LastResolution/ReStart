# Private profile backups

**Settings → Layout and data → Create full backup** saves a ZIP containing the current layout, appearance/preferences, referenced installed tile packages, up to 128 tile-library drafts, and the selected custom background. Tile HTML, CSS, JavaScript and supported local assets are included. The original source folders selected outside ReStart are never scanned.

This is a **private backup**, not a public layout-sharing format. It contains application names, launch targets and arguments, and any information written into your custom tile source or assets. Do not publish it without reviewing its contents. Saved data connections and credentials are excluded; this does not remove secrets you manually put in source code.

The repository ignores the default backup filename and its privacy guard rejects profile ZIPs even when renamed. These checks help prevent accidental commits; they do not make an archive suitable for public sharing.

## Excluded resources

- Encrypted data connections and credential headers (`data-bindings.dat`).
- Photo-folder bindings (`host-bindings.json`), slideshow photo collections and external JSON files.
- Logs, application usage history, icon/browser caches, update downloads and unrelated files.
- Per-tile browser networking grants; restored tiles require permission again.

The background copied into ReStart is included; the original image directory is not. Missing referenced packages or backgrounds fail the backup rather than silently producing an incomplete archive. Use a ZIP filename outside the ReStart data directory. A failed export leaves an existing destination archive unchanged.

## Restore

1. Open **Settings → Layout and data → Restore full backup** and select the ZIP.
2. ReStart validates entry counts, compressed/expanded sizes, paths, duplicates, hashes, the layout and tile package bounds before offering confirmation.
3. Confirm the group/tile counts. Restore replaces layout and appearance, including language. Windows-key capture, Start-button capture, Explorer registration and sign-in startup retain the current PC's settings.
4. Reconfigure data connections, photo folders and browser networking if needed.

Global or group layout locks and read-only future layouts block restoration. A backup from a newer app must be restored using a compatible app version. Unsaved settings must be resolved before starting either action.

Restored package, library and background resources receive fresh IDs. Existing resources are never overwritten, and connection files associated with earlier package IDs cannot accidentally become active. Cancellation or an unsuccessful atomic layout save removes only the newly staged resources. The prior layout backup and old resources remain available for recovery; see [migration and rollback](migration.md).

Archives are limited to 4,096 resources, 32 MiB per file and 512 MiB compressed/expanded total. Packages retain their normal source and asset limits. Symlinks, escaping paths, unknown resource roots and credentials/bindings are rejected. A checksum verifies consistency, not the identity or trustworthiness of the archive's author. Import backups only from a source you trust.

## Layout JSON remains available

The existing **Export / Import** actions still handle layout JSON only. They do not embed tile packages or images, and importing JSON keeps the current PC's preferences. Use the full backup actions to move a complete private profile.
