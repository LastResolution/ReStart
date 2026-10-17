# Start behavior audit

[日本語](ja/behavior-audit.md)

Reviewed October 18, 2026 before v0.5. This checklist covers the whole interaction surface rather than only individual bug reports. The baseline is Windows 10; Windows 11 sections on shared documentation pages are excluded. No comparison PC/VM is available.

| Area / reference | ReStart review and evidence |
| --- | --- |
| [Full-screen Start and left view switch](https://blogs.windows.com/windowsexperience/2017/09/11/windows-10-tip-make-start-full-screen/) | Left rail, pinned/all-apps switch and working-area window reviewed. Four-width fixtures and eight reopen resets passed |
| [Tile dimensions and coordinates](https://learn.microsoft.com/en-us/windows-hardware/customize/desktop/customize-start-layout) | Four footprints, explicit gaps and upper-left cells reviewed; packing, collisions and save round-trips tested |
| [Groups and inline naming](https://learn.microsoft.com/en-us/windows/configuration/start/layout) | Group move separated from child edits; preview/commit share geometry. Repeated collision/cancel/save tests retain internal items. Inline commit/cancel/focus-loss paths checked |
| [D&D folder creation](https://blogs.windows.com/windowsexperience/2018/12/03/windows-10-tip-name-your-tile-folders/) | One-second center hover, no Shift requirement. Actual WPF tile hit geometry arms after dwell and resets at edges. Ownership transfer and nested-folder rejection checked |
| Same article's animation | Re-inspected frames showing parent up-chevron, name/children beneath a complete row and lower separator. Removed the extra upper line, marked the open parent and removed the two-row minimum for small children. Expansion does not rewrite root rows |
| [Windows 10 app directory navigation](https://support.microsoft.com/en-us/accessibility/windows/use-a-screen-reader-to-explore-and-navigate-the-start-menu-in-windows) | Inline directories, Space/right/left and ExpandCollapse state reviewed. Hidden children stay searchable; view reset and heading chains verified |
| [Pin/unpin and app shortcuts](https://support.microsoft.com/en-us/windows/experience/personalization/customize-the-windows-start-menu) | App identity includes folder children. Dragging an already pinned app moves that existing tile; emptied source groups are pruned without deleting previously empty user groups. Menus refresh after edits; supported canonical shell verbs read on STA. No claim that shell commands were physically invoked |
| Initial typing and search | New-window text composition while catalog is delayed reproduces the original failure and passes after refresh fix. Reopen, continuation and navigation focus checked |
| Open/close and keyboard | State machines, duplicate launch and preview lifecycle checked. Bare Win/combinations, taskbar click and IME require physical verification; no automation claim substitutes for it |
| [Late theme-aware tiles](https://blogs.windows.com/windows-insider/2020/07/01/announcing-windows-10-insider-preview-build-20161/) | Uniform translucent tiles, transparent list icon plates, global classic accent and dark Start surface checked. Shared settings controls rendered in both languages/themes |
| Lifetime/settings | Single instance across paths, isolated startup registration, migration originals, future-producer read-only, About/licenses and release-check failures verified |

## Explicit extensions

These user-requested choices are not presented as proven native behavior:

- Empty-space drops create an independent tile with no group header or reserved group footprint.
- Groups measure occupied content rather than requiring a 6/8-cell minimum width. The configured columns still govern tile wrapping.
- Half-cell horizontal anchors and horizontal scrolling permit a wider board. Drag runways expand only at the corresponding edge; fitting content has no artificial vertical scrollbar. Shift+wheel scrolls horizontally.
- Tile-local display renaming and individual background colors live under the ReStart submenu. “Use theme color” restores shared behavior. Explicit colors survive saves and override JS colors; high contrast takes precedence.

## Findings and remaining evidence

This review found unnecessary vertical extent from viewport-height plus padding, unconditional drag runway extension, fixed group width/anchors, and an extra folder divider/minimum row. These were corrected and receive geometry/render checks.

Known larger gaps remain: exact animation/composition, Windows Search beyond apps, usage-history sections, Jump Lists, legacy UWP notifications, full accessibility and native taskbar compatibility. See [fidelity](fidelity.md). Physical dragging could not be repeated because the desktop helper failed to identify its foreground process after reacquisition. No Windows 10 side-by-side or ARM64 hardware result is claimed.

Re-run [the relevant fixtures](testing.md) and inspect their images whenever these paths change. Keep source-backed behavior, explicit extensions and unverified physical behavior separate in every release.
