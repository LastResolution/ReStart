# Context menus

ReStart keeps app actions separate from layout editing. Windows supplies the available Shell actions; ReStart translates their labels into the selected UI language. It recognizes both AppsFolder's `OpenFileLocation` and a shortcut's `opencontaining` command rather than matching translated Windows text.

## Where commands appear

| Surface | Main menu | More |
| --- | --- | --- |
| All apps, including expanded Start-menu directories | Open, Pin to Start/Unpin from Start, available Uninstall and optional Pinned/Recent/Frequent destinations and registered Tasks | Available administrator/different-user launch, taskbar pin/unpin, native app settings or the Installed apps shortcut, location, sharing, Store review and properties |
| Recently added app rows | Same app actions as All apps | Also Remove from this list and Clear list; these affect ReStart's discovery list, not installed apps or launch counts |
| Most used app rows | Same app actions as All apps | Also Don't show in this list; the exclusion persists through subsequent launches |
| Search: apps, settings, indexed files and directories | Open, pin/unpin and available Shell actions directly; local items also offer Copy full path | Shell actions are not nested here |
| Pinned tiles, including standalone pins and children of an opened tile folder | Unpin, Resize, available Uninstall and optional Pinned/Recent/Frequent destinations and registered Tasks | Available app or filesystem actions, as in All apps |
| A closed tile folder | Open folder, Unpin and Resize; rename/unpack are in ReStart | No filesystem actions: this is a layout container, not a directory |
| A live tile | Unpin, Resize and tile-specific tools such as Tile Studio, data configuration and reload | Turn live tile on/off |
| A Windows-pinned Jump List destination | Open; right-click for Unpin from this list and, for filesystem items, location/Properties | No More submenu |
| A registered application task | The application's registered action, such as New window | No synthesized app-specific command |
| An application-defined Jump List destination | Open; its own context menu offers available file/folder location and Properties, plus Windows pinning for existing physical destinations | Category membership remains controlled by the publishing app |
| A group heading | Rename, Unpin group from Start and ReStart's tile/layout tools | No application Shell actions |
| Navigation folder shortcuts and Settings, collapsed or expanded | Personalize this list opens ReStart's Start experience settings | No More submenu |
| A Recent/Frequent document inside an app menu | Right-click the document for Open, location, Properties, Remove from this list and Pin to this list when supported | No More submenu |

The **ReStart** submenu contains placement destinations, names, colors and launch options. Layout locks disable placement changes without disabling available Shell actions. In Select tiles mode the app-list/search Open command is disabled, consistently with ordinary app activation in that mode. **Shift+F10** opens the focused item's context menu. Holding **Shift** while opening an app menu also requests extended Shell verbs, including **Run as different user** when available.

## Opening a location

- A catalog app matched to a physical Start-menu shortcut reveals that shortcut, preserving its original app ID, launch arguments and icon. This follows the Windows 10 Start-menu shortcut behavior rather than guessing the executable's installation directory.
- A directly pinned or indexed file, directory, `.lnk` or `.url` reveals that item in its containing folder. Directories use **Open folder location**; other items use **Open file location**.
- Paths containing spaces, commas, non-ASCII characters or shell metacharacters are passed as Shell item ID lists, not interpolated into a command line.
- A packaged app does not gain access to its protected installation directory. A virtual tile folder, live tile, protocol link or missing filesystem target does not invent a local location.
- Availability is rechecked when an action is invoked. If an item disappears after the menu opens, the operation fails visibly rather than invoking a stale command ID.

The location action dismisses ReStart before asking Explorer to reveal the item. Copy full path copies a quoted absolute path for an existing local item; it does not copy shortcut arguments or resolve a shortcut to another executable.

## Commands controlled by Windows

Administrator launch, different-user launch, uninstall and taskbar pin/unpin appear only when Windows supplies their canonical Shell verbs. File sharing and properties use the dedicated filesystem paths described below. ReStart does not invoke registry command strings, patch Explorer, bypass taskbar restrictions or turn an unavailable command into a different operation. In particular, some Windows versions do not expose taskbar pinning to third-party launchers. ReStart currently relies on exposed taskbar verbs rather than claiming support for every TaskbarManager identity, approval and Windows-version combination.

**App settings** is used only when Windows exposes that specific command. Otherwise, recognized installed packages offer **Installed apps…**, which opens the Windows management list, not a falsely labeled individual settings page. Microsoft has confirmed that the public `ms-settings:appsfeatures-app` URI does not reliably target another app from an unpackaged launcher ([documentation issue](https://github.com/MicrosoftDocs/windows-dev-docs/issues/3688)). **Rate and review** is offered only for Store-signed, non-development packages and opens the Store's review page; ReStart does not submit a rating. Windows system packages are excluded from these added package links. The Store still accepts package-family links on Windows 10/11, but Microsoft marks that addressing form as deprecated; no Store product IDs are guessed or hardcoded.

## File sharing and properties

**Share** applies to an explicitly pinned or indexed, accessible file: for example `.py`, `.txt`, an image, PDF or ZIP. ReStart checks read access and obtains a Windows `StorageFile` before advertising the command, then checks again when clicked. It passes that file as a read-only storage item to the desktop Windows Share API, with a live visible owner on the UI thread. It does not execute the file, share its file association, or substitute an application's source shortcut. The full-screen window temporarily gives up its topmost status while Windows owns the share interaction. Opening Share does not send a file; the user chooses a destination and completes its interaction.

Share is hidden for application identities, directories, `.lnk`/`.url` shortcuts, virtual tile folders, live tiles and missing or inaccessible files. This is ReStart's file-sharing scope, not a claim that the Windows API cannot represent folders or links. Ordinary file types have no extension allowlist; the receivers installed on the PC determine which destinations accept a particular type. ReStart requires Windows Share support and its cancellation event (Windows 10 version 2004 or later); on older or unsupported systems the action is hidden without affecting other commands.

**Properties** opens the native filesystem property sheet for an existing file, directory or source shortcut. For a catalog application, it targets the matched physical Start-menu shortcut when one exists. Packaged app IDs, protocols and other virtual targets without a physical source do not expose this command. Shortcut properties describe the shortcut itself, preserving its arguments and target. Start hides before opening the independent sheet on its persistent UI apartment, so a disappearing owner or a short-lived worker does not take the sheet away. A file deleted after opening the menu produces an unavailable-action message instead of operating on another item.

Recent/Frequent lists respect Windows document-history preferences and ReStart's switch. Their loading is independent of the basic app commands, so a slow history provider cannot hold the More menu in its loading state. Closed/reopened menus reject obsolete async results. The documented document-list API supplies Recent/Frequent. Registered **Tasks** and **Pinned** destinations use a separate native adapter; application-defined categories use the same isolated adapter, with up to eight categories and eight destinations per category.

Removing an automatic document destination uses Windows' `IApplicationDestinations` API. It removes that app's Recent/Frequent history entry, not the file, another app's history or a user-pinned Jump List item. ReStart rechecks the selected app and destination before removal. This Windows history change is separate from layout Undo/Redo. Removing a Recently added or Most used app instead changes only ReStart's local activity metadata; the alphabetical app entry and its pins remain available. Clear list clears all discovery dates, including items beyond the three displayed rows. Most used exclusions survive history clearing and subsequent launches, and expire when the app leaves and re-enters the observed catalog.

## Application tasks, custom categories and Windows pins

An app can register **Tasks**, such as opening a new window. ReStart reads the registered labels and invokes the original Shell link or packaged-app activation context; task arguments and working directories are not reconstructed from menu text. Available tasks depend on the app, its installation and Windows. Apps without registered tasks have no Tasks section.

**Application-defined categories** preserve the names registered by each app. Destinations use the original Shell link or app identity; arguments are never reconstructed from labels. Removed categories and changed destinations invalidate old keys. Physical destinations can be pinned to the Windows list when supported; category membership and removal remain controlled by the publishing app. Unknown or unavailable native categories are omitted.

**Pinned** shows that app's Windows Jump List pins. Right-click a Recent/Frequent document and choose **Pin to this list**, or right-click a pinned destination and choose **Unpin from this list**. These changes are shared with Windows, survive restarting ReStart and do not delete files. They are separate from Start tile pinning and layout Undo/Redo. A pinned destination is omitted from Recent/Frequent to avoid duplicate rows. Task entries themselves do not offer pinning.

Start behavior settings independently control document history, registered tasks, application-defined categories and pinned destinations. Hiding tasks or pins does not erase the app's registrations or Windows pins. Turning off Windows document history suppresses automatic history, while an app's registered tasks and retained pins may still be available.

Windows does not offer a documented cross-application API for all of this metadata. The task/pin adapter uses version-dependent Shell COM contracts in a hidden, short-lived copy of ReStart, with bounded messages, two concurrent helpers and cancellation/deadlines. It never patches Explorer, injects a DLL or decodes private Jump List files. A failed/unsupported adapter omits these sections while retaining ordinary app commands. Before execution it rereads the app's native list and checks an opaque identity including the registered target, arguments and working directory; an obsolete menu item cannot invoke a replacement at the same list index. This is fault isolation, not a security sandbox: installed app actions run with the user's normal permissions.

These native paths are exercised on the current Windows 11 development system and Windows CI runners, including an owned task's Unicode arguments/working directory, Windows pin persistence/unpinning and worker failures. They do not establish every Windows 10/11 build, every app or physical ARM64 compatibility. Windows can change these undocumented interfaces.

## Windows 10 coverage audit

A Start menu is not one universal context menu. Windows 10 varied commands by the clicked surface, app type, build and policy. The scope below distinguishes existing commands, added conveniences and remaining differences; it is not a claim that the complete native Start menu has been reproduced.

| Native behavior family | ReStart status |
| --- | --- |
| Pin/unpin, tile Resize, More > live tile on/off, available app actions and Uninstall | Implemented. ReStart additionally offers four sizes for desktop pins; native desktop tiles had fewer size choices. |
| More > Remove from this list / Clear list for Recently added; Don't show in this list for Most used | Implemented against ReStart's activity lists. The lists use ReStart discovery/opt-in launch observations, not Windows' private installation/usage database. |
| Group and tile-folder unpin | Implemented. Group removal uses the native-style Unpin group from Start label, with ReStart's confirmation and layout undo. |
| Navigation shortcut Personalize this list | Implemented; opens ReStart's own shortcut settings rather than modifying Windows Start. |
| Recent/Frequent document opening, location/properties and removal | Implemented through public Windows document-list and destination APIs. |
| Jump List pinned destinations, pin/unpin-to-list and application Tasks such as New window or a private browsing window | Implemented when the installed app has an authoritative identity and Windows exposes the native interfaces. Pins are shared with Windows. ReStart does not guess app-specific commands or decode private history databases. Application-defined categories are included with their registered names and revalidated destination identities. |
| Windows account settings, lock and sign out | Available from the account button's ordinary menu; Switch user hands account selection to Windows, preserving the current local session. ReStart does not duplicate Windows' complete account list. |
| Suggested/promoted app dismissal and Store recommendations | Not applicable: ReStart does not display Microsoft's Start recommendations or ads. |
| Right-click the Windows Start button / Win+X system tools | Owned by Windows, not an app-tile context menu. ReStart does not replace this native system menu. |
| Explorer's complete file context menu, arbitrary third-party extension commands and app-specific Share implementations | Not reproduced. ReStart exposes its documented allowlist and usable native verbs, rather than treating every Explorer extension as a Start action. |

## Reference behavior and verification

Microsoft documents **More > Open file location** for Windows 10 desktop apps and **More > App settings** for UWP apps. ReStart follows that Start-menu placement and exposes available actions directly in its search menu. Windows 10 builds and app types differ; the complete native menu, private Start integrations and every historical layout are not claimed to be identical.

Native verification opens the actual WPF menus on app-list/search, grouped/standalone and expanded tile-folder surfaces, in all eight UI languages and light/dark themes. It checks command routing with intercepted effects, real local Shell capabilities, AppsFolder desktop commands, installed package capabilities when present, extended verbs, locks, pending history and stale requests. Destructive uninstall/elevation actions are not executed by this fixture. See [testing](testing.md).

File-command checks build and read back actual Windows storage-item payloads for six file-name/type cases, including Python, image and PDF names, without choosing a receiver or sending content. They cover unavailable, read-locked and deleted files, virtual targets and the dedicated UI routing. They do not establish compatibility with every installed share receiver or replace visual verification of native Windows dialogs.

History verification opens Recently added/Most used menus, both navigation-rail states and nested document context menus in the same language/theme matrix. A unique owned AppUserModelID checks the actual document-list COM class, adds a fixture link without writing the user's Recent folder, reads it through the production reader and removes only that fixture destination. When Windows document history is disabled, the native check verifies the reader interface without changing the privacy setting; UI routing remains tested with isolated data.

Sources:

- [Microsoft: desktop Windows Share, storage items and source-side lifetime events](https://learn.microsoft.com/en-us/windows/apps/develop/windows-integration/integrate-sharesheet-send)
- [Microsoft: Share cancellation availability](https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.datatransfer.datapackage.sharecanceled)
- [Microsoft: filesystem property sheets](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shobjectproperties)
- [Microsoft: Windows 10 More > Open file location](https://learn.microsoft.com/en-us/visualstudio/ide/user-permissions-and-visual-studio?view=visualstudio#modify-the-shortcut)
- [Microsoft: Windows 10 More > App settings, Uninstall, Share and Rate & Review](https://blogs.windows.com/windows-insider/2018/01/24/announcing-windows-10-insider-preview-build-17083-for-pc/)
- [Microsoft: SHOpenFolderAndSelectItems](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shopenfolderandselectitems)
- [Microsoft: Windows Settings URIs](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings)
- [Microsoft: Store product/review URIs and package-family deprecation](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-store-app)
- [Microsoft: taskbar pinning scope and restrictions](https://learn.microsoft.com/en-us/windows/apps/develop/windows-integration/pin-to-taskbar)
- [Microsoft: Run as different user](https://learn.microsoft.com/en-us/troubleshoot/windows-server/shell-experience/use-run-as-start-app-admin)
- [Microsoft: group and tile-folder unpin introduced in Windows 10 build 18272](https://blogs.windows.com/windows-insider/2018/10/31/announcing-windows-10-insider-preview-build-18272/)
- [Windows 10 Recently added screenshot showing More > Remove from this list / Clear list, in an AIHW installation guide, page 3](https://www.aihw.gov.au/getmedia/95f8859f-0080-4507-bb3a-60dc111da85a/installing-software-for-remote-access-windows10-device.pdf.aspx)
- [Microsoft: Jump List destinations, pinned categories, custom categories and Tasks](https://learn.microsoft.com/en-us/windows/win32/shell/taskbar-extensions)
- [Microsoft: automatic destination removal and pinned/custom category exclusions](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-iapplicationdestinations)
- [Microsoft .NET source: document-list and destination COM class identities](https://source.dot.net/PresentationFramework/MS/Internal/AppModel/ComGuids.cs.html)
- [Windows 10 navigation shortcut context menu, with firsthand screenshots](https://www.tenforums.com/tutorials/2192-add-remove-folders-start-list-windows-10-a.html)

- [Open-Shell: native Jump List interop contracts, pinned source revision](https://github.com/Open-Shell/Open-Shell-Menu/blob/9dfa7fa5c3614de15d0ff4f3eba7edd9e2dcbd24/Src/StartMenu/StartMenuDLL/JumpLists.cpp) (MIT source attribution in `third-party/Open-Shell-interoperability.txt`; no Open-Shell binaries or assets are included)
