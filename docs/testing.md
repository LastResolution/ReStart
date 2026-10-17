# Testing

[日本語](ja/testing.md)

Run on Windows with the stable SDK in global.json. Exit ReStart when rebuilding its own output, or pass an isolated OutputPath as described in the README.

~~~powershell
dotnet restore ReStart.slnx --locked-mode
dotnet format whitespace ReStart.slnx --verify-no-changes --no-restore
dotnet build ReStart.slnx -c Release --no-restore
dotnet test ReStart.slnx -c Release --no-build --logger trx --results-directory artifacts/test-results
./scripts/smoke.ps1
./scripts/test-navigation.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-navigation.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe -AppFolders
./scripts/test-first-input.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-app-menus.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-release-info.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-placement.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/package.ps1 -Runtime win-x64
./scripts/test-lifecycle.ps1 -Executable artifacts/publish/win-x64/ReStart.exe
~~~

## What fixtures establish

| Fixture | Evidence |
| --- | --- |
| smoke | WPF render, real catalog/icon counts, sanitized tile labels; optional SearchQuery E or packaged Executable |
| navigation | 288 fictional apps at 800/1280/1920/2560 DIPs, occupied column width, heading continuity and eight Hide/Present resets |
| navigation -AppFolders | Inline directory expansion, keyboard/Automation paths, hidden-child search, 1920/3840 widths, themes and initial jump |
| first-input | New window with delayed catalog; TextCompositionManager sends E, catalog release produces Edge results, then d continues input; reopening/rail focus preserved |
| app-menus | Pin/unpin/menu refresh, preview command availability, canonical verbs read for a real file and a nonexistent file; no shell verb invocation |
| release-info | Version, licenses, original migration backup, upgrade record, English/Japanese × light/dark settings, global tile and Start accent |
| placement | Independent group anchors, collision preview/cancel, unchanged child cells, standalone footprint/no header, persistence, inline name events and normal/independent folder expansion |
| lifecycle | Ten preview launches across two copied paths leave one primary process; duplicate smoke returns code 2 |

Fixtures use isolated preview state, disable hooks/startup/network/credential writes, and output PNG/JSON under artifacts. Placement copies binaries into a fresh directory. The WPF root can exceed the CI desktop's size, and tests verify the requested render width. Inspect images as well as success fields. Native routed/composition events are not physical OS input.

Core tests include semantic release ordering, bounded/invalid feeds, authentication/rate/network distinctions, schema 1/2 originals retained through later saves, future producer protection and repeated group collisions without internal tile movement. Windows credential tests use a unique temporary target. Startup tests use `HKCU/Software/ReStart.Tests/<GUID>`, never the production Run value.

## Manual coverage still required

| Area | Check |
| --- | --- |
| Monitors | Working area, 100/125/150/200% DPI, portrait and mixed-DPI monitors |
| Dragging | Header/tile drags, overlap/cancel/edge scroll, standalone neighbors, folder extraction, save/reopen |
| Input | Bare Win then E, Enter, Win+E/D/L/Tab, IME composition, Ctrl+F, Esc, navigation |
| Taskbar | Blank/app/secondary taskbar clicks close Start and preserve the original click; settings and drags stay open |
| Shell | Win32/Store/link/URL launch; only supported app verbs; missing old pins |
| Settings | Both languages/themes, contrast, scale, About/licenses, rollback information |
| Updates | User-configured private token, access denial, connectivity failure, newer compatible release; no implicit installation |
| Startup | Enable/disable, moved installation, actual sign-in, Windows policy override |
| Accessibility | Narrator, focus visibility, touch/long press |

Do not execute power, uninstall or elevation commands as a routine smoke check. Respect desktop-helper input restrictions; never treat domain tests as proof of physical shortcut behavior. Current hardware and known gaps are in [validation](validation.md) and [fidelity](fidelity.md).
