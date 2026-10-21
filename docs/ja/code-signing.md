# コード署名ポリシー

[English](../code-signing.md)

## 現在の状態

現在公開済みの配布物は**未署名**です。[SignPath Foundation の無料OSS署名](https://signpath.org/)に向けた準備段階で、申請承認・本番署名はまだ完了していません。CIを用意しただけでは証明書発行や署名の証拠になりません。実際に配布するファイルの状態を英語リリースノートに記載します。

秘密鍵は署名サービス側で保持し、開発PC・GitHubランナーへのPFX配置、秘密鍵やパスワードの保存、証明書ストアへのインポートは不要です。署名に含まれる**公開証明書と発行元情報は公開情報**です。署名直後のSmartScreen警告解消は保証されません。[Microsoftの説明](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)を参照してください。

## 役割と承認

- コミッター・レビュー担当：[LastResolution](https://github.com/LastResolution)。
- 本番署名の承認予定者：[LastResolution](https://github.com/LastResolution)。
- 署名有効化前にGitHubとSignPathでMFAを使用します。現在の有効状態をこの文書だけで保証するものではなく、申請時に本人が確認します。
- PR・会話解決・保護されたmainの3必須チェックを経由します。PRのワークフローから本番署名を要求しません。
- SignPath側で本番承認を必須とし、CIトークンは申請専用に限定します。証明書・ポリシー変更・管理権限は付けません。

これらは審査対象の予定です。採用後、公式指定の提供元表記を正しく掲載します。現時点で提供を受けていると誤記しません。

## 対象と設定

対象は自作の単一ファイルEXE `ReStart.exe` と `TileHost/ReStart.TileHost.exe` のみです。第三者のライブラリをReStart名義で個別に署名しません。両EXEのProductNameはReStart、バージョンは同じリリース値です。

[成果物定義](../../.github/signpath-artifact-configuration.xml)はGitHub成果物ZIPの中にある配布ZIPと、対象2EXEのパス・製品名・厳密なバージョンを制限します。署名にはSHA-256を指定します。SignPathアカウント上で定義の検証と試験を行ってから本番利用します。署名・タイムスタンプ・信頼・想定証明書のいずれかが不足したら公開を中止します。

1. [申請ページ](https://signpath.org/apply)と[条件](https://signpath.org/terms)を確認します。連絡先はサービスに直接渡し、追跡ファイルには残しません。
2. SignPath側で対象を `LastResolution/ReStart`、GitHub管理ランナー、レビュー済みReleaseワークフロー、`workflow_dispatch`、`refs/heads/main`に限定します。本番承認とmain保護の条件も設定します。[公式GitHub連携](https://docs.signpath.io/trusted-build-systems/github)に従い、XMLの定義だけでソースや承認が保護されたと扱いません。
3. GitHubの **Settings → Environments → New environment** で **release-signing** を作成し、**Deployment branches and tags → Selected branches and tags → main** に制限します。複数の保守者がいる場合は環境レビューも追加できます。単独保守で自己承認不可の行き止まりを作らず、SignPath側の本番承認は維持します。
4. 環境secret **SIGNPATH_API_TOKEN** と、変数 **SIGNPATH_ORGANIZATION_ID / SIGNPATH_PROJECT_SLUG / SIGNPATH_SIGNING_POLICY_SLUG / SIGNPATH_CERTIFICATE_THUMBPRINT** を登録します。最後の値は本番公開証明書の40桁16進の識別子で、署名自体のSHA-256とは別です。秘密鍵はアップロードしません。
5. 定義と署名結果を試験後、リポジトリ変数 **RESTART_SIGNING_PROVIDER=signpath** を有効にします。それまでは未設定または `none` です。有効な署名設定で失敗した場合、未署名へフォールバックせず処理を止めます。
6. mainからレビュー済みタグ・draftでReleaseを実行します。未署名ビルドを成果物として登録し、出所検証・承認・署名後の内容確認を経てZIPを置換、チェックサムを再計算し、公開ZIPのGitHub出所証明を作ってから配布します。公開タグは変更しません。

証明書更新時は公開thumbprintを確認して変更します。トークンをログ、コマンド引数、README、成果物に出さず、固定SHAの署名Actionだけに渡します。コミット前とZIPの検査は秘密鍵コンテナ名や秘密鍵・トークンのパターンを拒否します。

## ダウンロードの確認

ZIPのSHA-256を照合します。新しい出所証明対応ワークフローで作ったリリースは次で確認できます。旧版に証明が遡及追加されるわけではありません。

```powershell
gh attestation verify ReStart-win-x64.zip --repo LastResolution/ReStart
Get-AuthenticodeSignature .\ReStart.exe | Format-List Status,SignerCertificate,TimeStamperCertificate
Get-AuthenticodeSignature .\TileHost\ReStart.TileHost.exe | Format-List Status,SignerCertificate,TimeStamperCertificate
```

SHA-256はZIPの改変検出、GitHub出所証明はビルドとの対応、AuthenticodeはWindows上の発行元確認です。別の目的を持ち、GitHubの証明だけではEXEにWindows署名は付きません。

[検証スクリプト](../../scripts/verify-signed-package.ps1)はファイル一覧と通常ファイルの完全一致、EXEの署名以外の内容一致も確認します。EXEではチェックサム、セキュリティディレクトリ、配置の端数ゼロ、末尾証明書以外をSHA-256で比較し、.NETバンドルも含めます。テストには既に公開署名されたSDKバイナリを使い、秘密鍵の生成や信頼登録は行いません。SignPath本番での一連の試験は承認後に必要です。

## 利用者のデータ

[セキュリティとデータ](../../SECURITY.ja.md)を参照してください。アプリ一覧・配置・写真の選択を送信しません。任意の起動時更新確認は公開GitHubにアクセスし、設定で無効化できます。データタイルは接続画面で選んだ宛先だけを使い、個人の接続をローカル暗号化して出力から除外します。署名に提出するのはCIで作った配布物だけで、個人の設定や文書は含めません。
