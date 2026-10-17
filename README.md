<p align="center"><img src="assets/ReStart.png" width="88" alt="ReStart" /></p>
<h1 align="center">ReStart</h1>
<p align="center"><strong>Full-screen Start, again.</strong><br>A familiar home for your apps. A canvas for your live tiles.</p>
<p align="center">
  <a href="https://github.com/LastResolution/ReStart/releases/latest">Download</a> ·
  <a href="docs/javascript-tiles.md">Create a tile</a> ·
  <a href="CONTRIBUTING.md">Contribute</a> ·
  <a href="README.ja.md">日本語</a>
</p>
<p align="center">
  <a href="https://github.com/LastResolution/ReStart/actions/workflows/ci.yml"><img src="https://github.com/LastResolution/ReStart/actions/workflows/ci.yml/badge.svg" alt="Windows CI" /></a>
  <a href="https://github.com/LastResolution/ReStart/releases/latest"><img src="https://img.shields.io/github/v/release/LastResolution/ReStart?color=0078d4" alt="Latest release" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-0078d4" alt="MIT license" /></a>
</p>

![Twelve bundled live tiles rendered by ReStart](docs/images/live-tiles.png)

ReStart brings the Windows 10 full-screen Start experience to an independent Windows desktop launcher. Arrange your apps, open a folder in place, type to search, and make the space yours with JavaScript live tiles.

**Windows 11 / 10 · x64 / ARM64 · Portable · No administrator rights for normal use**

| Your apps, within reach | A layout that stays yours | Live at a glance |
| --- | --- | --- |
| Open with Windows, the taskbar Start button, `Ctrl+Alt+Space` or the tray. Type immediately to search. | Four tile sizes, independent pins, movable groups, inline folders and individual tile colors. | Twelve bundled tiles, native charts, PC time, read-only system metrics and local photo slideshows. |

## Make it your Start

- **Arrange freely.** Move tiles between groups or into empty board space. Groups use content-sized rectangles. A soft hover surface and drag guides reveal the same boundary used for collision previews.
- **Keep folders in place.** Hover over a tile center to create a folder. Opening it pushes only affected neighbors; closing restores their positions.
- **Pin more than apps.** Add files, directories and shortcuts from Explorer's **Pin to ReStart** command. New pins use free space; an explicit group destination is available in ReStart's menus.
- **Add a live tile in a click.** Right-click the background or a group → **Add live tile**, or browse Settings. A watched local tile library discovers your own packages automatically. Native layouts adapt content to all four tile sizes.
- **Choose your look.** Dark, light, system and high-contrast themes, wallpaper, accent colors and per-tile names/colors. Both scroll axes follow the theme.
- **Speak your language.** English, Japanese, German, French, Spanish, Korean, Simplified Chinese and Traditional Chinese. Default live-tile names follow the selected language; custom names stay yours.
- **Upgrade with confidence.** A single instance, optional sign-in startup, migration backups, in-app version/licenses and public update checks without an API key.

The gallery above uses demonstration metrics and generated photo colors. Real tiles read your PC's selected capabilities. Explore [the live tile API and styling examples](docs/javascript-tiles.md).

<details>
<summary>See the launcher and app list</summary>

![Pinned apps in ReStart](docs/images/start.png)

Demonstration data: [app columns](docs/images/app-list.png), [app directories](docs/images/app-folders.png), [group placement](docs/images/group-placement.png), [inline folders](docs/images/inline-folder.png). Normal mode reads this PC's app names and icons.

</details>

ReStart is independent of Microsoft. It uses reversible shell integration; it does not patch Explorer. Legacy UWP tile notifications, the full Windows Search index, Cortana, Jump Lists and usage-based recommendations are outside the current implementation. The [behavior audit](docs/behavior-audit.md) and [validation record](docs/validation.md) document the baseline and executed checks.

## Install and run

1. Download a ZIP from [Releases](https://github.com/LastResolution/ReStart/releases).
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

v1.0 uses **schema 4**. Schemas 1–3 are migrated with their original bytes preserved in `layout.json.schema<version>-<id>.bak` before writing. Same-schema app upgrades also retain a durable backup. Settings → Layout and data shows the recorded source version, schema, date and rollback file. See [migration and rollback](docs/migration.md).

Settings → About displays the running version/build, settings schema and full license texts. Settings → Updates provides manual and optional startup checks against public GitHub releases, without an API key. Updates are downloaded and installed manually. See [updates](docs/updates.md).

No advertising or usage analytics are included. Update checks contact GitHub with the app version; app lists, layouts, photo-folder selections and usage history are not sent. Launching an app or website follows that application's behavior.

To remove ReStart, disable sign-in startup and Explorer integration, exit the tray app and delete the extracted installation. Delete the local data folder only if you also want to remove settings and custom tiles.

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

Work on short feature/fix branches, commit cohesive changes in English, open a PR, require all Windows CI/package checks to pass, then squash merge and delete the branch. Release tags come from main. Future release notes are English.

- [Contributing](CONTRIBUTING.md) · [Architecture](docs/architecture.md) · [API contracts](docs/api-contracts.md)
- [Testing](docs/testing.md) · [Validation record](docs/validation.md) · [Releasing](docs/releasing.md)
- [Research](docs/research.md) · [Fidelity](docs/fidelity.md) · [Changelog](CHANGELOG.md)
- [Release notes](docs/release-notes.md) · [Historical Japanese notes](docs/ja/release-notes.md)

English is the primary documentation language; Japanese counterparts are linked from each document. UI strings use typed resource keys and eight complete language catalogs. Public/protected API comments are checked at build time.

## License and acknowledgments

ReStart is licensed under the [MIT License](LICENSE). Dependency and .NET redistribution notices are included in packages and available in-app. Windows and application names/icons belong to their respective owners. No Microsoft Start binaries, source implementation or research screenshots are redistributed. ReStart is independent of Microsoft.

[Security policy](SECURITY.md) · [Support](SUPPORT.md) · [Branch protection](docs/github-protection.md)
