namespace ReStart.Core;

/// <summary>Stable, language-independent identifiers for all application-owned UI text.</summary>
public enum TextKey
{
    /// <summary>The backup was restored.</summary>
    RecoveryBackupRestored,
    /// <summary>folder</summary>
    TileFolderSuffix,
    /// <summary>Ctrl+Alt+Space is in use. Open ReStart from the notification area.</summary>
    ErrorHotkeyInUse,
    /// <summary>Put this PC to sleep?</summary>
    ConfirmSleep,
    /// <summary>ReStart — Open with Ctrl+Alt+Space</summary>
    TrayTooltip,
    /// <summary>ReStart encountered an error and will close. Your saved layout is preserved.</summary>
    ErrorUnhandled,
    /// <summary>ReStart settings</summary>
    SettingsTitle,
    /// <summary>Exit ReStart</summary>
    AppExit,
    /// <summary>Open ReStart</summary>
    AppOpen,
    /// <summary>Website URL</summary>
    PromptWebsiteUrl,
    /// <summary>Pin a website</summary>
    PinWebsite,
    /// <summary>Windows shortcuts such as Win+E and Win+D remain available. You can also use Ctrl+Alt+Space or the notification area.</summary>
    SettingsShortcutHelp,
    /// <summary>An independent recreation of Windows 10 full-screen Start. Legacy UWP notifications, Cortana, the full Windows Search index and Explorer internals are not included. Clock and date tiles update locally.</summary>
    AboutCompatibility,
    /// <summary>Requires a native Start button exposed by Explorer. Some Windows 11 taskbar configurations are not supported.</summary>
    SettingsStartButtonHelp,
    /// <summary>Windows settings</summary>
    OpenWindowsSettings,
    /// <summary>Start when I sign in to Windows</summary>
    SettingsAutostart,
    /// <summary>Could not capture the Windows key:</summary>
    ErrorWindowsKeyCapture,
    /// <summary>Open ReStart with a single Windows key press</summary>
    SettingsWindowsKey,
    /// <summary>Enter an http or https URL.</summary>
    ErrorInvalidUrl,
    /// <summary>Other</summary>
    AppListOther,
    /// <summary>Everyday apps</summary>
    SeedEveryday,
    /// <summary>All apps</summary>
    NavAllApps,
    /// <summary>Account</summary>
    NavAccount,
    /// <summary>Change account settings</summary>
    AccountSettings,
    /// <summary>Accent</summary>
    AccentPrefix,
    /// <summary>Accent color</summary>
    SettingsAccent,
    /// <summary>App</summary>
    AppFallback,
    /// <summary>Could not load apps:</summary>
    ErrorCatalogLoad,
    /// <summary>Pin an app</summary>
    PinApp,
    /// <summary>Search apps</summary>
    SearchPlaceholder,
    /// <summary>Loading apps…</summary>
    StatusLoading,
    /// <summary>Import</summary>
    CommonImport,
    /// <summary>File Explorer</summary>
    AppExplorer,
    /// <summary>Export</summary>
    CommonExport,
    /// <summary>Exported. The file includes launch targets and local paths.</summary>
    StatusExported,
    /// <summary>Calendar</summary>
    AppCalendar,
    /// <summary>Calendar tile</summary>
    AddCalendar,
    /// <summary>Cancel</summary>
    CommonCancel,
    /// <summary>Clock</summary>
    AppClock,
    /// <summary>Group:</summary>
    GroupPrefix,
    /// <summary>Group name</summary>
    PromptGroupName,
    /// <summary>Group width</summary>
    SettingsGroupWidth,
    /// <summary>Move to group</summary>
    MoveToGroup,
    /// <summary>Delete group</summary>
    DeleteGroup,
    /// <summary>Move group earlier</summary>
    MoveGroupEarlier,
    /// <summary>Move group later</summary>
    MoveGroupLater,
    /// <summary>Rename group</summary>
    RenameGroup,
    /// <summary>Resize</summary>
    ResizeTile,
    /// <summary>Sign out</summary>
    AccountSignOut,
    /// <summary>Follow system</summary>
    ThemeSystem,
    /// <summary>Shut down</summary>
    PowerShutdown,
    /// <summary>Start</summary>
    NavStart,
    /// <summary>Unpin from Start</summary>
    UnpinTile,
    /// <summary>Sleep</summary>
    PowerSleep,
    /// <summary>Tile name</summary>
    PromptTileName,
    /// <summary>Tile size</summary>
    SettingsTileSize,
    /// <summary>Add tile</summary>
    AddTile,
    /// <summary>Too many tiles.</summary>
    ErrorTooManyTiles,
    /// <summary>Replace the taskbar Start button (experimental)</summary>
    SettingsStartButton,
    /// <summary>Dark</summary>
    ThemeDark,
    /// <summary>Tools</summary>
    SeedTools,
    /// <summary>Theme</summary>
    SettingsTheme,
    /// <summary>Use the desktop wallpaper as the background</summary>
    SettingsWallpaper,
    /// <summary>Documents</summary>
    NavDocuments,
    /// <summary>Pictures</summary>
    NavPictures,
    /// <summary>Choose a file to pin</summary>
    PromptPinFile,
    /// <summary>Choose a folder to pin</summary>
    PromptPinFolder,
    /// <summary>Pinned tiles</summary>
    NavPinned,
    /// <summary>Choose file</summary>
    ChooseFile,
    /// <summary>Pin a file or folder</summary>
    PinFileFolder,
    /// <summary>Photos</summary>
    AppPhotos,
    /// <summary>Folder</summary>
    TileFolder,
    /// <summary>Create or add to folder</summary>
    CreateFolder,
    /// <summary>Nested folders are not supported.</summary>
    ErrorNestedFolder,
    /// <summary>Unpack folder</summary>
    UnpackFolder,
    /// <summary>Choose folder</summary>
    ChooseFolder,
    /// <summary>Close folder</summary>
    CloseFolder,
    /// <summary>Paint</summary>
    AppPaint,
    /// <summary>Expand menu</summary>
    ExpandMenu,
    /// <summary>Notepad</summary>
    AppNotepad,
    /// <summary>Mail</summary>
    AppMail,
    /// <summary>Light</summary>
    ThemeLight,
    /// <summary>Turn live tile off</summary>
    DisableLiveTile,
    /// <summary>Turn live tile on</summary>
    EnableLiveTile,
    /// <summary>The layout exceeds 5 MB.</summary>
    ErrorLayoutTooLarge,
    /// <summary>The layout is empty.</summary>
    ErrorLayoutEmpty,
    /// <summary>Layout and data</summary>
    SettingsLayout,
    /// <summary>Import layout</summary>
    ImportLayout,
    /// <summary>Layout imported.</summary>
    StatusImported,
    /// <summary>Export layout</summary>
    ExportLayout,
    /// <summary>Lock</summary>
    AccountLock,
    /// <summary>No matching apps</summary>
    SearchNoMatches,
    /// <summary>Positions, sizes and folders are saved automatically. Hold Shift and drop a tile onto another to create a folder.</summary>
    SettingsLayoutHelp,
    /// <summary>Medium</summary>
    TileMedium,
    /// <summary>3 medium tiles</summary>
    ColumnsThree,
    /// <summary>4 medium tiles</summary>
    ColumnsFour,
    /// <summary>Save</summary>
    CommonSave,
    /// <summary>The language changes when you save. App names and your custom labels are preserved.</summary>
    SettingsLanguageHelp,
    /// <summary>Could not save:</summary>
    ErrorSave,
    /// <summary>Storage:</summary>
    StoragePrefix,
    /// <summary>Compatibility</summary>
    AboutScope,
    /// <summary>Restart</summary>
    PowerRestart,
    /// <summary>Default layout restored.</summary>
    StatusLayoutReset,
    /// <summary>Restore default layout</summary>
    ResetLayout,
    /// <summary>Close when switching to another app</summary>
    SettingsCloseOnDeactivate,
    /// <summary>Search with part of an app name.</summary>
    SearchHint,
    /// <summary>Rename</summary>
    RenameTile,
    /// <summary>Appearance</summary>
    SettingsAppearance,
    /// <summary>Large</summary>
    TileLarge,
    /// <summary>Small</summary>
    TileSmall,
    /// <summary>About</summary>
    SettingsAbout,
    /// <summary>New group</summary>
    NewGroup,
    /// <summary>Pin to a new group</summary>
    PinNewGroup,
    /// <summary>This layout uses a newer format and is protected from changes.</summary>
    ErrorFutureLayout,
    /// <summary>Native name of Japanese in the language selector.</summary>
    LanguageJapanese,
    /// <summary>Use classic colored tiles</summary>
    SettingsColoredTiles,
    /// <summary>Clock tile</summary>
    AddClock,
    /// <summary>Best match</summary>
    SearchBestMatch,
    /// <summary>Unsupported</summary>
    ErrorUnsupported,
    /// <summary>Search</summary>
    SearchTitle,
    /// <summary>Clear search</summary>
    SearchClear,
    /// <summary>Wide</summary>
    TileWide,
    /// <summary>Restore the default pinned layout?</summary>
    ConfirmReset,
    /// <summary>Replace your current pins? Imported tiles include launch targets. Only use files you trust.</summary>
    ConfirmImport,
    /// <summary>Exit</summary>
    CommonExit,
    /// <summary>Background opacity</summary>
    SettingsOpacity,
    /// <summary>Animate when opening</summary>
    SettingsAnimation,
    /// <summary>Display language</summary>
    SettingsDisplayLanguage,
    /// <summary>Language</summary>
    SettingsLanguage,
    /// <summary>Settings</summary>
    NavSettings,
    /// <summary>Could not read:</summary>
    ErrorRead,
    /// <summary>Could not launch:</summary>
    ErrorLaunch,
    /// <summary>Startup and controls</summary>
    SettingsStartup,
    /// <summary>No launch target is set.</summary>
    ErrorNoTarget,
    /// <summary>No ads or usage reporting. Installed apps and shortcuts are read locally on this PC.</summary>
    SettingsPrivacy,
    /// <summary>Close</summary>
    CommonClose,
    /// <summary>Calculator</summary>
    AppCalculator,
    /// <summary>Power</summary>
    NavPower,
    /// <summary>+ Group</summary>
    AddGroup,
    /// <summary>+ Create a group and pin apps</summary>
    EmptyGroups,
    /// <summary>Choose a letter    × Close</summary>
    AlphabetTitle,
    /// <summary>{0} apps  ·  Type to search  ·  Esc to close</summary>
    StatusCatalogReady,
    /// <summary>Delete “{0}” and its pins? The apps themselves will remain installed.</summary>
    ConfirmDeleteGroup,
    /// <summary>Pin to “{0}”</summary>
    PinToGroup,
    /// <summary>Pinned {0}</summary>
    StatusPinned,
    /// <summary>{0} this PC? Save your work first.</summary>
    ConfirmPower,
    /// <summary>Could not read the layout. The original file is preserved. {0}</summary>
    ErrorLayoutRead,
    /// <summary>Unsupported layout version: {0}</summary>
    ErrorLayoutVersion,
    /// <summary>Add groups or tiles</summary>
    AddMenuTooltip,
    /// <summary>☰     Start</summary>
    RailStart,
    /// <summary>▦     Pinned tiles</summary>
    RailPinned,
    /// <summary>☷     All apps</summary>
    RailAllApps,
    /// <summary>▤     Documents</summary>
    RailDocuments,
    /// <summary>▧     Pictures</summary>
    RailPictures,
    /// <summary>⚙     ReStart settings</summary>
    RailSettings,
    /// <summary>⏻     Power</summary>
    RailPower,
    /// <summary>ReStart — Preview</summary>
    PreviewTitle,
    /// <summary>Use the portable ReStart.exe to register startup.</summary>
    ErrorStartupExecutable,
    /// <summary>The startup path is too long. Move ReStart to a shorter path.</summary>
    ErrorStartupPathLength,
    /// <summary>Preview mode does not change Windows startup registration.</summary>
    PreviewAutostartHelp,
    /// <summary>Starts in the notification area when you sign in. Use the shortcut to open Start.</summary>
    StartupBackgroundHelp,
    /// <summary>Registered startup command:</summary>
    StartupCommandPrefix,
    /// <summary>Could not restore startup registration:</summary>
    ErrorStartupRollback,
    /// <summary>JavaScript tiles</summary>
    SettingsScriptTiles,
    /// <summary>Create a JavaScript tile</summary>
    CreateScriptTile,
    /// <summary>Import a JavaScript tile</summary>
    ImportScriptTile,
    /// <summary>Select the JavaScript tile manifest.json</summary>
    PromptScriptManifest,
    /// <summary>Edit JavaScript</summary>
    EditScript,
    /// <summary>Edit tile settings</summary>
    EditScriptSettings,
    /// <summary>Open tile folder</summary>
    OpenScriptFolder,
    /// <summary>Reload tile</summary>
    ReloadScript,
    /// <summary>Tile error:</summary>
    ErrorScriptPrefix,
    /// <summary>Tile error</summary>
    ErrorScript,
    /// <summary>Up to eight JavaScript tiles can update at once.</summary>
    MaxScriptTiles,
    /// <summary>Create sample</summary>
    CreateScriptSample,
    /// <summary>Tile added. Close settings to see it.</summary>
    StatusScriptAdded,
    /// <summary>Build your own live tiles with JavaScript. Create a sample and edit tile.js in Notepad. Saved changes appear on the next refresh.</summary>
    ScriptAuthoringHelp,
    /// <summary>Scripts receive the time, language, settings and previous state. Network, file, Node.js, DOM and CLR APIs are not exposed. See the tile development guide linked in the README.</summary>
    ScriptCapabilitiesHelp,
    /// <summary>Invalid tile manifest (version 1, title 1–80, refreshSeconds 1–3600, settings JSON ≤8192 characters).</summary>
    ErrorScriptManifestInvalid,
    /// <summary>Invalid tile frame. Check text lengths, #RRGGBB colors, progress 0–1 and state JSON ≤8192 characters.</summary>
    ErrorScriptFrameInvalid,
    /// <summary>Tile protocol size limit exceeded.</summary>
    ErrorScriptProtocolSize,
    /// <summary>Invalid installed tile id.</summary>
    ErrorScriptPackageId,
    /// <summary>Tile package is too large.</summary>
    ErrorScriptPackageSize,
    /// <summary>Missing manifest.</summary>
    ErrorScriptManifestMissing,
    /// <summary>Tile input is too large.</summary>
    ErrorScriptInputSize,
    /// <summary>Tile output exceeds 16 KB.</summary>
    ErrorScriptOutputSize,
    /// <summary>render(ctx) must return an object.</summary>
    ErrorScriptReturnObject,
    /// <summary>Could not start tile worker.</summary>
    ErrorScriptWorkerStart,
    /// <summary>Tile request is too large.</summary>
    ErrorScriptRequestSize,
    /// <summary>Tile worker exited.</summary>
    ErrorScriptWorkerExit,
    /// <summary>Missing tile response.</summary>
    ErrorScriptResponseMissing,
    /// <summary>Missing tile frame.</summary>
    ErrorScriptFrameMissing,
    /// <summary>Tile worker exceeded its deadline.</summary>
    ErrorScriptDeadline,
    /// <summary>Missing request.</summary>
    ErrorScriptRequestMissing,
}
