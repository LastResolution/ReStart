<p align="center"><img src="assets/ReStart.png" width="96" alt="ReStart" /></p>
<h1 align="center">ReStart</h1>
<p align="center"><strong>Bring full-screen Start back.</strong><br>Your apps, your layout, your own live tiles.</p>
<p align="center">
  <a href="https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip"><strong>Download for Windows · x64</strong></a> ·
  <a href="https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-arm64.zip">ARM64</a> ·
  <a href="https://github.com/LastResolution/ReStart/releases/latest">Release notes</a> ·
  <a href="README.ja.md">日本語</a>
</p>
<p align="center">
  <a href="https://github.com/LastResolution/ReStart/actions/workflows/ci.yml"><img src="https://github.com/LastResolution/ReStart/actions/workflows/ci.yml/badge.svg" alt="Windows CI" /></a>
  <a href="https://github.com/LastResolution/ReStart/releases/latest"><img src="https://img.shields.io/github/v/release/LastResolution/ReStart?color=0078d4" alt="Latest release" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-0078d4" alt="MIT license" /></a>
</p>

![The complete ReStart desktop launcher with apps, folders and live tiles](docs/images/start.png)

**Miss the Windows 10 full-screen Start menu?** ReStart gives your apps a familiar home on Windows 11 and Windows 10. Pin what you use, arrange the screen your way, and add live tiles that you can write in JavaScript.

**Portable · x64 & ARM64 · Eight languages · No separate .NET installation**

## Start in a minute

1. Download the **[x64 ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip)** for an Intel/AMD PC, or the **[ARM64 ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-arm64.zip)** for Windows on ARM.
2. Extract the **entire ZIP** into a folder you want to keep. Leave `TileHost`, `Examples` and `third-party` beside `ReStart.exe`.
3. Run **ReStart.exe**. Press the **Windows key**, then type an app name to search. **Ctrl+Alt+Space** and the tray menu also open Start.

Use the gear icon for themes, optional sign-in startup, Windows-key/Start-button integration and updates. Normal use needs no administrator rights. Downloads are unsigned; checksums are included on the [release page](https://github.com/LastResolution/ReStart/releases/latest).

## A familiar place to begin

![ReStart opening and closing with apps and live tiles](docs/images/start-motion.gif)

- **Make room for your apps.** Four tile sizes, movable groups, independent tiles and folders that expand in place. Drag, resize and rename without leaving Start.
- **Keep your files close.** Pin a document, directory or shortcut from Explorer's **Pin to ReStart** command.
- **Open and type.** Search your apps immediately, including while the catalog is loading. Reopening returns to your pinned home.
- **Set the mood.** Light, dark, system and high-contrast themes; desktop wallpaper, accent colors and individual tile colors. Opening and closing respect your animation settings.

<details>
<summary><strong>See the light theme</strong></summary>

![The complete ReStart launcher in light mode](docs/images/start-light.png)

</details>

## Give your tiles something to say

See the time, CPU and memory, network traffic, battery, storage and your chosen photos without opening another app. **Twelve JavaScript tiles are included**, with layouts for all four tile sizes.

![Bundled JavaScript live tiles](docs/images/live-tiles.png)

**Orbit clock · Digital clock · Month dots · CPU pulse · Memory flow · Network flow · Battery · Storage · Focus cycle · Year in motion · Countdown · Photo moments**

Right-click the background → **Add live tile** to try one. Click a tile to use its local action, such as pausing a focus timer or advancing a photo.

### Write your own

A tile is a `manifest.json` and a `tile.js`. ReStart supplies PC time, the selected language, tile size, click events and retained state. Optional capabilities provide read-only system metrics or photos chosen through the host.

With `system.metrics` enabled in the manifest, this is a complete CPU display:

```js
function render(ctx) {
  const cpu = ctx.system?.cpuPercent;
  return {
    text: cpu == null ? "—" : Math.round(cpu) + "%",
    kicker: "CPU",
    background: "#12344B", backgroundEnd: "#10202F",
    foreground: "#FFFFFF", accent: "#70E0CB",
    layout: ctx.size === "large" ? "stacked" : "split",
    visual: "ring", progress: (cpu ?? 0) / 100
  };
}
```

ReStart draws the tile natively. Put a package in its own subfolder under `%LOCALAPPDATA%\ReStart\TileLibrary` to discover it automatically, or add it from the right-click menu. Start from the [bundled examples](examples/tiles), follow the [tile guide](docs/javascript-tiles.md), or explore the [API contracts](docs/api-contracts.md).

## Make it yours—and share it

The interface and bundled tiles support **English, Japanese, German, French, Spanish, Korean, Simplified Chinese and Traditional Chinese**.

Have a layout to show, an idea for a tile, or a bug to report? [Open an issue](https://github.com/LastResolution/ReStart/issues/new/choose). Contributions to tiles, translations, accessibility and launcher behavior are welcome. The [contribution guide](CONTRIBUTING.md) explains how to get started.

## Everyday details

<details>
<summary><strong>Keyboard and mouse controls</strong></summary>

| Action | Result |
| --- | --- |
| Bare Windows key / `Ctrl+Alt+Space` | Open or close |
| Type / `Ctrl+F` | Search apps |
| `Enter` in search | Launch the first result |
| `Esc` | Close the current search, folder or overlay, then Start |
| `Tab` / arrows / `Enter` | Navigate and activate |
| Drag a tile / group header | Move it |
| Drop a tile in empty space | Create an independent pin |
| Hold over a tile center for about one second, then drop | Create a tile folder |
| Pencil button / double-click a group or folder name | Rename inline |
| Right-click an app, tile or background | Pin, resize, add tiles and more |
| `Shift` + mouse wheel | Scroll horizontally |

Win+letter shortcuts are forwarded to Windows. Settings can disable Windows-key and taskbar Start-button capture. [Compatibility](docs/fidelity.md) describes the desktop integration and platform coverage.

</details>

<details>
<summary><strong>Settings, privacy and updating</strong></summary>

Settings live in `%LOCALAPPDATA%\ReStart\layout.json`. Saves and upgrades retain backups; see [migration and rollback](docs/migration.md). About displays the running version/build and licenses.

Update checks read public GitHub Releases without an API key. Installation is manual: exit ReStart, then replace the complete extracted installation. If its location changed, save sign-in startup and Explorer integration again. See [updates](docs/updates.md).

No advertising or usage analytics are included. App lists, layouts and photo-folder selections are not uploaded. JavaScript tiles return bounded display data and do not receive general file, network or CLR access; see [security](SECURITY.md).

To uninstall, disable sign-in startup and Explorer integration, exit the tray app and delete the extracted installation. Remove the local data folder only if you also want to delete your settings and custom tiles.

</details>

## Build it yourself

<details>
<summary><strong>Release build and portable packaging commands</strong></summary>

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

</details>

[Architecture](docs/architecture.md) · [Testing](docs/testing.md) · [Releasing](docs/releasing.md) · [Branch protection](docs/github-protection.md)

## About the project

ReStart is an independent, MIT-licensed project inspired by late Windows 10 full-screen Start. It uses reversible desktop integration and does not patch Explorer. It is not affiliated with Microsoft. Read [compatibility](docs/fidelity.md) for implemented behavior, differences and platform testing.

Screenshots and the animation show the real launcher with a demonstration layout, synthetic metrics and generated photo colors. Windows/application names and icons belong to their owners. Dependency and .NET notices ship with the app.

[MIT license](LICENSE) · [Security policy](SECURITY.md) · [Support](SUPPORT.md)
