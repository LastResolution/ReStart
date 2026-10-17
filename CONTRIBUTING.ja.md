# 開発への参加

[English](CONTRIBUTING.md)

一次ドキュメント、README、リリースノート、開発コメントは英語で記述し、日本語版も更新します。見た目や操作は[全体の動作確認](docs/ja/behavior-audit.md)で資料・実装・検証範囲を照合し、要望による拡張は本家の仕様と区別します。

ReStart は Windows 10 の全画面スタートの使い勝手を、独立した Windows アプリとして再現するプロジェクトです。見た目の変更では、どの Windows 10 バージョンを根拠とするかを明記してください。

## 通常の流れ

1. `main` を更新し、作業内容に合わせて `feat/*`、`fix/*`、`docs/*` ブランチを作ります。
2. ひとつの目的を持つ単位でコミットします。生成物や個人のレイアウトは含めません。
3. Release ビルド、関連するテスト、変更に応じた実機確認を実施します。
4. Pull Request にトリガー・変更後の動作・検証・未検証の範囲を記載します。
5. CI 成功と差分確認後に Squash merge し、不要になった作業ブランチを削除します。

```powershell
git switch main
git pull --ff-only
git switch -c fix/tile-focus
# 変更・検証
git add <変更したファイル>
git commit -m "fix: preserve focus when refreshing tile icons"
git push -u origin fix/tile-focus
gh pr create --base main --fill
```

`main` の履歴を書き換えません。共有済みのブランチへの force push も通常の作業では使いません。

## 検証

```powershell
dotnet restore ReStart.slnx --locked-mode
dotnet format whitespace ReStart.slnx --verify-no-changes --no-restore
dotnet build ReStart.slnx -c Release --no-restore
dotnet test ReStart.slnx -c Release --no-build
```

コアの配置・保存・検索・キーステートは、境界条件とデータ損失を防ぐテストを重視します。UI の文言変更だけに、実装を写したテストを増やす必要はありません。操作を変えたときは [実機チェック](docs/ja/testing.md) を行います。

依存関係を意図して変更した場合は `dotnet restore --force-evaluate` でロックファイルを更新し、差分をレビューします。アプリ本体は現在、外部 NuGet パッケージを使用しません。

## 実装上の制約

- Explorer への DLL 注入、非公開の Windows パッチ、管理者権限を前提にしません。
- フック内で I/O・列挙・描画をせず、UI スレッドに処理を渡します。
- キー捕捉は独立したメッセージループで動かし、終了時に解除します。
- シェル COM は STA スレッドで扱い、取得した画像は Freeze して UI に渡します。
- 起動先をコマンド文字列として評価せず、ShellExecute と引数リストを使います。
- インポートした設定で常駐フックや自動起動を勝手に有効にしません。
- 保存形式を変えるときはバージョン・移行・復旧方法を同時に設計します。

## 報告

不具合は GitHub Issues のテンプレートを利用してください。アプリ名・ユーザー名・ファイルパスなど、画面やレイアウトに含まれる個人情報は確認してから添付してください。秘密情報やトークンをログ・コミットに含めないでください。

## 表示文言とAPIコメント

- UI文言は `TextKey` と `Locales/ja.json`・`en.json` に追加し、型付きキーで参照します。日本語の原文を翻訳キーとして使いません。
- public / protected の型・メンバーにはXMLドキュメントコメントを書き、契約、入力単位、状態の変更、例外、呼び出し側の責任を必要な範囲で説明します。
- App / Core / TileHost はXMLドキュメント出力を有効にしています。コメント不足のCS1591も警告をエラーとして扱うため、CIを通りません。
- ドラッグのイベント段階、保存と復旧、OSフック、JSのプロセス境界など、実装上の理由が必要な箇所にはコメントを残します。
- [多言語実装](docs/ja/localization.md) と [API設計](docs/ja/api-contracts.md) も参照してください。
