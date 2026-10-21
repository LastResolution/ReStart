# Testing

[日本語](ja/testing.md)

Run on Windows with the stable SDK in global.json. Exit ReStart when rebuilding its own output, or pass an isolated OutputPath as described in the README.

~~~powershell
dotnet restore ReStart.slnx --locked-mode
dotnet format whitespace ReStart.slnx --verify-no-changes --no-restore
./scripts/check-api-docs.ps1 -SelfTest
./scripts/check-api-docs.ps1
./scripts/check-architecture.ps1
./scripts/check-readmes.ps1
./scripts/test-privacy.ps1
dotnet build ReStart.slnx -c Release --no-restore
dotnet test ReStart.slnx -c Release --no-build --logger trx --results-directory artifacts/test-results
./scripts/smoke.ps1
./scripts/test-navigation.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-navigation.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe -AppFolders
./scripts/test-first-input.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-search-selection.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-fidelity.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-startup.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-appearance.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-motion.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-app-menus.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-release-info.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-placement.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-folder-drops.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-editing.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-scrolling.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-file-pinning.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-bundled-tiles.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-tile-data.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/package.ps1 -Runtime win-x64
./scripts/test-lifecycle.ps1 -Executable artifacts/publish/win-x64/ReStart.exe
~~~

## What fixtures establish

| Fixture | Evidence |
| --- | --- |
| smoke | WPF render, real catalog/icon counts, sanitized tile labels; optional SearchQuery E or packaged Executable |
| navigation | 288 fictional apps at 800/1280/1920/2560 DIPs, occupied column width, heading continuity and eight Hide/Present resets |
| navigation -AppFolders | Inline directory expansion, keyboard/Automation paths, hidden-child search, 1920/3840 widths, themes and initial jump |
| first-input | Delayed catalog and first-character search, reopening/rail focus, native activation, then real foreground events with WPF deactivation disabled: repeated intermediate-window transitions, owned dialogs, hook lifetime and disabled preference |
| motion | Real WPF entrance/exit clocks, staged rows, catalog refresh, input during entrance, rapid reopen, taskbar dismissal, disabled animation and shutdown cleanup |
| search-selection | Native selection/automation, selected Enter target, first-Down advance, single/no-result and list-end bounds, arrows, typing continuation, same-query refresh, removed/new app catalogs, refresh failure/coalescing, preserved pins and eight languages in both themes |
| startup | Delayed catalog, hidden prepared content, splash typeahead/cancellation/coalesced activation, initial photo/script/error frames, warm reopening, eight languages in dark/light/system modes and pixel-cache corruption/expiry/invalidation |
| appearance | Eight languages across five families × dark/light/system modes, actual expanded-rail glyphs and alignment, splash theme resources, extreme RGB text/control/scrollbar contrast, invalid color rejection and ten native family-save/reopen/mode-switch/custom-RGB persistence paths |
| app-menus | Pin/unpin/menu refresh, default/explicit directory destinations with immediate Shell icons, preview command availability, canonical verbs read for real/missing files; no shell verb invocation |
| fidelity | Eight-language settings search/sidebar checks, async indexed-file cancellation and native query, feature switch save/reload, local history, menu categories, Unicode arguments/working directory and shortcut launches through a test-only handler, launch-options editor validation, stationary edge scrolling without saved-coordinate changes |
| release-info | Version, licenses, original migration backup, upgrade record, Eight languages × light/dark settings, global tile and Start accent |
| placement | Independent group anchors, collision preview/cancel, unchanged child cells, standalone footprint/no header, persistence, inline name events and normal/independent folder expansion |
| folder-drops | Twelve real dispatcher-timer cases (independent → group / grouped → folder × normal/edit × same/mixed sizes), 192 source/target-size and grab-position cases, four deadline/edge/drift cases, earlier-neighbor header collisions, persisted identities, one-step undo/redo, cancellation, scrolling, batch grouping and folder-as-root grouping |
| editing | Native selection clicks suppress launches, rigid previews/cancel/commit, one-step undo/redo/unpin, existing/expanded folder moves, parent-child exclusion, real save-lock rollback, eight languages × two themes, Escape and reopen |
| scrolling | Twelve axis/theme cases and 32 stationary-pointer scrolled-group previews without offset feedback or repeated layout |
| bundled-tiles | Fifteen packages, four sizes, local clicks, retained state, photo binding/file handles, custom colors and a real host CPU-to-frame path |
| tile-data | Actual worker rendering for weather/feed/local JSON, feed clicks, file producer updates, DPAPI round-trip/corruption, no imported bindings, saved-secret handling, eight languages × two themes and hide cleanup; no external requests |
| file-pinning | Cold directory pin, single-instance transfers, exact shortcut target, deduplication and rejected missing targets |
| icons | Nine Shell file/folder/shortcut patterns, four sizes × three scales, folder previews and status expiry/replacement |
| lifecycle | Ten preview launches across two copied paths leave one primary process; duplicate smoke returns code 2 |

Fixtures use isolated preview state, disable production hooks/startup/update requests and use temporary local connection files, and output PNG/JSON under artifacts. Placement copies binaries into a fresh directory. The WPF root can exceed the CI desktop's size, and tests verify the requested render width. Inspect images as well as success fields. Native routed/composition events are not physical OS input.

Core tests include semantic release ordering, bounded/invalid feeds, unauthenticated feed/rate/network distinctions, schema 1–3 originals retained through later saves, future producer protection and repeated group collisions without internal tile movement. Data-source tests use injected HTTP handlers and temporary files for selectors, RSS/Atom, response/depth bounds, redirects, credentials, timeouts, cancellation, concurrency, conditional reads and stale-cache backoff. Startup tests use `HKCU/Software/ReStart.Tests/<GUID>`, never the production Run value.

For local coverage, build portable symbols and allow the neutral mapped source paths. Keep coverage reports and symbols outside release packages:

```powershell
dotnet test tests/ReStart.Core.Tests/ReStart.Core.Tests.csproj -c Release -p:DebugType=portable -p:DebugSymbols=true --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeAssembliesWithoutSources=None
```

## Manual coverage still required

| Area | Check |
| --- | --- |
| Monitors | Working area, 100/125/150/200% DPI, portrait and mixed-DPI monitors |
| Dragging | Header/tile drags, overlap/cancel/edge scroll, standalone neighbors, folder extraction, save/reopen |
| Editing | Ctrl+E, click/keyboard selection, batch drag from different grab points, group/folder/open-space destinations, hold-to-folder preview, Ctrl+Z/Y, text-field undo, save/reopen |
| Input | Bare Win then E, Enter, Win+E/D/L/Tab, IME composition, Ctrl+F, Esc, navigation |
| Taskbar | Blank/app/secondary taskbar and thumbnail clicks close Start and preserve the original click; select a window after hovering a multi-window app, repeat after reopening; settings and drags stay open |
| Shell | Win32/Store/link/URL launch; only supported app verbs; missing old pins |
| Settings | All languages/themes, contrast, scale, About/licenses, rollback information |
| Updates | Public checks without an API key, rate limits, connectivity failure, newer compatible release; no implicit installation |
| Startup | Enable/disable, moved installation, actual sign-in, Windows policy override |
| Accessibility | Narrator, focus visibility, touch/long press |

Do not execute power, uninstall or elevation commands as a routine smoke check. Automated events do not establish physical shortcut behavior. See [compatibility](fidelity.md) for platform coverage.
