# Windows 10 full-screen Start research

[日本語](ja/research.md)

Reviewed October 17–18, 2026 (JST). The baseline is late Windows 10 “Use Start full screen,” with classic colored tiles optional. Windows 8's horizontally scrolling Start screen and the whole tablet shell are separate targets.

## Behavior sources

| Primary source | Evidence and use |
| --- | --- |
| [Make Start full screen](https://blogs.windows.com/windowsexperience/2017/09/11/windows-10-tip-make-start-full-screen/) | Full-screen pinned view and all-apps switch on the left rail |
| [Managed Start layout](https://learn.microsoft.com/en-us/windows/configuration/start/layout) | Windows 10 section: tile groups, empty-space drag group creation and inline Name group |
| [Windows 10 OEM layout](https://learn.microsoft.com/en-us/windows-hardware/customize/desktop/customize-start-layout) | 1×1 / 2×2 / 4×2 / 4×4 dimensions and group-local row/column coordinates |
| [Build 10547](https://blogs.windows.com/windows-insider/2015/09/18/announcing-windows-10-insider-preview-build-10547/) | Default three-medium-tile width and optional four |
| [Screen-reader Start navigation](https://support.microsoft.com/en-us/accessibility/windows/use-a-screen-reader-to-explore-and-navigate-the-start-menu-in-windows) | Windows 10 section: app directories, Space expansion and keyboard navigation; distinguish the Windows 11 section |
| [Tile folders](https://blogs.windows.com/windowsexperience/2017/06/05/windows-10-tip-organize-start-files-new-tile-folders/) and [folder naming](https://blogs.windows.com/windowsexperience/2018/12/03/windows-10-tip-name-your-tile-folders/) | Center hover for about a second; expand before naming |
| [Official folder animation](https://blogs.windows.com/wp-content/uploads/prod/sites/2/2018/10/d48e2dc05daefba99b5170de49dc2f1e.gif) | Parent up-chevron, name/children between tile rows, following content pushed downward |
| [Four ways to customize Start](https://blogs.windows.com/windowsexperience/2016/11/07/windows-10-tip-four-ways-to-customize-the-start-menu/) | Global tile color and drag-created groups; no evidence of independent group colors |
| [Build 20161](https://blogs.windows.com/windows-insider/2020/07/01/announcing-windows-10-insider-preview-build-20161/) | Uniform partially transparent plates, removal of app-list icon backplates, dark Start accent behavior |
| [Personalize colors](https://support.microsoft.com/en-US/Windows/Experience/Personalization/personalize-your-colors-in-windows) | Global accent on Start/taskbar in dark/custom modes |

Double-click group naming comes from the user's experience report. Exact snap intervals, displacement animations, catalog matching and column balancing remain ReStart implementation choices until compared on hardware.

Microsoft describes dragging into empty space as creating a group. The user explicitly requested tiles that can remain outside a group, so v0.5 adds independent tiles as a ReStart extension. This is documented rather than presented as proven Windows 10 fidelity.

## Implementation sources

- [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc): short hooks and dedicated message loop.
- [Launching applications](https://learn.microsoft.com/en-us/windows/win32/shell/launch): shell identity/target activation.
- [ExtendedProperty](https://learn.microsoft.com/en-us/windows/win32/shell/shellfolderitem-extendedproperty), [link target](https://learn.microsoft.com/en-us/windows/win32/properties/props-system-link-targetparsingpath), [arguments](https://learn.microsoft.com/en-us/windows/win32/properties/props-system-link-arguments): read link identities without resolving/running them.
- [SHGetFileInfoW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shgetfileinfow): localized display names, with filename fallback on failure/long paths.
- [QueryContextMenu](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-icontextmenu-querycontextmenu), [GetCommandString](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-icontextmenu-getcommandstring), [InvokeCommand](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-icontextmenu-invokecommand): enabled language-independent verbs and fresh IDs on invocation.
- [Run/RunOnce keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys): current-user startup, command length and scheduling limits.
- [Jint 4.17.0](https://github.com/sebastienros/jint/tree/v4.17.0) and [constraints](https://sebastienros.github.io/jint/guide/constraints): bounded JS plus an external process deadline.
- [GitHub Releases API](https://docs.github.com/en/rest/releases/releases): list releases including prereleases and authenticate private repository reads.
- [CredWriteW](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credwritew) / [CredReadW](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credreadw): current-user credential persistence.
- [GitHub rulesets](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets): private-repository plan requirements.

Sources establish behavior and API contracts, not pixel identity. No Windows 10 comparison environment is available. Research images stay in ignored artifacts; distributed screenshots are generated by ReStart using demonstration data.
