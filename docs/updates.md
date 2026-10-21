# Update checks

Settings → Updates checks public GitHub releases manually or optionally at startup. No API key, account or token is required. Stable releases are selected by default; prereleases can be included explicitly.

Versions are compared numerically with prerelease precedence. Drafts and releases missing the current architecture ZIP or SHA-256 sidecar are excluded. At most five pages of 100 releases are read; incomplete/malformed data is never called up to date. Rate limits, service errors and connection failures are reported separately.

Requests use the fixed api.github.com repository endpoint and app version, without an Authorization header. Redirects are disabled. No layouts, application lists, photo selections or usage history are uploaded. Startup checks do not block input or catalog loading. Preview mode disables update networking.

The checker opens the fixed repository release page. Download, checksum verification and installation are manual: exit the tray app, extract the complete ZIP and restart. No feed-provided executable is launched automatically.

Upgrading from private development builds removes ReStart's obsolete saved credential target without reading its value. The update checker no longer reads, accepts or stores access tokens. Optional live-tile data-source credentials are separate, explicitly configured connections; they are never used for update checks.
