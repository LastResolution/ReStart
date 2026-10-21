# Protect the default branch

This repository's default branch is **main**. In Settings → Rulesets → New ruleset → New branch ruleset, use the exact English labels below. Public repositories support active rulesets on GitHub Free; a private-plan warning means rules are not enforced. [GitHub setup guide](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/creating-rulesets-for-a-repository).

| Setting | Value |
| --- | --- |
| Ruleset Name | Protect main |
| Enforcement status | Active |
| Bypass list | Empty |
| Target branches → Add target | Include default branch |
| Restrict creations | Off |
| Restrict updates | Off |
| Restrict deletions | On |
| Require linear history | On |
| Require a pull request before merging | On |
| Require status checks to pass | On |
| Block force pushes | On |

Under **Require a pull request before merging**, use **Required approvals: 0** for the single-maintainer workflow, enable **Dismiss stale pull request approvals when new commits are pushed** and **Require conversation resolution before merging**, and leave **Require review from Code Owners** and **Require approval of the most recent reviewable push** off. Allow only **Squash**. Requiring another person's approval prevents a sole maintainer from merging their own PRs.

Under **Require status checks to pass**, enable **Require branches to be up to date before merging**. Add these GitHub Actions checks:

- Windows build and tests
- Clean portable win-x64
- Clean portable win-arm64

Leave deployments, signed commits, scanning/coverage results and automatic Copilot review off until those workflows/policies are deliberately configured. Click **Create**. Refresh the rule afterward to verify **Active** and the intended target/checks.

For reproducible API configuration, `.github/main-ruleset.json` contains the same policy and binds checks to the observed GitHub Actions app (15368). Run `./scripts/protect-main.ps1`; it creates/updates only the named rule, checks the returned settings, then verifies effective rules on main. It does not add an administrator bypass. A failed plan/permission response is not success. This is a repository-maintenance command; the application/update checker needs no token.
