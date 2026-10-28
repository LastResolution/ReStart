# Tablet edition

An optional mode for Windows 2-in-1 tablets (developed on an ASUS ROG Flow Z13, 13.4" 2560×1600 at 200%). When the keyboard is detached (slate posture), ReStart becomes the tablet's Home screen. With the keyboard attached, Windows and ReStart behave exactly as upstream.

## What changes in tablet mode

| Area | Behavior |
| --- | --- |
| Home | ReStart is the layer under the apps, full screen with no taskbar. Start and the Windows key go Home. A Today dashboard sits beside touch-sized app groups, with an always-visible search field. |
| Apps | Ordinary app windows open and stay maximized. Minimizing an app, closing the last one or landing on the desktop shows Home instead. |
| Home bar | A thin strip at the bottom. Swipe up goes Home; swipe up and hold opens the app switcher; swipe sideways switches to the previous/next app; a short swipe shows the dock. |
| App switcher | Live window previews. Tap to switch, flick up to close, and a split button for Split View. |
| Split View | Two apps side by side with a draggable divider that snaps at 1/3, 1/2 and 2/3. |
| Dock | Favorites and recent apps, on Home and over apps (short swipe). Favorites are in `%LOCALAPPDATA%\ReStart\tablet-dock.json`. |
| Control Center | Swipe down from the top-right. It has Wi-Fi, Bluetooth, airplane mode, now playing, brightness, volume, rotation lock, refresh rate, light/dark, performance mode (G-Helper, when installed), battery and shortcuts, plus **Show Desktop**, which gives a temporary Windows desktop session until the next Home. |
| Dashboard tiles | Today (clock), weather (Open-Meteo), now playing (system media session), Home Assistant lights and scenes, battery, and CPU/GPU/RAM. |

The taskbar is hidden only in tablet mode. Its previous state is saved, and it is restored on laptop mode, on exit, and after a crash or kill (a watchdog handles the kill case).

## Settings that stay on your PC

Personal values are never bundled. They are read from `%LOCALAPPDATA%\ReStart\tablet\tablet-settings.json` when the tablet Home layout is built (first run, `ReStart.exe --tablet-command apply-layout`, Reset layout):

```json
{
  "weather": { "latitude": 52.52, "longitude": 13.41, "name": "Home" },
  "homeAssistant": {
    "lights": [ { "entity": "light.living_room", "name": "Living room" } ],
    "scenesTitle": "Living room scenes",
    "scenes": [ { "entity": "scene.living_room_relax", "name": "Relax", "icon": "relax" } ]
  }
}
```

For Home Assistant, tap **Connect** on its tile and paste a long-lived access token (Profile → Security). The address and token are stored per user with DPAPI in `%LOCALAPPDATA%\ReStart\tablet\home-assistant.dat`. They never appear in layouts, tile packages or logs. The tile only acts on the entities listed above.

## Windows settings

- Windows' own edge swipes, such as swipe up for Start or the right edge for notifications, are handled by the shell before any app sees them. Disable them so the home bar and Control Center edge receive the gestures. Use the policy *Computer Configuration → Administrative Templates → Windows Components → Edge UI → Allow edge swipe = Disabled* (`HKLM\SOFTWARE\Policies\Microsoft\Windows\EdgeUI`, `AllowEdgeSwipe=0`), then sign out or restart Explorer.
- Turn off *Optimize taskbar for touch interactions*, because the taskbar is hidden in tablet mode anyway.

## Commands

`ReStart.exe --tablet-command <name> [arg]` sends a command to the running instance. Use it for button bindings and testing:

- **Navigation:** `home`, `switcher`, `control-center`, `dock`, `desktop`.
- **Device controls:** `side-button`, `lock`, `screen-off`.
- **Layout:** `apply-layout`.
- **Diagnostics:** `status`, `nav-dump`.

`ReStart.exe --tablet on|off|auto` forces tablet or laptop mode for testing. The override expires after 20 minutes and is cleared whenever the keyboard is attached or detached.

The log is at `%LOCALAPPDATA%\ReStart\tablet.log`.

## Building

```bash
tools/tablet/build.sh          # cross-compiles win-x64 (ReadyToRun) from Linux or Windows into artifacts/tablet/
```

`tools/tablet/rog.sh` deploys a build to a tablet over SSH and drives it for tests (`ROG_HOST`, default `rog`): deploy, restart, tablet on/off, commands, screenshots and touch injection. `tools/tablet/win/install.ps1` installs a build as a non-elevated logon task.

## Code

Everything lives in `src/ReStart.App/Tablet/`:

| Folder | Contents |
| --- | --- |
| `Core` | Home as the desktop layer, taskbar control, startup |
| `Windowing` | Maximize policy and Home fallbacks |
| `Gestures` | Home bar and edge |
| `Switcher` | App switcher |
| `Split` | Split View |
| `Dock` | Dock |
| `ControlCenter` | Control Center |
| `Home` | Tablet layout, dashboard tiles and their host bridges |

The folder also holds shared foundations: `TabletShell` (the hooks features use to call each other), `TabletMode` (posture), `TabletCommands`, `TabletLog`, `TabletSettings`, `AppWindows` and `Media/MediaSessions`.

Upstream files are touched only where needed, and each such change is marked `// Tablet edition:`.
