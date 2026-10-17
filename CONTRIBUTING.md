# Contributing

[日本語](CONTRIBUTING.ja.md)

ReStart independently recreates Windows 10 full-screen Start. State the Windows version and primary evidence behind visual or behavioral changes. Implementation is not proof of exact fidelity.

## Workflow

1. Update main and create a short `feat/*`, `fix/*`, `docs/*` or `chore/*` branch.
2. Commit cohesive changes frequently. Exclude generated artifacts and personal data.
3. Run relevant Release tests and inspect changed UI in isolated preview mode.
4. Open a PR describing the trigger, resulting behavior, validation and limitations.
5. Wait for every CI check, review the diff, squash merge and remove the branch.

Never rewrite main. Rebase a published personal PR branch only with `--force-with-lease` and coordinate any shared branch.

~~~powershell
dotnet restore ReStart.slnx --locked-mode
dotnet format whitespace ReStart.slnx --verify-no-changes --no-restore
dotnet build ReStart.slnx -c Release --no-restore
dotnet test ReStart.slnx -c Release --no-build
~~~

Use [native fixture checks](docs/testing.md) for changed interactions. Test meaningful boundaries and data-loss risks; avoid tests that merely duplicate trivial UI text changes. Intentionally update lock files with `dotnet restore --force-evaluate` and review them.

## Constraints

- No Explorer patching, injected DLLs or normal-operation administrator requirement.
- Keep hooks short, use a dedicated native message loop and release registrations on exit.
- Use Shell COM on STA threads; freeze icon images before UI handoff.
- Use shell identities and argument lists instead of evaluating command strings.
- Never let imported layouts enable startup, hooks or credential changes.
- Design schema versioning, migration, backups and rollback together.
- Never commit credentials, personal app catalogs, layouts, logs or Microsoft research images.
- Keep fidelity documentation candid about physical-device and Windows 10 comparison gaps.

## Text, comments and documentation

Use semantic `TextKey` identifiers with `Locales/en.json` and `ja.json`; no Japanese source strings as translation keys. Add both translations and formatting tests. OS names, user labels and script output are not translated.

Write English XML comments for public/protected APIs. CS1591 is treated as an error in App, Core and TileHost. Explain units, side effects, invariants and caller responsibilities where they matter.

English documentation is primary. Maintain the linked Japanese counterparts when changing user-facing behavior or release instructions. See [localization](docs/localization.md) and [API contracts](docs/api-contracts.md).

Report ordinary defects through Issues, removing personal information from attachments. Use the [security policy](SECURITY.md) for vulnerabilities.
