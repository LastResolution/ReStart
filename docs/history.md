# History migrations

On October 18, 2026, the maintainer requested translating all Japanese commit messages. Two subjects were translated; all descendant commit/tag identifiers necessarily changed. For that translation pass, every commit tree, author, committer and timestamp was verified unchanged. The offline pre-migration bundle remains outside the repository. [The complete mapping](history/commit-map.tsv) links old and new identifiers.

A subsequent privacy pass replaced the maintainer’s private author/committer email and display name with the GitHub account’s public noreply identity. Commit trees, messages and timestamps were verified unchanged. Canonical branches and tags use this sanitized history. The former development repository is retained privately; publication uses a clean repository without its original GitHub PR refs.

Existing release binaries/assets remain unchanged and retain their original embedded source identifiers. Use this table when tracing those builds. Historical GitHub PR discussion/internal refs may still reference original commits. Ordinary protected-main development does not rewrite history. Existing clones should fetch and use a fresh checkout or deliberately reset after preserving local work; do not merge the two histories.

| Tag | Original source / binary build | Rewritten source |
| --- | --- | --- |
| v0.1.0 | `f347d8b03871b6c2819ca5adee24e8273c0699e9` | `1cd785463d65c7c91c95985e2b1abe0798563060` |
| v0.1.1 | `bcf187dc446b373a3b4944ec0058090332f2c749` | `3a70446c5882db85ab53bdcb7e2a1e3ea50e2707` |
| v0.2.0 | `1c6f2150bc01e06b602df062c77295a863c86150` | `782bca304873c4619ed0fc8072f6a7d1b8f9b413` |
| v0.2.1 | `ccf1ea24ffc6547f310a6a46a1410000456728df` | `078827c74258d3d79061b371b9cdf14f9ccb58ed` |
| v0.2.2 | `937839ddc411c3caab3cf757e37d8ed3643ae3c4` | `52b8eee2e841be6b40febc7156af7b2276b1e139` |
| v0.3.0 | `34cae2febdcdee02abf55e59b34d2363ae3b0e6d` | `92701c561d8c64765d68dbecfdcb6fffb88a1ea0` |
| v0.4.0 | `02184aa8beaf49ebca5e2fbe80d05b82d72f9925` | `03b8a685910c9ec2594b769b2484014a2d621e72` |
| v0.5.0 | `79432f0e472cb5d980027cc462aec304a6dfb2e8` | `9b567791d5c7f4eb2c3f3ae988811c3eda35887d` |
