# Settings migration and rollback

[日本語](ja/migration.md)

Normal data is %LOCALAPPDATA%\ReStart\layout.json; preview data is isolated beside its executable. v0.6 writes schema 4 with producer version and the last upgrade record.

Schemas 1–3 preserve settings, IDs, names, launch targets and tile ownership. Earlier unnamed single-item groups become independent entries. Schema 4 aligns independent tiles to the same visible tile baseline as named groups. Schema-3 independent row anchors subtract the former approximate header offset, rounded to a half-cell; named groups are unchanged. Schemas 1/2 use the original child position directly. Migration is applied once. X anchors and individual colors round-trip. Imported layouts retain this PC's preferences.

Before replacing an older schema, the original bytes are saved as layout.json.schema<version>-<id>.bak. A same-schema app upgrade uses layout.json.upgrade-<id>.bak. These durable copies are separate from the rotating layout.json.bak; failure to preserve the source prevents replacement. Recovery from an older .bak preserves that source too.

Settings → Layout and data shows source/target app and schema versions, UTC date and full backup path. Future schemas or newer producer versions are protected from writing. Do not manually change a schema number to force compatibility.

To roll back: exit ReStart, separately copy current data and tiles, restore the matching older backup to layout.json, then launch that older app. Recheck startup/Explorer registration if the executable moved. JS sources and photo-folder bindings are not part of layout import/export; retain the installed tiles directory separately.

Damaged files are preserved as .corrupt-<timestamp>; a valid rotating backup may be recovered. Future files are not renamed or overwritten. See [testing](testing.md).
