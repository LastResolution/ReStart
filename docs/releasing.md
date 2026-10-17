# リリースと GitHub 運用

## ブランチと PR

日常の変更は短い機能ブランチでコミットし、PR の `Windows build and tests` が成功してから Squash merge します。コミットの先頭は `feat:` / `fix:` / `docs:` / `test:` / `chore:` を目安にします。マージ後はブランチを削除します。

リポジトリは非公開です。可視性の変更は別途ユーザーの明示指示が必要です。ブランチ保護・Rulesets は GitHub のプランに依存します。設定できない場合も PR と CI の手順は省略しません。

2026-10-17 時点では、非公開リポジトリの main 保護設定 API がプラン制限の HTTP 403 を返しました。したがって保護はサーバー上で強制されていません。非公開を優先し、CI を確認して PR 経由でマージする運用を行います。プラン変更後に required status check `Windows build and tests`、PR 必須、force push 禁止、会話解決必須を設定できます。

## バージョン

`Directory.Build.props` の Version を変更し、必要な README、`portable-README.txt`、`release-notes.md`、検証記録を更新する PR を作ります。マージ済みの `main` に、バージョンと一致するタグを付けます。

```powershell
git switch main
git pull --ff-only
git tag -a v0.2.0 -m "ReStart v0.2.0"
git push origin v0.2.0
gh run list --workflow release.yml
```

`Release` ワークフローがタグとバージョン、main の祖先であることを確認し、ビルド・テスト後に x64 / ARM64 の自己完結型 ZIP と SHA-256 を作ります。両方の成功後にプレリリースを作成します。現行のワークフローは 0.x 用です。安定版を公開する際はプレリリース指定を見直します。

失敗した場合は原因を直し、既存タグを指す `workflow_dispatch` で再実行できます。公開済みタグのすり替えは行いません。修正コードを含める場合は新しいバージョンのタグを作成します。

## 配布前の確認

- CI と [検証](testing.md) を確認し、未検証を成功と記載しない。
- 実際の app list、ユーザー名、個人レイアウト、ログが含まれていないことを確認する。
- ZIPは新しいステージングフォルダーに作り、ReStart.exe、TileHost/ReStart.TileHost.exe、Examples、third-party、LICENSE、README.txtだけを収める。ビルドで解決した.NET Runtime / Windows Desktopのライセンスと通知もthird-partyへ同梱する。preview-dataや個人レイアウトを含めない。
- Releases の可視性はリポジトリに従う。非公開のまま配布する。
- 配布物の署名は未設定。署名鍵をリポジトリへ追加しない。

## CI の権限

通常の CI は contents:read。リリースの publish ジョブだけ contents:write と actions:read を使います。外部 Actions を SHA に固定し、Dependabot で更新を管理します。シークレットをログへ出さないでください。
