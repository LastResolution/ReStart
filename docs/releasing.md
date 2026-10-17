# Releases and GitHub workflow

[日本語](ja/releasing.md)

Use short feat/fix/docs/chore branches, cohesive English commits and PRs. Wait for Windows build/tests and both clean portable checks, squash merge and delete the branch. Release notes from v0.6 onward are English only; historical Japanese notes remain archived. General documentation keeps Japanese counterparts.

## Main protection

The intended server configuration is .github/main-ruleset.json. Run ./scripts/protect-main.ps1 and verify its result. It requires PRs, resolved conversations, linear history and three strict checks, enforces admins and prevents force pushes/deletion. Approval count is zero for a single maintainer. A rejected API response is failure, not protection. Private repository plan restrictions previously blocked this; public visibility permits applying the configuration.

## Version and tag

Update Directory.Build.props, docs/release-notes.md, CHANGELOG.md and current guidance in a PR. Package readme versions are substituted at build time. After merge and successful main CI:

```powershell
git switch main
git pull --ff-only
git tag -a v0.6.0 -m "ReStart v0.6.0"
git push origin v0.6.0
gh run list --workflow release.yml
```

The workflow verifies project/tag/English note versions and main ancestry, builds/tests, packages x64 and ARM64 in separate clean Windows jobs, then publishes a stable release for a public repository. Private repositories produce a draft for review. Each ordinary PR also packages from a clean checkout before solution build to catch missing restore steps. Actions are SHA-pinned; only publication has contents:write.

## Payload

Stage a fresh strict payload: main EXE, worker EXE, twelve manifest/JS pairs, six dependency/runtime notice files, LICENSE and two readmes: 35 files. Never include personal settings, logs, catalogs, image bindings or credentials. Runtime notices come from the resolved packs.

Release builds disable debug metadata and map source paths to a neutral prefix. Packaging runs the generic privacy guard over every ZIP entry, including binary UTF-8/UTF-16 strings, and rejects personal Windows paths or credential patterns without printing matched values.

Download actual release assets, verify SHA-256, PE architecture, product version/build provenance and the exact payload. Run packaged x64 smoke, lifecycle and JS fixtures; distinguish ARM64 cross-build from hardware execution. See [testing](testing.md) and [validation](validation.md).

Packages are unsigned. Published tags normally remain immutable. The explicitly requested English commit-history rewrite is a one-time exception; retain the offline bundle and original-to-rewritten mapping, preserve existing binary assets and their original build provenance. Disable tag-triggered release workflows during that maintenance. Failed v0.2.0 had no release and remains a historical tag.

Exit before replacing the complete portable installation. Data is separate, with durable upgrade backups. If the executable moved, refresh startup/Explorer registration. In-app checks open the public release page without a key; installation stays manual.

See [the exact English UI settings](github-protection.md). After public visibility, verify private vulnerability reporting, Dependabot alerts/security updates, secret scanning/push protection and CodeQL default setup. Run the dependency audit and review actual scanning results; configuration alone is not proof of a successful scan.
