# ReStart repository instructions

- Work on short-lived `feat/*`, `fix/*`, `docs/*`, or `chore/*` branches. Commit cohesive changes frequently, then use a GitHub PR and passing CI before merging to `main`.
- Keep the GitHub repository private unless the user explicitly changes that decision.
- Use the stable SDK selected by `global.json`; do not introduce preview SDK dependencies.
- Verify meaningful changes with Release build and relevant core tests. For UI changes, use preview/smoke mode and inspect the rendered result.
- Run `dotnet format whitespace ReStart.slnx --verify-no-changes --no-restore` before requesting merge.
- Do not commit `artifacts`, `preview-data`, personal app lists, layouts, logs, signing keys, credentials, or research screenshots from Microsoft.
- Keep `docs/fidelity.md` honest about implemented, partially reproduced, unverified, and unavailable behaviors. Never describe a feature as identical to Windows 10 without evidence.
- Preserve the shell: no Explorer patching or injected DLLs. Hooks must be reversible, short, and disposed on exit.
- Keep primary documentation, README and release notes in English; update the Japanese counterparts when behavior or release procedures change.
