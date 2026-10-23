# Settings migration and rollback

## Version 1.8 placement defaults

Schema 4 is unchanged. The additive `FreeTilePlacement` preference defaults to false: future empty-space drops and pins create unnamed groups. The existing `GroupTilesOnDrop` preference is only active when free placement is enabled. Alt temporarily permits independent empty-space placement; Shift suppresses overlap actions. Neither gesture changes saved preferences.

Loading an existing schema-4 layout does not convert or resize anything. Folder IDs, child IDs, names, sizes, colors, local cells and group anchors are preserved. Existing independent entries remain independent. Folders are not flattened or changed into Small tiles. Moving an existing item later follows the chosen placement mode. The normal same-schema upgrade backup remains available.

## Earlier layouts and rollback

Normal data is %LOCALAPPDATA%\ReStart\layout.json; preview data is isolated beside its executable. v1.0 writes schema 4 with producer version and the last upgrade record.

Schemas 1–3 preserve settings, IDs, names, launch targets and tile ownership. Earlier unnamed single-item groups become independent entries. Schema 4 aligns independent tiles to the same visible tile baseline as named groups. Schema-3 independent row anchors subtract the former approximate header offset, rounded to a half-cell; named groups are unchanged. Schemas 1/2 use the original child position directly. Migration is applied once. X anchors and individual colors round-trip. Imported layouts retain this PC's preferences.

Before replacing an older schema, the original bytes are saved as layout.json.schema<version>-<id>.bak. A same-schema app upgrade uses layout.json.upgrade-<id>.bak. These durable copies are separate from the rotating layout.json.bak; failure to preserve the source prevents replacement. Recovery from an older .bak preserves that source too.

Settings → Layout and data shows source/target app and schema versions, UTC date and full backup path. Future schemas or newer producer versions are protected from writing. Do not manually change a schema number to force compatibility.

To roll back: exit ReStart, separately copy current data and tiles, restore the matching older backup to layout.json, then launch that older app. Recheck startup/Explorer registration if the executable moved. JS sources and photo-folder bindings are not part of layout import/export; retain the installed tiles directory separately.

Damaged files are preserved as .corrupt-<timestamp>; a valid rotating backup may be recovered. Future files are not renamed or overwritten. See [testing](testing.md).

## Version 1.5 additions

The schema remains 4. New preferences are additive: live tile names default on, placement locks default off, and a separate background is selected explicitly. Native tile packages remain supported. Web packages use manifest version 2 and retain generated installed IDs; direct web networking defaults off and imports cannot grant it. A same-schema app upgrade preserves its original backup as described above. A newer producer version remains protected from writes by an older app.

Custom background PNGs, installed tile source/assets and local tile-library drafts live beside the layout but are not embedded in layout exports. Keep these directories when backing up. If the whole data directory is deleted while the process runs, that session refuses to recreate the deleted layout. Restart to build fresh defaults. Missing custom tile source is reported explicitly and can be repaired in Tile Studio; the app does not invent the deleted source or credentials.
