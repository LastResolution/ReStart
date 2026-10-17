# デフォルトブランチの保護

[English](../github-protection.md)

対象は master ではなく **main** です。画面の英語表記は次のとおりです。Settings → Rulesets → New ruleset → New branch ruleset を開きます。

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

**Require a pull request before merging** の **Required approvals** は単独運用のため `0`。**Dismiss stale pull request approvals when new commits are pushed** と **Require conversation resolution before merging** は On。**Require review from Code Owners** と **Require approval of the most recent reviewable push** は Off。**Allowed merge methods** は Squash のみです。他者承認を必須にすると自分の PR を単独ではマージできません。

**Require status checks to pass** 内の **Require branches to be up to date before merging** を On にし、**Windows build and tests**・**Clean portable win-x64**・**Clean portable win-arm64** を追加します。

デプロイ・署名・スキャン・カバレッジ・Copilot の必須化は、対応する運用を設定するまで Off です。**Create** 後に Active・対象・チェックを再確認してください。private のプラン警告が出ている間はルールを作っても強制されません。公開リポジトリでは GitHub Free でも利用できます。[公式手順](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/creating-rulesets-for-a-repository)。

同じ設定を `.github/main-ruleset.json` と `./scripts/protect-main.ps1` で再現できます。名前が一致するルールだけを作成・更新し、管理者バイパスを設けず、保存結果と main に実際に適用されるルールを検証します。失敗を保護済みとは扱いません。この管理コマンドは開発者の GitHub CLI を使用しますが、配布アプリの更新確認にトークンは不要です。
