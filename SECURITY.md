# Security policy

[日本語](SECURITY.ja.md)

Security fixes target the latest published release. Update earlier versions before reporting; mention if the problem also reproduces on current main. Packages are unsigned.

Report vulnerabilities through [GitHub private vulnerability reporting](https://github.com/LastResolution/ReStart/security/advisories/new), under Security → Report a vulnerability. Include the affected version/build, Windows version, impact, minimal reproduction and any relevant package/manifest. Redact personal paths and credentials. Do not put exploit details in public Issues or PRs. The maintainer will triage privately and coordinate a fix and disclosure. Ordinary UI bugs belong in Issues.

Layouts contain application names, paths and launch targets. Import only layouts you trust. Importing does not launch their targets or adopt startup, hook or credential settings.

Normal operation does not require elevation. Supported shell commands such as Run as administrator and Uninstall execute only after explicit user selection; preview mode disables them.

Update checks use the fixed public GitHub repository API without API keys or Authorization headers; HTTP redirects are disabled. App lists, layouts and photo selections are not uploaded. ReStart no longer reads or stores release tokens and removes only its obsolete credential target without reading the value. There is no automatic executable replacement, advertising or usage analytics. SHA-256 sidecars check download integrity; the distribution is not code-signed.

JavaScript receives JSON only and shares four separate bounded workers. CLR, Node, filesystem, network, DOM and shell-launch APIs are not exposed. Optional capabilities provide aggregate read-only metrics and an opaque image from an explicitly chosen folder. Paths, filenames, process lists and arbitrary file reads are not supplied to scripts. Library discovery validates metadata without executing sources; imports copy only manifest.json and tile.js, never host image bindings. These controls improve isolation and responsiveness; they are not a lowered-privilege OS sandbox. Inspect packages before import. See the [tile guide](docs/javascript-tiles.md).

Explorer pin requests use bounded current-user-only IPC, validate filesystem targets and acknowledge after saving. Integration is per-user and reversible; Explorer is not patched and no DLL is injected. Startup is opt-in.

Local error logs, exports and screenshots may contain paths or app names. Review before sharing. Never commit credentials, signing keys, personal layouts or logs. Report bypasses of package/path/IPC bounds and unintended code execution privately.
