# リリース

[English](../releasing.md)

短い作業ブランチ、まとまった英語コミット、PR を使います。**Windows build and tests**・**Clean portable win-x64**・**Clean portable win-arm64** の成功後に squash merge します。リリースノートは GitHub Releases に英語で記述し、重複するノートをソースツリーに置きません。

main の保護は `.github/main-ruleset.json` を `./scripts/protect-main.ps1` で適用・検証します。PR、会話解決、直線履歴、3つの厳格なチェック、force push / 削除禁止を設定し、bypass は設けません。単独運用の承認人数は0です。

PR で `Directory.Build.props` と現行ドキュメントを更新し、main の CI 成功後に注釈付きタグと英語ノートを含む draft release を作成します。続いて `gh workflow run release.yml -f tag=v1.0.0` を実行します。正確なコマンドは[英語版](../releasing.md)を参照してください。

workflow はタグ・バージョン・main の祖先を検証し、テスト、両アーキテクチャの配布ビルド、ZIP と SHA-256 のアップロードを行います。ノートを含む既存 draft が必須です。公開リポジトリでは安定版として公開し、非公開では draft のままにします。既存の公開済み release は上書きしません。

配布物は41ファイルです。本体・ワーカー・15組の JS/manifest・6つの通知・LICENSE・2つの README を含みます。個人設定・ログ・カタログ・画像指定・資格情報を含めません。Release はデバッグ情報を除き、ソースパスを匿名化します。ZIP 全体を一般的な個人パス・資格情報の形式で検査します。

実際の公開 ZIP を取得し、SHA-256・PE アーキテクチャ・版・ソース ID・内容を確認し、x64 実行テストをします。ARM64 クロスビルドと実機実行は区別します。タグは通常変更しません。アプリを終了して ZIP 全体を交換し、保存先が変わった場合は起動・Explorer 登録を更新します。

公開後は[保護設定](github-protection.md)、非公開脆弱性報告、Dependabot、secret scanning / push protection、CodeQL の実際の実行結果を確認します。
