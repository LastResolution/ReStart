<p align="center"><img src="assets/ReStart.png" width="88" alt="ReStart" /></p>

<h1 align="center">ReStart</h1>
<p align="center">Full-screen Start, again.</p>

[English](README.md) | [日本語](README.ja.md)

[![CI](https://github.com/LastResolution/ReStart/actions/workflows/ci.yml/badge.svg)](https://github.com/LastResolution/ReStart/actions/workflows/ci.yml) · Windows 11 / 10 · .NET 10 · [MIT](LICENSE)

ReStart recreates the Windows 10 full-screen Start experience as an independent Windows desktop launcher. It provides a left navigation rail, pinned tiles, inline tile folders, app search and an expandable all-apps list.

**v0.5 is a private development prerelease. Full Windows 10 compatibility has not been achieved or verified.** See [the behavior audit](docs/behavior-audit.md) and [fidelity and limitations](docs/fidelity.md) for the distinction between implementation, automated verification and physical-device testing.

![Pinned tiles](docs/images/start.png)

These are ReStart screenshots with demonstration data. Normal mode reads this PC's app names and icons. Additional previews show [app columns](docs/images/app-list.png), [app directories](docs/images/app-folders.png), [group placement](docs/images/group-placement.png) and [inline folders](docs/images/inline-folder.png).

## Features

- Full-screen tiles within the monitor's working area, keeping the taskbar available.
- Open with a bare Windows key, `Ctrl+Alt+Space` or the tray icon. Reopening returns to the pinned home view.
- Four tile sizes; saved cells, content-sized groups and independent tiles. Empty-board drops detach tiles without group headers. Groups move on half-cell horizontal anchors; wide boards scroll horizontally, including Shift+wheel.
- Inline group naming with double-click / Enter / F2. Folder creation by hovering one tile over another for about one second; contents expand between tile rows.
- Group collision previews show displaced groups. Small-only groups avoid an artificial medium-tile minimum height, and explicit collision bounds exclude trailing spacing.
- OS-provided app names, icons and Start-menu directories. Closed directories remain searchable. Stable AUMIDs and link targets/arguments distinguish same-named shortcuts.
- Type immediately to search, including before catalog loading completes. Pending queries refresh automatically. Search normalizes width, case and kana.
- Pin / unpin apps and display supported shell commands under More. Extra layout operations are under ReStart.
- English / Japanese / system language, dark / light / system theme and high contrast.
- Uniform translucent modern tiles, optional dark Start accent surfaces and a shared classic accent. Individual tile colors and display names are available under right-click → ReStart; colors can be reset to the theme.
- Custom JavaScript live tiles with a separate worker, examples and a [development guide](docs/javascript-tiles.md).
- Single instance per user/session, optional sign-in startup, atomic settings, durable migration backups and layout import/export.
- In-app version/build information, license texts, migration information and authenticated private-release checks.

Legacy UWP tile notifications, the full Windows Search index, Cortana, Jump Lists and usage-based recommendations remain outside the implemented scope. Native taskbar Start-button interception is experimental and depends on Explorer's implementation.

## Install and run

1. Download a ZIP from [Releases](https://github.com/LastResolution/ReStart/releases). Repository access is required while it is private.
2. Choose `ReStart-win-x64.zip` for x64 or `ReStart-win-arm64.zip` for ARM64.
3. Exit any running ReStart from its tray menu. Extract the entire ZIP, retaining `TileHost`, `Examples`, `third-party` and the notices.
4. Run `ReStart.exe`. No separate .NET installation or administrator permission is required for normal operation.
5. Open Settings from the gear icon. If you moved the installation and use sign-in startup, save that setting again to update the registered path.

Packages are unsigned. Windows 11 x64 is the hardware verification target; Windows 10 22H2, ARM64 hardware, mixed DPI and multiple-monitor combinations require further testing.

## Controls

| Action | Result |
| --- | --- |
| Bare Windows key / `Ctrl+Alt+Space` | Open or close |
| Type / `Ctrl+F` | Search apps from the home view |
| `Enter` in search | Launch the first result |
| `Esc` | Close the current search, folder or overlay, then Start |
| `Tab` / arrows / `Enter` | Navigate and activate |
| `↓` in idle search | Enter the app list or tile grid |
| Click / `Space` on an app directory | Expand or collapse; `→` expands and `←` collapses |
| Alphanumeric key while an app row is focused | Jump to a visible initial; `Ctrl+F` opens search |
| Drag a tile | Move within or between groups |
| Drop into empty space | Detach an independent tile at that position |
| Hover over a tile center for about one second, then drop | Create a tile folder |
| Drag a group header | Preview and move the group without changing its tile cells |
| Double-click a group name / `Enter` / `F2` | Edit inline; `Enter` commits, `Esc` cancels |
| Right-click an app or tile | Pin/unpin, resize, supported shell commands and extra layout actions |

Win+letter shortcuts are forwarded to Windows. Physical Windows-key, taskbar and IME combinations still need device-level verification.

## Settings, upgrades and updates

Data lives in `%LOCALAPPDATA%\ReStart\layout.json`. The previous save is `layout.json.bak`; invalid files are preserved and future schemas are protected.

v0.5 uses **schema 3**. Schemas 1 and 2 are migrated with their original bytes preserved in `layout.json.schema<version>-<id>.bak` before writing. Same-schema app upgrades also retain a durable backup. Settings → Layout and data shows the recorded source version, schema, date and rollback file. See [migration and rollback](docs/migration.md).

Settings → About displays the running version/build, settings schema and full license texts. Settings → Updates provides manual and optional startup checks. For private releases, save a fine-grained GitHub token scoped to this repository with **Contents: read**. It is held in Windows Credential Manager, never in layout JSON or exports. Authentication failures and connection errors are distinct from “up to date.” Updates are downloaded and installed manually. See [updates](docs/updates.md).

No advertising or usage analytics are included. Update checks contact GitHub with the app version and configured authentication; app lists, layouts and usage history are not sent. Launching an app or website follows that application's behavior.

To remove ReStart, disable sign-in startup, exit the tray app and delete the extracted installation. Remove the saved release token in Settings first if desired. Delete the local data folder only if you also want to remove settings and custom tiles.

## Build Release locally

Use Windows and the stable [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) selected by `global.json`. Exit the app if its output files are in use.

~~~powershell
git clone https://github.com/LastResolution/ReStart.git
cd ReStart
dotnet restore ReStart.slnx --locked-mode
dotnet build ReStart.slnx -c Release --no-restore
dotnet test ReStart.slnx -c Release --no-build
.\src\ReStart.App\bin\Release\net10.0-windows\ReStart.exe
~~~

This is a framework-dependent developer build. Use isolated preview mode to avoid hooks, real settings, startup registration, update requests and credential changes:

~~~powershell
dotnet run --project src/ReStart.App -c Release -- --preview
./scripts/smoke.ps1
~~~

Create self-contained portable packages and SHA-256 files with:

~~~powershell
./scripts/package.ps1 -Runtime win-x64
# ./scripts/package.ps1 -Runtime win-arm64
~~~

Output: `artifacts/packages/ReStart-win-x64.zip` or `ReStart-win-arm64.zip`. Always distribute and extract the entire ZIP. Generated artifacts and preview data are ignored by Git.

## Development and documentation

Work on short feature/fix branches, commit cohesive changes, open a PR, require all Windows CI/package checks to pass, then squash merge and delete the branch. Release tags come from main. The repository remains private unless explicitly changed.

- [Contributing](CONTRIBUTING.md) · [Architecture](docs/architecture.md) · [API contracts](docs/api-contracts.md)
- [Testing](docs/testing.md) · [Validation record](docs/validation.md) · [Releasing](docs/releasing.md)
- [Research](docs/research.md) · [Fidelity](docs/fidelity.md) · [Changelog](CHANGELOG.md)
- [English release notes](docs/release-notes.md) · [日本語リリースノート](docs/ja/release-notes.md)

English is the primary documentation language; Japanese counterparts are linked from each document. UI strings use typed resource keys and both language catalogs. Public/protected API comments are checked at build time.

## License and acknowledgments

ReStart is licensed under the [MIT License](LICENSE). Dependency and .NET redistribution notices are included in packages and available in-app. Windows and application names/icons belong to their respective owners. No Microsoft Start binaries, source implementation or research screenshots are redistributed. ReStart is independent of Microsoft.
