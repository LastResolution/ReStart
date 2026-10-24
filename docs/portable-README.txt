ReStart v@VERSION@
Full-screen Start, again.

Run ReStart.exe. Keep TileHost, Examples, third-party, ReStart-package.json and license notices.
The portable package includes .NET; normal use needs no administrator rights.
Open with Windows key, taskbar Start button, Ctrl+Alt+Space or tray icon.
Settings: left gear. Exit: tray menu. Sign-in startup is optional.

Drag tiles/groups; empty-space drops create unnamed groups by default. Use the
name or edit icon to rename. Hold Alt to place independently, or Shift to
rearrange without folders. Sustained tile overlap creates a folder. Optional
free placement and independent-tile grouping are available in settings. Existing folders
accept additions; one remaining child stays in its folder until moved out.
Select tiles in the sidebar/background menu, or Ctrl+E, selects without opening apps.
Normal tile/group dragging is always available. Drag a selection together;
Move to group collects it into a compact block. Finish selecting leaves the mode.
Ctrl+Z undoes layout edits; Ctrl+Y or Ctrl+Shift+Z redoes. History is session-only.
Type immediately to search. Explorer: Pin to ReStart (Windows 11: Show more options).
Settings -> JavaScript tiles: nineteen HTML/CSS/JavaScript examples. Normal click operates
the tile; use Tile Studio to edit and preview all four sizes, or choose a photo folder.
HTML tiles require Microsoft Edge WebView2 Runtime (installed separately by Microsoft).
Legacy native JS packages remain supported. Tile names are shown by default and
can be hidden in settings; direct web networking requires per-tile permission.
Tiles outside the visible area pause by default. Browser failures receive bounded
automatic recovery; repeated failures pause that tile until Reload. Both features
have switches in JavaScript tile settings.
Host APIs provide PC time, aggregate metrics and opt-in cached Web API/feed/file data.
Configure data sources sets the URL/file and optional credential, encrypted locally.
Weather defaults to Berlin coordinates, not the PC location; see the online tile guide.

Eight UI languages; dark/light/system/high-contrast themes.
Settings -> About: version and licenses. Layout/data: migration and rollback.
Layout/data also creates/restores private full backups including tile packages,
drafts and backgrounds. Connections, credentials, photo-folder bindings and caches
are excluded. Backups contain app names, launch paths and custom source/assets;
keep them private and restore only archives you trust.
Updates: public GitHub checks without API keys. Choose Download update, then
Restart and update after verification; failed installation/startup restores old files.
Guided updates require a complete, unmodified package in a writable folder.
Manual replacement remains available. About -> Diagnostic information previews
a report you can copy; no paths, app lists, tile data, credentials or raw logs.
Data: %LOCALAPPDATA%\ReStart\layout.json; schema 1-3 upgrade to 4 with backups.
Layout export excludes JS sources and photo/data bindings; keep tiles separately.

Remove: disable startup and Explorer integration, exit, delete installation.
Local data remains separately. MIT License.
SHA-256 checksum files cover the complete ZIP.
ReStart is independent of Microsoft. Source, build and implementation details:
https://github.com/LastResolution/ReStart

Tile library: place package folders in %LOCALAPPDATA%\ReStart\TileLibrary; Settings discovers them automatically.
