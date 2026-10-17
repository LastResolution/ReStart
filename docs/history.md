# History migrations

On October 18, 2026, the maintainer requested translating all Japanese commit messages. Two subjects were translated; all descendant commit/tag identifiers necessarily changed. For that translation pass, every commit tree, author, committer and timestamp was verified unchanged. The offline pre-migration bundle remains outside the repository. [The complete mapping](history/commit-map.tsv) links old and new identifiers.

A subsequent privacy pass replaced the maintainer’s private author/committer email and display name with the GitHub account’s public noreply identity. Commit trees, messages and timestamps were verified unchanged. Canonical branches and tags use this sanitized history. The full audit also normalized display names on earlier GitHub squash merges, including commits that already used noreply addresses. The former development repository is retained privately; publication uses a clean repository without its original GitHub PR refs.

Existing release binaries/assets remain unchanged and retain their original embedded source identifiers. Use this table when tracing those builds. Historical GitHub PR discussion/internal refs may still reference original commits. Ordinary protected-main development does not rewrite history. Existing clones should fetch and use a fresh checkout or deliberately reset after preserving local work; do not merge the two histories.

| Tag | Original source / binary build | Rewritten source |
| --- | --- | --- |
| v0.1.0 | `f347d8b03871b6c2819ca5adee24e8273c0699e9` | `80cd0a3aca24619f2fee77f3aaef236586655c12` |
| v0.1.1 | `bcf187dc446b373a3b4944ec0058090332f2c749` | `285348a8a0d6de5a06710eec57bc01f8eb26f4c6` |
| v0.2.0 | `1c6f2150bc01e06b602df062c77295a863c86150` | `5c8d78e9f582a0906f22738f56b9681c0d3ba9dc` |
| v0.2.1 | `ccf1ea24ffc6547f310a6a46a1410000456728df` | `fca7ec584f48192aef79d48f1c08c6038681d51f` |
| v0.2.2 | `937839ddc411c3caab3cf757e37d8ed3643ae3c4` | `9b1463712279156e48439ae187a69bade8d8115f` |
| v0.3.0 | `34cae2febdcdee02abf55e59b34d2363ae3b0e6d` | `5bf28ccc82dda741d752b9007dc57dbcb2153ab4` |
| v0.4.0 | `02184aa8beaf49ebca5e2fbe80d05b82d72f9925` | `d16a077ae056c7f2937ec68f45b81c3fe44d1acb` |
| v0.5.0 | `79432f0e472cb5d980027cc462aec304a6dfb2e8` | `8ac0253b1b9a96de20c125a900493ed8a8b4bf4a` |
