# Validation record

[日本語 / historical detail](ja/validation.md)

Environment: Windows 11 x64 build 26200, stable .NET SDK 10.0.113. Records describe executed checks; cross-compilation and automated WPF input are distinguished from hardware interaction.

## v1.0 candidate — October 18, 2026

- Release build: zero warnings/errors; 203 Core tests passed locally.
- All twelve JS packages render eight languages, four sizes, available/missing host data and local click states. A sixteen-request worker-pool test verifies reuse beyond the previous eight-tile limit and cancellation during disposal.
- Rendered native gallery covers twelve packages/four sizes, timer pause, photo advance, state retention, image handles and custom-color precedence. A separate real host path supplies CPU data to a JS frame; gallery screenshots use synthetic metrics and generated solid images.
- Twelve theme/axis fixtures verify no spurious horizontal overflow, independent axis movement and 12-DIP themed scrollbars. Thirty-two stationary-pointer previews from the bottom of a tall board retain the offset and reuse unchanged layout.
- Adding twelve independent widgets at three width/scale combinations preserves every existing visible group rectangle and child cell.
- Placement: twenty-three placement, six inline-name, sixteen folder and six scale/baseline checks. Rectangular group collision and hit testing work in either child order; reversed-order folder expansion leaves adjacent live-tile columns unchanged. First input: six cold/search/navigation checks, ten native foreground activations and one focus-loss close check.
- Release info: sixteen language/theme settings views, three migration, three accent, five individual-color and four wallpaper checks. Filesystem pinning covers cold folders, IPC, shortcuts, duplicate identity and invalid targets.
- Canonical commit-history cleanup, repository publication/protection, final CI and published asset checks are maintenance actions recorded separately; local results alone do not establish their outcome.

- Native Shell icon verification: nine filesystem/shortcut patterns, four sizes and three scales (108 rendered checks), four folder-preview icons and two status-expiry/replacement checks. Folder icons occupy 56 of 64 source pixels horizontally. Both scrollbar thumbs have matching six-DIP visual thickness.
- Context-menu gallery and eight language-switch checks verify default titles, rendered captions and accessibility names; explicitly renamed titles are retained.
- App menus: seven pin/destination checks and four shell-availability/read checks, including immediate folder icons for default and explicitly grouped pins. The privacy guard accepts clean staged content and rejects generic token/path patterns and private author/committer addresses using an isolated index.

## v0.5 candidate — October 18, 2026

- Release build: zero warnings/errors; 151 Core tests passed locally.
- Placement fixture: 17 placement, four inline naming and ten folder checks. Independent tiles have no group header/menu or reserved group width. Expanded independent folders retain their root X position.
- Repeated group collision/preview/normalization/JSON round-trips at two widths/scales retain every internal item position. Canceling a preview does not mutate saved data.
- Release information fixture: version/schema/licenses, four language/theme settings cases, three migration checks, three accent checks and five individual-color checks.
- First-input regression was reproduced before the fix: E reached the TextBox while enumeration was pending, but the resulting catalog never refreshed it. The fixed six-case fixture uses text composition instead of assigning the query.
- Update-feed mocks cover version/channel/architecture selection, pagination/limits, credentials and failure distinctions. Credential tests use an isolated current-user target; they do not configure the production token.
- Schemas 1/2 and same-schema app upgrades retain original bytes in durable backups; newer producers are read-only.
- Physical drag re-verification failed twice with `foreground window did not report a process id` after window reacquisition. Inputs were stopped. The result remains unverified.
- Server-side private-main protection was retried and rejected with HTTP 403 requiring GitHub Pro or public visibility. The repository remains private; PR/CI policy is observed but not server-enforced.
- The whole-interaction [behavior audit](behavior-audit.md) also caught unnecessary vertical extent, fixed group width/anchors and folder divider/minimum-row differences. Fitting-board/no-scroll and actual horizontal-scroll checks pass.
- Final PR CI and tag-package results are recorded in GitHub Actions and the release. Do not infer their outcome from local checks.

## Earlier releases

| Version | Executed evidence |
| --- | --- |
| 0.1 | Initial Windows build, 36 Core tests, app catalog/icons, WPF previews, settings/search/resize checks, x64 packaged smoke, ARM64 cross-build |
| 0.2 development | Themes/languages, centered buttons, single-instance/startup registration, JS worker limits/state/timeouts, localization keys/XML comments; test count grew to 73 |
| 0.2.0 | Tag packaging failed on a clean checkout due to missing RID-neutral restore. No release was published; the tag was retained |
| 0.2.1 | Explicit neutral restore fixed clean packaging. Fresh-checkout x64/ARM CI, checksums and x64 portable/lifecycle/JS checks |
| 0.2.2 | Four-width app-list render and eight home resets; 73 tests |
| 0.3.0 | Schema-2 anchors/migration, inline name/folder behavior, 91 tests, 17 native fixture checks; published x64/ARM payloads verified |
| 0.4.0 | App directory hierarchy, shell identity matching and 13 native directory checks; 105 tests; private prerelease published |
| Post-0.4 | First-input fix (#18), app menus (#19) and group preview/spacing (#20) merged after CI. No separate 0.4.1 release; changes ship in 0.5 |

The real catalog has been read successfully; private app names, paths, layouts and icons are not included in this record or committed artifacts. Research images are not redistributed. Detailed Japanese historical notes are retained in the linked counterpart.

## Unverified boundaries

Windows 10 22H2 side-by-side comparison is unavailable. ARM64 hardware, mixed DPI, multiple monitors, physical Windows-key/taskbar sequences, complete IME/Narrator paths and actual re-sign-in remain pending. Elevation, uninstall and power operations were not run. Consult [fidelity](fidelity.md) before making compatibility claims.

Library discovery validates metadata without running sources; a native watcher fixture detects a newly copied package and click-adds it without changing existing group rectangles.

Responsive frames exercise centered/split/stacked/caption layouts, compact values and a native four-size comparison, including a two-line small clock. Local portable inspection caught and removed a stale dependency PE debug path; packaging now inspects every entry for generic credential and user-path patterns.
