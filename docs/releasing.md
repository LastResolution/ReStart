# Releases

[日本語](ja/releasing.md)

Use a short feature/fix branch, cohesive English commits and a pull request. Wait for **Windows build and tests**, **Clean portable win-x64** and **Clean portable win-arm64**, then squash merge and delete the branch. All release notes are written in English on GitHub Releases.

## Main protection

`.github/main-ruleset.json` requires pull requests, resolved conversations, linear history and the three strict checks, with no bypass actors. It blocks force pushes and deletion. The approval count is zero for a solo maintainer. Apply and verify it with `./scripts/protect-main.ps1`; an API error is a failed configuration. See the [English UI labels](github-protection.md).

## Prepare and publish

Update `Directory.Build.props` and current documentation through a PR. Package README versions are substituted at build time. After merge and successful main CI, create an annotated tag and a draft containing the reviewed English notes. Keep the notes in a local file outside the tracked tree while drafting:

```powershell
git switch main
git pull --ff-only
git tag -a v1.0.0 -m "ReStart v1.0.0"
git push origin v1.0.0
gh release create v1.0.0 --draft --verify-tag --title "ReStart v1.0.0" --notes-file <local-notes-file>
gh workflow run release.yml -f tag=v1.0.0
gh run list --workflow release.yml
```

Dispatch the workflow from main. It checks the project/tag version and main ancestry, builds/tests, and packages x64 and ARM64 in clean Windows jobs. Publication requires an existing draft with notes. It uploads both ZIPs and SHA-256 sidecars, then publishes a stable release when the repository is public; private repositories retain the draft. An existing published release cannot be overwritten by this workflow. Actions are SHA-pinned, and only publication has `contents: write`. Public package jobs create GitHub build-provenance attestations of the final ZIPs using short-lived OIDC credentials. Attestations are separate from Windows code signatures.

The `release-signing` environment protects signing access. [Code signing policy and setup](code-signing.md) covers SignPath enrollment, provider approval, environment variables and the submitter secret. Leave `RESTART_SIGNING_PROVIDER` unset/`none` until enrollment is complete. When `signpath` is enabled, both own EXEs must have trusted timestamped signatures from the configured certificate, and all unsigned content must match the build. Failure blocks publication. SHA-256 sidecars are recomputed after signing, never reused from the unsigned ZIP. Never commit or upload PFX/private-key containers.

## Check the payload

The strict payload contains 41 files: the launcher, worker, fifteen manifest/JS pairs, six dependency/runtime notices, LICENSE and two READMEs. Runtime notices come from the exact resolved packs. Release builds omit debug metadata and map source paths to a neutral prefix. Packaging scans every ZIP entry, including binary UTF-8/UTF-16 strings, for generic credential and personal-path patterns without printing matched values.

Download the actual assets and verify SHA-256, PE architecture, product version/source ID and contents. Run the packaged x64 smoke, lifecycle and JS fixtures. ARM64 cross-builds do not establish hardware compatibility. See [testing](testing.md).

Currently published packages are unsigned; enrollment and a verified production signing run are still pending. Keep published tags immutable. Exit before replacing the complete portable installation. Settings live separately and retain upgrade backups; refresh startup/Explorer registration if the executable moved. In-app update checks use public releases without an API key; installation is manual.

After publication, run `./scripts/configure-public-security.ps1` and inspect the resulting CodeQL run and alerts. Confirm private vulnerability reporting, Dependabot alerts/security updates, secret scanning and push protection are enabled. Configuration alone does not establish a successful scan.
