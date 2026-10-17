# Settings migration and rollback

[日本語](ja/migration.md)

Normal data lives at `%LOCALAPPDATA%\ReStart\layout.json`. Preview data is separate beside its executable.

v0.5 writes schema 3, including the producer version and last upgrade record. Schemas 1 and 2 retain preferences, groups, tiles, names, IDs and launch targets while normalizing supported bounds. Earlier unnamed single-item groups migrate to independent entries without group headers, preserving the tile and approximating its previous vertical position on the half-cell grid. Named groups remain groups. New X anchors and explicit individual-color flags round-trip in schema 3. Importing a layout retains this PC's preferences.

Before the first replacement, the original source is copied byte-for-byte to `layout.json.schema1-<id>.bak` or `layout.json.schema2-<id>.bak`. A same-schema app-version change uses `layout.json.upgrade-<id>.bak`. These are durable backups, separate from the rotating `layout.json.bak`. Failure to create the required backup prevents replacement. A recovered legacy `.bak` is preserved before it is replaced too.

The document records the source/target application versions and schemas, UTC upgrade time and backup filename. Settings → Layout and data displays the record and full backup path. Older files with no producer version are labeled legacy/unknown.

Newer schemas are read-only to older builds. v0.5 also protects settings saved by a newer producer version. Builds before this safeguard rely on their schema protection; schema 3 prevents v0.2–0.4 from rewriting the new document.

## Roll back

1. Exit all ReStart processes through the tray.
2. Keep a separate copy of the current layout and custom tile packages.
3. Find the durable backup matching the desired older version/schema.
4. Copy it to `layout.json` in the same data folder.
5. Start the older app. Recheck startup registration if its executable path changed.

Do not rename a schema number manually to force an older app to load it. Import/export does not include JavaScript package sources or Windows Credential Manager secrets.

Damaged files are preserved as `.corrupt-<timestamp>`; a valid rotating backup may be recovered. Future-format files are not renamed or overwritten. See [testing](testing.md) for migration and recovery coverage.
