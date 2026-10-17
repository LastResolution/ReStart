ReStart v@VERSION@
Full-screen Start, again.
Japanese instructions: README.ja.txt

Run ReStart.exe. No separate .NET install or administrator permission is needed.
Keep TileHost, Examples and third-party with the main executable.
Open with the bare Windows key, Ctrl+Alt+Space or the tray icon.
Settings: gear on the left rail. Exit: ReStart tray menu.
Sign-in startup is optional and initially disabled.

Drag tiles within/between groups. Dropping in empty board space detaches an
independent tile without a group header. Drag headers to move whole groups.
Double-click a group name to edit inline. Hover over a tile center for about
one second before dropping to create a folder; contents open inline.
Right-click an app in All apps to pin it. Type from home to search.

Settings -> About: version/build and license texts.
Settings -> Layout and data: upgrade record and rollback backup.
Settings -> Updates: manual/startup checks. Private releases require a
repository-scoped GitHub Contents:read token saved in Windows Credential Manager.
Checks do not install updates. Exit, extract the complete ZIP and restart.

Data: %LOCALAPPDATA%\ReStart\layout.json
Schema 1/2 upgrade to schema 3 with durable original backups.
Before downgrading, exit and restore a backup matching the older schema.
Keep custom tile sources separately; layout export does not include them.

Remove: disable sign-in startup, remove any saved update token if desired,
exit ReStart and delete its extracted installation. Local data is separate.

This is an unsigned development prerelease and independent implementation.
Legacy UWP notifications, full Windows Search and Jump Lists are unavailable.
Start-button capture is experimental. Physical Win-key/taskbar sequences,
Windows 10 22H2 comparison, ARM64 hardware and mixed DPI remain unverified.

Source, build instructions and limitations:
https://github.com/LastResolution/ReStart
MIT License. Windows/app names and icons belong to their respective owners.
