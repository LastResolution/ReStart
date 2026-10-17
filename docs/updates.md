# Update checks

[日本語](ja/updates.md)

Open Settings → Updates to check published GitHub releases manually, enable startup checks or include prereleases. Current 0.x releases are prereleases, so leave that option enabled to receive them.

ReStart compares semantic versions numerically, ignoring build metadata and respecting prerelease precedence. It excludes drafts and releases without both the selected architecture ZIP and its SHA-256 sidecar. It reads at most five pages of 100 releases; an incomplete or malformed response is not reported as “up to date.”

## Private repository access

Create a fine-grained GitHub token scoped to `LastResolution/ReStart` with **Contents: read**, then save it under GitHub access in Settings. Use an account with repository access and an appropriate expiry. Saving/removing this credential takes effect immediately, independently of the Settings Save button.

Windows Credential Manager stores the token for the current user under `ReStart/GitHub/LastResolution/ReStart`. It is absent from layout JSON, export files and release artifacts. The password field is never prefilled. Remove it using Settings → Updates → Remove saved token.

Without access, GitHub may return 404 for the private repository. The app displays access required, including expired or invalid credentials, separately from rate limits, service/connection failures, invalid data and a current installation.

## Network and installation

Requests go only to the fixed `api.github.com` release endpoint, with the app version and configured authentication. No app list, layout or usage history is uploaded. Authentication is request-scoped and HTTP redirects are disabled. Startup checks run asynchronously and do not block typing or catalog loading.

The app checks only. It opens the fixed repository's release page; it does not run feed URLs, download installers or replace itself. Exit the app, extract the full ZIP and restart to update. SHA-256 sidecars are available for manual verification.

Preview mode disables update networking and production credential changes. Automated tests use synthetic HTTP responses and an isolated Credential Manager target. A private token is required for a live authenticated in-app check; credentials are never embedded in the executable.

API basis: [GitHub releases](https://docs.github.com/en/rest/releases/releases) supports fine-grained Contents: read tokens; [CredWriteW](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credwritew) and [CredReadW](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credreadw) provide Windows credential storage.
