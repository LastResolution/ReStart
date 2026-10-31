# Updates

Settings → Updates checks public GitHub Releases manually or optionally at startup. No API key, account or token is required. Stable releases are selected by default; prereleases can be included explicitly. Checking never downloads or installs an update on its own.

## Guided portable updates

Complete portable packages from v1.6.0 include `ReStart-package.json`, the distribution inventory used for guided updates. Keep this file beside `ReStart.exe`. A developer build, an older package without the inventory, a modified distribution or a protected folder requires the manual route below. The app updates its installed process architecture, including when an x64 app runs on an ARM64 PC.

1. Choose **Check now**. Read the available version and release notes.
2. Choose **Download update**. ReStart fetches the current-architecture ZIP and SHA-256 sidecar from the fixed public repository. You can cancel; the installed application is not changed during preparation.
3. When the package is verified, choose **Restart and update** and accept the confirmation. If Settings has unsaved changes, choose to save, discard or keep editing before proceeding.
4. ReStart saves its current layout, starts a separate copy of its updater, and exits. If a newer app version or layout format has made the saved layout read-only, ReStart preserves that file unchanged and still permits updating. The updater backs up distribution files, replaces the package and starts the new app from the same location. The update commits only after the new app initializes and acknowledges startup.

The installation must be writable without elevation. Atomic file replacement, update metadata replacement and owned-file deletion retry Windows access/sharing locks with increasing delays, for up to ten seconds per operation. Persistent failures still stop the update and trigger rollback. Read-only distribution files are rejected before backup; the updater never changes file attributes or permissions. New package files may not overwrite unrelated local files. Modified distribution-owned files also stop the update; keep custom live tiles in ReStart's local tile library instead of editing bundled installation files. Settings, tile sources and data connections in local app data remain separate from the installed package. Settings writes are deferred until startup is acknowledged, so a failed candidate does not stamp its version into the saved layout.

Installation or startup failure restores the previous distribution and launches it again. A durable journal allows an interrupted transaction to be restored before normal startup. If automatic recovery cannot finish, the app shows repair guidance rather than continuing with a partly replaced distribution. Close ReStart and extract the complete trusted portable ZIP into the installation folder, keeping local settings. Workspaces and backups are under `%LOCALAPPDATA%\ReStart.UpdateCache`; completed update files are removed on a later ordinary startup, while incomplete recovery material is retained. Cleanup removes known update-owned files, never unrelated files.

The helper is copied from the currently installed version. Retry improvements included in v1.13.1 therefore apply after v1.13.1 is installed; an update from an older version still uses that version's helper. Use the manual route if its shorter retry budget cannot outlast a temporary lock.

## Prepared updates and reminders

An explicit download continues if you close Settings. Once verified, a nonmodal ready notice appears at the top right; a hidden launcher may request a Windows notification. **Not now** reduces it to a persistent reminder with a **Restart and update** action. Language/theme changes and reopening Start retain the reminder. The Updates settings section also keeps the ready version and restart action visible.

A small local index associates the preparation with its exact portable installation. Ordinary startup rechecks the installed inventory, newer version, architecture, cached payload and helper hashes before restoring readiness. It neither redownloads nor applies the update automatically. Missing, stale, modified, linked or incomplete workspaces are rejected. A ready update is restored even if optional online startup checks are disabled, because its download was already requested explicitly. A normal exit/relaunch is not installation consent: **Restart and update** and its confirmation remain required.

## Manual updates

Open the [release page](https://github.com/LastResolution/ReStart/releases), download the correct architecture ZIP and optionally verify its SHA-256 sidecar. Exit ReStart through its tray menu, then replace the complete extracted installation, including supporting folders and the distribution inventory. Restart `ReStart.exe`. If the installation location changed, save sign-in startup and Explorer integration again. See [migration and rollback](migration.md) for settings backups.

## Checks and download boundaries

Versions are compared numerically with prerelease precedence. Drafts and releases missing the architecture ZIP or SHA-256 sidecar are excluded. At most five pages of 100 releases are read; incomplete/malformed data is never called up to date. Rate limits, service errors and connection failures have distinct results. Startup checks do not block input or catalog loading. Preview mode disables update networking and installation.

Checks use the fixed `api.github.com` repository endpoint, without Authorization or cookies; API redirects are disabled. An explicitly requested download uses fixed repository/tag/asset paths, then follows at most three HTTPS redirects among GitHub's release download hosts. There are compressed/expanded byte and entry-count limits, bounded checksum/manifest reads, duplicate/path/link rejection, exact file inventories and per-file hashes. The package version, architecture and application executable must agree. A SHA-256 sidecar checks integrity; it is not a code signature or protection against a compromised publisher account.

The startup notice appears once per run when an eligible newer release is found and startup checks are enabled. **Review update** opens **Settings → Updates** inside ReStart, where you can read the version and release notes and choose **Download update**. **Not now** dismisses the notice without downloading. A hidden launcher may also request a Windows notification, subject to Windows settings; clicking it opens the same in-app update choice, including after cold startup. An already open Settings window is reused without saving or discarding its draft. The release-page link remains available as a secondary action for manual downloads. Download and restart always require user action.

Upgrading from private development builds removes ReStart's obsolete saved credential target without reading its value. The updater neither reads nor stores access tokens. Optional live-tile credentials are separate connections and are never used for updates. App lists, layouts, tile inputs and usage history are not attached to update requests.

## Diagnostic information

Settings → About → **Diagnostic information** previews the complete report. **Copy report** copies exactly that text to the clipboard; nothing is sent to GitHub or another server. You choose whether to paste it into an issue.

The report has an explicit field whitelist: report/app/runtime versions, numeric Windows/.NET/WebView2 versions, supported UI language, resolved light/dark/high-contrast mode, layout schema, last check status and up to eight in-memory error categories. It does not read `error.log`, layouts, app catalogs, connection settings or environment variables. Names, local paths, account identifiers, app lists, tile data, credentials, exception messages and stack traces are excluded. Error categories reset with the process. Review additional screenshots, reproduction steps and tile packages separately before sharing them.
