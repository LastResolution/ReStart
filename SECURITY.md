# Security policy

[日本語](SECURITY.ja.md)

The latest 0.x development release is the supported line. Packages are currently unsigned.

Use Security → Report a vulnerability privately when available. If it is unavailable, ask the repository owner for a private contact route before posting sensitive reproduction details. Ordinary UI bugs belong in Issues.

Layouts contain application names, paths and launch targets. Import only layouts you trust. Importing does not launch their targets or adopt startup, hook or credential settings.

Normal operation does not require elevation. Supported shell commands such as Run as administrator and Uninstall execute only after explicit user selection; preview mode disables them.

Update checks contact the fixed GitHub repository API. A private-release token is stored under the current Windows user in Credential Manager, not in JSON, exports or logs. Use a fine-grained token with Contents: read for this repository. Authentication headers are request-scoped; update HTTP redirects are disabled. There is no automatic executable replacement, advertising or usage analytics.

JavaScript receives JSON only and runs in separate bounded workers. CLR, Node, filesystem, network, DOM and shell-launch APIs are not exposed. These controls improve isolation and responsiveness; they are not a lowered-privilege OS sandbox. See the [tile guide](docs/javascript-tiles.md).

Local error logs may contain paths. Review logs and screenshots before sharing. Do not commit tokens, credentials, signing keys or personal data.
