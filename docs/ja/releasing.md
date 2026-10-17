# リリースと GitHub 運用

[English](../releasing.md)

短い feat/fix/docs/chore ブランチ、まとまった英語コミット、PR を使います。Windows テストと x64/ARM64 のクリーン配布ビルドを待って squash merge しブランチを削除します。v1.0以降のリリースノートは英語のみ、過去の日本語版は履歴として保存します。一般ドキュメントは日本語を併記します。

main 保護は .github/main-ruleset.json を ./scripts/protect-main.ps1 で適用・検証します。PR・会話解決・直線履歴・3つの厳格なチェック、管理者への適用、force push / 削除禁止を設定します。単独運用の承認人数は0です。以前は private のプラン制限で失敗しました。API が失敗した状態を保護済みとは扱いません。

PR で Directory.Build.props・英語ノート・CHANGELOG・現行案内を更新し、マージ後の CI 成功を確認して main に v1.0.0 形式の注釈付きタグを付けます。リリース workflow はタグ・バージョン・英語ノート・main の祖先を検証し、テスト、両アーキテクチャの ZIP、安定版公開を行います。配布物は35ファイル（本体・ワーカー・12組のJS/manifest・6つの通知・LICENSE・2つのREADME）です。

公開済みの実 ZIP を取得し SHA-256・PE アーキテクチャ・バージョン・ビルド出所・内容を検証し、x64 実行テストをします。ARM64 クロスビルドは実機実行と区別します。個人設定・ログ・カタログ・画像指定・資格情報は含めません。

リポジトリが非公開の場合、workflow は確認用の draft release を作成します。公開リポジトリでは安定版として公開します。

タグは通常変更しません。ユーザー指示による英語履歴への一度限りの書換えでは、オフライン bundle・旧新対応表・既存バイナリの元ビルド情報を保持し、タグ起動の workflow を停止して実施します。配布更新は終了後に ZIP 全体を展開します。保存先を変えたら起動・Explorer 登録を更新してください。

画面の英語ラベルを使った[保護設定の手順](github-protection.md)も参照してください。公開後は非公開脆弱性報告、Dependabot alerts / security updates、secret scanning / push protection と CodeQL default setup の実際の状態を確認します。

Release はデバッグ情報を除きソースパスを匿名化します。ZIP の全エントリ（バイナリの UTF-8 / UTF-16 を含む）を一般的な個人パス・資格情報の形式で検査し、該当値はログへ表示しません。
