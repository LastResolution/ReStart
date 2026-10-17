# Create JavaScript live tiles

[日本語](ja/javascript-tiles.md)

![Clock and countdown previews](images/live-tiles.png)

Settings → JavaScript tiles → Create sample adds a clock and opens `tile.js` in Notepad. Edit and save to update the next render; no compilation is needed. A tile's menu can edit JS/settings, open its directory or reload with fresh state.

To import a package, place `manifest.json` and `tile.js` together and select the manifest. ReStart copies the two files into its data directory; edit that installed copy afterward. Portable packages include `Examples/clock` and `Examples/countdown`.

~~~json
{
  "version": 1,
  "title": "My tile",
  "refreshSeconds": 5,
  "settings": { "caption": "Hello" }
}
~~~

~~~js
function render(ctx) {
  return {
    text: ctx.settings.caption,
    detail: ctx.now,
    badge: "JS",
    background: "#0078D7",
    foreground: "#FFFFFF",
    progress: 0.5,
    state: { count: (ctx.state?.count ?? 0) + 1 }
  };
}
~~~

`render(ctx)` is synchronous and returns JSON-compatible display data. ReStart renders native controls, not HTML or XAML.

| Input | Meaning |
| --- | --- |
| ctx.now | ISO date/time with offset; usable with new Date(ctx.now) |
| ctx.locale | ja-JP or en-US |
| ctx.size | small, medium, wide or large |
| ctx.settings | Manifest settings object |
| ctx.state | State from the last successful result, initially {} |

| Output | Limit |
| --- | --- |
| text | 256 UTF-16 code units |
| detail | 512; hidden for small tiles |
| badge | 16 |
| background / foreground | Empty or #RRGGBB; high contrast overrides |
| progress | Optional number from 0 to 1 |
| state | JSON representation up to 8,192 code units |

An explicit user-selected tile color overrides script foreground/background so text contrast stays readable. Reset the tile to its theme color to let the script control colors again.

Small tiles truncate content. Refresh intervals are 1–3,600 seconds, with at most eight concurrent updates. Disabling live updates stops execution. Hiding Start terminates workers but retains successful JSON state for reopening. Reloading the tile or exiting resets state.

## Runtime and limits

Jint 4.17.0 runs in `ReStart.TileHost.exe`. Scripts receive JSON only. CLR, Node, files, network, DOM, shell launch and host callbacks are unavailable; eval/string function generation and module imports are disabled. There is no fetch and no legacy UWP notification hosting.

Each render uses a fresh engine with limits of 20,000 statements, recursion 32, engine allocations 8 MB and 100 ms. The parent adds a one-second response deadline (four seconds on first startup), terminating overdue workers independently of IPC cancellation. A Windows Job Object limits each worker to 192 MB, prevents child processes and ends it when its parent exits. On errors, the last good frame remains and the tooltip reports the failure; an initial failure shows an error.

Source files are limited to 64 KiB and manifests to 16 KiB. Settings/state JSON is limited to 8,192 code units; IPC input/output to 131,072/16,384. Lengths count .NET UTF-16 units.

Only the two named package files are imported. Layout JSON stores an installed package ID rather than an arbitrary source path. Exported layouts do not contain the script source.

Process/resource isolation protects responsiveness; the worker is not an OS privilege sandbox. Use packages whose contents you trust and inspect. Editing explicitly launches Notepad rather than executing .js through Windows Script Host.

## Files

Normal data: `%LOCALAPPDATA%\ReStart\tiles\<id>\`. Preview data: `preview-data/tiles/<id>/` beside its executable. Keep `TileHost` and `Examples` with ReStart when updating. Only the main executable is registered for sign-in startup.
