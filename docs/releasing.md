# Releases and GitHub workflow

[日本語](ja/releasing.md)

Use short feat/fix/docs/chore branches and cohesive commits. Open a PR, wait for every required CI/package check, squash merge and delete the branch. Keep the repository private. Primary docs/release notes are English; Japanese counterparts accompany them.

## Main protection

GitHub rejected private main protection on October 18, 2026 with HTTP 403: “Upgrade to GitHub Pro or make this repository public to enable this feature.” Main is currently **not server-protected**. Keeping PR discipline does not provide protection against force pushes or deletion.

The intended configuration is committed in `.github/main-protection.json`. After upgrading to a qualifying plan, run:

~~~powershell
./scripts/protect-main.ps1
~~~

It requires PRs, resolved conversations, linear history and strict checks for Windows build/tests and both clean portable jobs, disallows force pushes/deletion, and enforces rules for admins. Reviewer approval count is zero for this single-maintainer repository. The script verifies the returned protection; a plan error must not be treated as success. See [GitHub's requirements](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets). Do not change visibility or purchase a plan without authorization.

## Version and tag

Update Directory.Build.props, both release notes, changelogs, current guidance and validation in a PR. Portable readme versions are substituted from the project version at packaging time.

~~~powershell
git switch main
git pull --ff-only
git tag -a v0.5.0 -m "ReStart v0.5.0"
git push origin v0.5.0
gh run list --workflow release.yml
~~~

The workflow verifies matching tag/project/release-note versions and main ancestry, then builds/tests. Separate clean Windows jobs create self-contained x64 and ARM64 ZIPs with SHA-256 sidecars. Publication waits for both. Current 0.x releases are private prereleases; stable publication requires a deliberate policy change.

Each ordinary PR also runs packaging before any solution restore/build in fresh jobs, preventing missing restore steps from being masked. Actions are pinned to commit SHAs. Ordinary CI has contents:read; only release publication has contents:write and actions:read.

## Payload and verification

Stage from a strict file list in a fresh directory: main EXE, worker EXE, two example packages, six third-party/runtime notice files, LICENSE and English/Japanese readmes (15 files). Include notices from the exact resolved .NET runtime packs. Never include preview data, personal layouts/catalogs, credentials or logs.

Download the actual published assets. Verify both SHA-256 files, PE architecture, file/product versions and the expected payload. Run packaged x64 smoke, critical native fixtures, duplicate-launch and JS examples; ARM64 execution still needs hardware. Read [testing](testing.md) and report gaps honestly.

Packages are unsigned. Never commit signing keys. Existing published tags are immutable. A failed workflow may be rerun against the same tag only if its code has not changed; code fixes require a new version/tag. The failed v0.2.0 tag was retained, with the fix released as v0.2.1.

## Updating installations

Exit the old process, extract the complete ZIP, and restart. Local data remains separate. Schema/app upgrades preserve durable rollback copies before writing; see [migration](migration.md). If the executable path changes, re-save enabled startup registration. The in-app checker opens the release page; download/verification/installation are manual.
