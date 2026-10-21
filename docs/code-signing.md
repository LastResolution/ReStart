# Code signing policy

[日本語](ja/code-signing.md)

## Current status

Current published packages are **unsigned**. ReStart is preparing for [SignPath Foundation's free open-source signing program](https://signpath.org/), but enrollment and production signing have not been completed. Prepared CI is not proof of an issued certificate or a signed release. Release notes must state the status of the actual assets.

SignPath keeps the private signing key in its service. ReStart does not require a PFX, private key, signing-password file or certificate-store import on a developer PC or GitHub runner. The public signer certificate and publisher identity are embedded in signed executables and are intentionally public. A signature does not promise that SmartScreen warnings disappear immediately; [Microsoft documents reputation building for new signed downloads](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options).

## People and approval

- Committer and reviewer: [LastResolution](https://github.com/LastResolution).
- Intended production-signing approver: [LastResolution](https://github.com/LastResolution).
- GitHub and SignPath accounts must use MFA before signing is enabled. Account settings must be confirmed during enrollment; this document does not attest to their current state.
- Contributions go through pull requests, resolved conversations and the protected main branch's three required checks. Contributors cannot request production signatures from PR workflows.
- A production request requires explicit approval under the SignPath policy. The CI token must have submitter permissions only, without certificate, policy or administrator permissions.

These proposed roles and controls are subject to Foundation review. After acceptance, publish the Foundation's required attribution accurately: “Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org).” It is not a claim about current releases.

## What is signed

Only ReStart's own single-file executables, `ReStart.exe` and `TileHost/ReStart.TileHost.exe`, are submitted for Authenticode signing. Third-party components and notices remain in the portable package without being separately signed as ReStart projects. Both own executables carry the product name ReStart and the same release version.

The [artifact-configuration template](../.github/signpath-artifact-configuration.xml) describes the GitHub artifact ZIP containing one portable ZIP. It restricts both PE paths, product name and exact version metadata, and requests SHA-256 signatures. Its schema and resolved output must be validated in the SignPath account before production use. The pipeline rejects a missing/untrusted/untimestamped signature or a signer different from the configured public certificate thumbprint.

## Enable after enrollment

1. Apply at [SignPath Foundation](https://signpath.org/apply). Review the [terms](https://signpath.org/terms), confirm MFA and supply a contact address directly to the provider. Do not place personal contact details in tracked files.
2. Link the exact GitHub repository to a SignPath project. Restrict origin verification to `LastResolution/ReStart`, GitHub-hosted runners, the reviewed Release workflow, `workflow_dispatch` and `refs/heads/main`. Configure required production approval and the protected-branch constraints in the provider. Follow the [official GitHub integration](https://docs.signpath.io/trusted-build-systems/github); the artifact template alone does not enforce source or approval policy.
3. In GitHub, open **Settings → Environments → New environment**, create **release-signing**, and restrict **Deployment branches and tags** to **Selected branches and tags → main**. Additional environment reviewers are useful with multiple maintainers; do not create a solo self-approval deadlock. The SignPath production approval remains required.
4. In that environment create the secret **SIGNPATH_API_TOKEN** and these variables. No private certificate is uploaded:

   | Variable | Value |
   | --- | --- |
   | `SIGNPATH_ORGANIZATION_ID` | Assigned organization ID |
   | `SIGNPATH_PROJECT_SLUG` | Assigned ReStart project slug |
   | `SIGNPATH_SIGNING_POLICY_SLUG` | Approved production policy slug |
   | `SIGNPATH_CERTIFICATE_THUMBPRINT` | Public production certificate's 40 hexadecimal SHA-1 identifier; this pins identity, not the signature digest algorithm |

5. Validate the configuration and signed test output with the provider. Set the repository variable **RESTART_SIGNING_PROVIDER** to **signpath** only after production access and approval work. Until then leave it unset or `none`. An unknown provider or incomplete enabled configuration fails; a failed signature never falls back to an unsigned publication.
6. Dispatch the Release workflow from **main** with the reviewed draft/tag. It packages, uploads an unsigned workflow artifact for provider origin verification, waits for approved signing, verifies the returned payload, recomputes ZIP checksums, attests the final public ZIP and uploads release assets. Keep release tags immutable.

Certificate renewal requires reviewing and updating the public thumbprint. Tokens belong only in the protected environment secret and are referenced by the pinned signing action. Never print them, pass them on a shell command line, store them in README files or upload them as artifacts. The repository guard rejects private signing-container filenames and private-key/token patterns in staged files and final ZIPs.

## Verify a download

Check the ZIP against its SHA-256 sidecar. For releases produced by the new attesting workflow, verify GitHub build provenance with:

```powershell
gh attestation verify ReStart-win-x64.zip --repo LastResolution/ReStart
```

After extracting, inspect both code signatures:

```powershell
Get-AuthenticodeSignature .\ReStart.exe | Format-List Status,SignerCertificate,TimeStamperCertificate
Get-AuthenticodeSignature .\TileHost\ReStart.TileHost.exe | Format-List Status,SignerCertificate,TimeStamperCertificate
```

Checksums detect changed bytes. GitHub attestations associate a final ZIP with its build. Authenticode identifies the publisher to Windows. These mechanisms have different purposes; build attestations are not Windows executable signatures. Older releases do not gain attestations or code signatures retroactively.

The [signed-package validator](../scripts/verify-signed-package.ps1) also compares all ZIP entries against the unsigned build. For each EXE it excludes only the PE checksum, security-directory entry, alignment padding and appended certificate table before comparing SHA-256; the .NET bundle remains covered. Other files must match exactly. Its rejection tests use an already publicly signed .NET SDK binary, never a locally generated/trusted certificate or private key. An end-to-end SignPath production run remains pending enrollment.

## User data

See [security and data handling](../SECURITY.md). App lists, layouts and photo selections are not uploaded. Optional startup update checks contact public GitHub Releases and can be disabled; manual update checks remain available. Configured data tiles contact the explicit provider/file chosen in their connection dialog; personal connections are encrypted locally and excluded from exports. Signing submits CI-built release packages, never a developer's local settings or documents.
