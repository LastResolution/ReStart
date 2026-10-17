# Fidelity and limitations

[日本語](ja/fidelity.md)

The baseline is late Windows 10 full-screen Start. ReStart is an independent implementation. “Implemented” does not mean identical, and automated WPF fixtures do not establish physical input compatibility. No Windows 10 comparison PC/VM is available.

| Area | Status and practical limit |
| --- | --- |
| Full-screen / rail | Uses the pointer monitor's working area; expandable rail, pinned/all-apps views, folders, settings and power actions. Reopening resets search, overlays and scroll to pinned home |
| Composition | Wallpaper blur and tint approximate Acrylic/Reveal; animations and pixel geometry are not matched |
| Themes | Dark/light/system/high contrast; themed settings controls, scrollbars and calendar. Shared classic tile accent, translucent modern tiles and optional dark Start accent; explicit individual colors are a ReStart extension |
| Four tile sizes | Logical 1×1 / 2×2 / 4×2 / 4×4 cells |
| Groups | Independent column/half-cell anchors, inline naming, displaced-group preview and no trailing collision padding. Collision geometry uses a content-sized rectangular footprint, with matching hover/drag boundary guides. Group width follows occupied content; configured 6/8 columns limit wrapping. Half-cell X anchors and horizontal scrolling are explicit extensions; exact native snap/animation behavior is unverified |
| Independent tiles | ReStart extension: empty-board drops detach a tile without a header or full group footprint. Earlier unnamed single-item groups migrate to this form. Microsoft's group-creation guidance does not prove native standalone behavior |
| Tile drag/drop | Cell/ownership/persistence and empty-space routes verified in domain/WPF fixtures. Physical drag re-verification is blocked by the desktop helper error described below |
| Tile folders | About one-second center hover; inline name/content expansion with temporary displacement. Expanded independent folders preserve the root X position; expansion keeps the opening anchor and leaves unrelated live-tile spacing unchanged. Native animation timing remains unverified |
| Catalog / icons | AppsFolder and Start-menu .lnk/.url files; shell launch identities and icon-only images, with fallback glyphs |
| All apps | OS display names, inline directories and initial jump. Columns use available width and preserve heading/first-child chains. Japanese items share an “other” bucket; ambiguous shortcuts may appear twice |
| App search | Immediate text including cold startup; pending queries refresh after enumeration; width/case/kana normalization. No file/settings index, Web or Cortana |
| App menus | Pin/unpin across folder contents; enabled canonical shell verbs for More/Uninstall. Read/availability verified; elevation and uninstall not executed. Jump Lists absent |
| Recent / most used | Not implemented; Windows usage history is not imported |
| Windows key | Bare-key state logic implemented/tested; physical Win+letter forwarding remains unverified |
| Taskbar | Close on taskbar click implemented, forwarding the click. Physical sequences remain unverified. Start-button capture uses recent UI Automation bounds of Explorer Start buttons on primary/secondary taskbars; physical sequences remain unverified |
| Accessibility | WPF peers/names/focus, directory ExpandCollapse and high contrast. Full Narrator, touch and IME coverage pending |
| Live content | Local clock/calendar and twelve interactive JS packages, pooled workers, PC time, metrics and selected-folder photos verified. Legacy UWP tile notifications/background delivery are unavailable |
| Power/account | Commands and confirmations exist; disruptive OS actions were not executed in verification |
| Persistence | Schema-4 migration, durable original backups, rotating backup, corruption preservation and future-producer protection tested |
| Single instance / startup | Duplicate launches verified across paths; current-user Run registration tested in an isolated key. Real sign-in/policy interactions pending |
| About / updates | Version/build, license texts and migration information; bounded public release checks without keys or credential storage; no automatic installer |
| Native Start import | Not implemented; ReStart JSON is the supported interchange format |

## Verification boundaries

The test machine is Windows 11 x64 build 26200. ARM64 packages are cross-built; Windows 10 22H2, ARM64 hardware, mixed DPI and multi-monitor combinations have not been validated.

After selecting and reacquiring the preview window, the desktop helper still returned `foreground window did not report a process id` for physical dragging. Inputs were stopped rather than bypassed. WPF routed-input/composition tests and rendered geometry are useful evidence but do not replace that missing test. Physical Windows-key/taskbar operations were not sent.

## Remaining work

- Compare real Windows 10 22H2 placement, focus and animation once a comparison environment is available.
- Validate shell/input behavior on current taskbar implementations and multiple DPI/monitor configurations.
- Improve Japanese alphabet grouping, usage-based sections, Jump Lists, touch and accessibility.
- Continue hardware validation of the documented host APIs and imported packages; retired Microsoft UWP services are not assumed available.

See [the behavior audit](behavior-audit.md), [testing](testing.md), [validation](validation.md) and [research](research.md). Keep these distinctions in release notes and bug reports.
