# Security policy

[日本語](SECURITY.ja.md)

Security fixes target the latest published release. Update earlier versions before reporting; mention if the problem also reproduces on current main. Packages are unsigned.

The [Code signing policy](docs/code-signing.md) describes prepared SignPath integration, protected credentials and signature verification. Enrollment is pending; this does not change the unsigned status of existing releases. GitHub build attestations identify build provenance separately from Windows publisher signatures.

Report vulnerabilities through [GitHub private vulnerability reporting](https://github.com/LastResolution/ReStart/security/advisories/new), under Security → Report a vulnerability. Include the affected version/build, Windows version, impact, minimal reproduction and any relevant package/manifest. Redact personal paths and credentials. Do not put exploit details in public Issues or PRs. The maintainer will triage privately and coordinate a fix and disclosure. Ordinary UI bugs belong in Issues.

Layouts contain application names, paths and launch targets. Import only layouts you trust. Importing does not launch their targets or adopt startup, hook or credential settings.

Normal operation does not require elevation. Supported shell commands such as Run as administrator and Uninstall execute only after explicit user selection; preview mode disables them.

Update checks use the fixed public GitHub repository API without API keys or Authorization headers; HTTP redirects are disabled. App lists, layouts and photo selections are not uploaded. ReStart no longer reads or stores release tokens and removes only its obsolete credential target without reading the value. There is no automatic executable replacement, advertising or usage analytics. SHA-256 sidecars check download integrity; the distribution is not code-signed.

JavaScript receives JSON only and shares four separate bounded workers. CLR, Node, direct filesystem/network, DOM and shell-launch APIs are not exposed. Optional capabilities provide aggregate read-only metrics, an opaque image from an explicitly chosen folder and cached data from explicitly configured sources. Library discovery validates metadata without executing sources; imports copy only manifest.json and tile.js, never host image/data bindings. These controls improve isolation and responsiveness; they are not a lowered-privilege OS sandbox. Inspect packages before import. See the [tile guide](docs/javascript-tiles.md).

Data-source access is opt-in per installed package. The host performs bounded GET requests over HTTPS or loopback HTTP, or reads an explicitly selected local file. At most four reads run concurrently, with eight-second deadlines, 256 KiB response limits, bounded JSON selection, XML DTD/entity rejection and retry backoff. Hiding Start cancels pending reads. Cookies and ambient credentials are disabled; redirects stay on the same origin. URLs, selected paths and optional credential headers are encrypted together with current-user Windows DPAPI in data-bindings.dat, separate from layout exports and imported packages. They are never added to script context; selected response values are. DPAPI does not defend against software already running as the same user. Review the data you select and trust the package receiving it.

Configured providers receive the request, its connection metadata and any explicitly configured URL parameters/credential header. No app inventory or layout is attached. The weather sample starts with Berlin coordinates and does not infer location. Provider terms and limits apply. Never publish personal connection settings or credentials in a shared manifest or JS file.

Explorer pin requests use bounded current-user-only IPC, validate filesystem targets and acknowledge after saving. Integration is per-user and reversible; Explorer is not patched and no DLL is injected. Startup is opt-in.

Local error logs, exports and screenshots may contain paths or app names. Review before sharing. Never commit credentials, signing keys, personal layouts or logs. Report bypasses of package/path/IPC bounds and unintended code execution privately.
