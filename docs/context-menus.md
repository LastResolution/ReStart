# Context menus

ReStart keeps app actions separate from layout editing. Windows supplies the available Shell actions; ReStart translates their labels into the selected UI language. It recognizes both AppsFolder's `OpenFileLocation` and a shortcut's `opencontaining` command rather than matching translated Windows text.

## Where commands appear

| Surface | Main menu | More |
| --- | --- | --- |
| All apps, including expanded Start-menu directories | Open, Pin to Start/Unpin from Start, available Uninstall and optional Recent/Frequent documents | Available administrator/different-user launch, taskbar pin/unpin, native app settings or the Installed apps shortcut, location, sharing, Store review and properties |
| Search: apps, settings, indexed files and directories | Open, pin/unpin and available Shell actions directly; local items also offer Copy full path | Shell actions are not nested here |
| Pinned tiles, including standalone pins and children of an opened tile folder | Unpin, Resize, available Uninstall and optional Recent/Frequent documents | Available app or filesystem actions, as in All apps |
| A closed tile folder | Open folder, Unpin and Resize; rename/unpack are in ReStart | No filesystem actions: this is a layout container, not a directory |
| A live tile | Unpin, Resize and tile-specific tools such as Tile Studio, data configuration and reload | Turn live tile on/off |

The **ReStart** submenu contains placement destinations, names, colors and launch options. Layout locks disable placement changes without disabling available Shell actions. In Select tiles mode the app-list/search Open command is disabled, consistently with ordinary app activation in that mode. **Shift+F10** opens the focused item's context menu. Holding **Shift** while opening an app menu also requests extended Shell verbs, including **Run as different user** when available.

## Opening a location

- A catalog app matched to a physical Start-menu shortcut reveals that shortcut, preserving its original app ID, launch arguments and icon. This follows the Windows 10 Start-menu shortcut behavior rather than guessing the executable's installation directory.
- A directly pinned or indexed file, directory, `.lnk` or `.url` reveals that item in its containing folder. Directories use **Open folder location**; other items use **Open file location**.
- Paths containing spaces, commas, non-ASCII characters or shell metacharacters are passed as Shell item ID lists, not interpolated into a command line.
- A packaged app does not gain access to its protected installation directory. A virtual tile folder, live tile, protocol link or missing filesystem target does not invent a local location.
- Availability is rechecked when an action is invoked. If an item disappears after the menu opens, the operation fails visibly rather than invoking a stale command ID.

The location action dismisses ReStart before asking Explorer to reveal the item. Copy full path copies a quoted absolute path for an existing local item; it does not copy shortcut arguments or resolve a shortcut to another executable.

## Commands controlled by Windows

Administrator launch, different-user launch, uninstall, properties, taskbar pin/unpin and sharing appear only when a usable Shell verb exists for the selected target. ReStart does not invoke registry command strings, patch Explorer, bypass taskbar restrictions or turn an unavailable command into a different operation. In particular, some Windows versions do not expose taskbar pinning to third-party launchers. ReStart currently relies on exposed taskbar verbs rather than claiming support for every TaskbarManager identity, approval and Windows-version combination.

**App settings** is used only when Windows exposes that specific command. Otherwise, recognized installed packages offer **Installed apps…**, which opens the Windows management list, not a falsely labeled individual settings page. Microsoft has confirmed that the public `ms-settings:appsfeatures-app` URI does not reliably target another app from an unpackaged launcher ([documentation issue](https://github.com/MicrosoftDocs/windows-dev-docs/issues/3688)). **Rate and review** is offered only for Store-signed, non-development packages and opens the Store's review page; ReStart does not submit a rating. Windows system packages are excluded from these added package links. The Store still accepts package-family links on Windows 10/11, but Microsoft marks that addressing form as deprecated; no Store product IDs are guessed or hardcoded. Store-app sharing is not synthesized when the Shell withholds a share verb.

Recent/Frequent lists respect Windows document-history preferences and ReStart's switch. Their loading is independent of the basic app commands, so a slow history provider cannot hold the More menu in its loading state. Closed/reopened menus reject obsolete async results. Public document-list APIs do not expose another app's complete custom/pinned Jump List categories or application task lists.

## Reference behavior and verification

Microsoft documents **More > Open file location** for Windows 10 desktop apps and **More > App settings** for UWP apps. ReStart follows that Start-menu placement and exposes available actions directly in its search menu. Windows 10 builds and app types differ; the complete native menu, private Start integrations and every historical layout are not claimed to be identical.

Native verification opens the actual WPF menus on app-list/search, grouped/standalone and expanded tile-folder surfaces, in all eight UI languages and light/dark themes. It checks command routing with intercepted effects, real local Shell capabilities, AppsFolder desktop commands, installed package capabilities when present, extended verbs, locks, pending history and stale requests. Destructive uninstall/elevation actions are not executed by this fixture. See [testing](testing.md).

Sources:

- [Microsoft: Windows 10 More > Open file location](https://learn.microsoft.com/en-us/visualstudio/ide/user-permissions-and-visual-studio?view=visualstudio#modify-the-shortcut)
- [Microsoft: Windows 10 More > App settings, Uninstall, Share and Rate & Review](https://blogs.windows.com/windows-insider/2018/01/24/announcing-windows-10-insider-preview-build-17083-for-pc/)
- [Microsoft: SHOpenFolderAndSelectItems](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shopenfolderandselectitems)
- [Microsoft: Windows Settings URIs](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings)
- [Microsoft: Store product/review URIs and package-family deprecation](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-store-app)
- [Microsoft: taskbar pinning scope and restrictions](https://learn.microsoft.com/en-us/windows/apps/develop/windows-integration/pin-to-taskbar)
- [Microsoft: Run as different user](https://learn.microsoft.com/en-us/troubleshoot/windows-server/shell-experience/use-run-as-start-app-admin)
