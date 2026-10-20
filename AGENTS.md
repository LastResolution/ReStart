# ReStart repository instructions

- Work on short-lived `feat/*`, `fix/*`, `docs/*`, or `chore/*` branches. Commit cohesive changes frequently, then use a GitHub PR and passing CI before merging to `main`.
- Keep the GitHub repository private unless the user explicitly changes that decision.
- Use the stable SDK selected by `global.json`; do not introduce preview SDK dependencies.
- Verify meaningful changes with Release build and relevant core tests. For UI changes, use preview/smoke mode and inspect the rendered result.
- Run `dotnet format whitespace ReStart.slnx --verify-no-changes --no-restore` before requesting merge.
- Document public API parameters, generic parameters and return values with their meaning, units, null/default behavior and mutation/ownership contracts. Describe important exceptions. Run `scripts/check-api-docs.ps1` before requesting merge; a summary alone is insufficient for a callable API.
- Do not commit `artifacts`, `preview-data`, personal app lists, layouts, logs, signing keys, credentials, or research screenshots from Microsoft.
- Use the maintainer's GitHub noreply identity for commits in this workspace. Run `scripts/check-privacy.ps1 -Staged` before committing; never copy private author emails or absolute user paths into tracked files.
- Keep `docs/fidelity.md` honest about implemented, partially reproduced, unverified, and unavailable behaviors. Never describe a feature as identical to Windows 10 without evidence.
- Preserve the shell: no Explorer patching or injected DLLs. Hooks must be reversible, short, and disposed on exit.
- Keep primary documentation, README and release notes in English; update the Japanese counterparts when behavior or release procedures change.
