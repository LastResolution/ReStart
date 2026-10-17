# 検証方法

[English](../testing.md)

Windows と global.json の安定 SDK を使います。使用中の実行ファイルを再ビルドする場合は通知領域から終了するか、別の出力先を指定します。

~~~powershell
dotnet restore ReStart.slnx --locked-mode
dotnet format whitespace ReStart.slnx --verify-no-changes --no-restore
dotnet build ReStart.slnx -c Release --no-restore
dotnet test ReStart.slnx -c Release --no-build
./scripts/smoke.ps1
./scripts/test-navigation.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-navigation.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe -AppFolders
./scripts/test-first-input.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-app-menus.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-release-info.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-placement.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/package.ps1 -Runtime win-x64
./scripts/test-lifecycle.ps1 -Executable artifacts/publish/win-x64/ReStart.exe
~~~

| 検証 | 対象 |
| --- | --- |
| smoke | WPF 描画、カタログ / アイコン数。SearchQuery E と配布 EXE も指定可能 |
| navigation | 架空288アプリ、4幅、見出しの連続、8回の再表示リセット |
| AppFolders | 階層開閉、キー / Automation、隠れた子の検索、幅・テーマ・文字ジャンプ |
| first-input | 列挙を待機した新規画面へ文字合成で E、完了後 Edge、継続 d、再表示・フォーカス |
| app-menus | ピン / 解除、メニュー更新、有効 verb の読み取り。実行しない |
| release-info | 版・ライセンス・移行原本・記録、日英×テーマ、アクセント |
| placement | 独立アンカー、押し出しプレビュー / 取消、内部座標、単独タイルの実寸、保存、直接命名、フォルダー |
| lifecycle | コピー先2か所から10回起動して主プロセス1個、重複 smoke はコード2 |

本番の設定・フック・自動起動・更新通信・資格情報を変更しないプレビューを使います。PNG / JSON は artifacts に保存し、画像も確認します。イベントと文字合成は物理 OS 入力の検証ではありません。

Core は繰り返し衝突・保存後の内部位置保持、移行原本、将来版保護、更新データ上限・認証 / 接続障害を確認します。資格情報・Run 登録のテストは専用 GUID の対象だけを使います。

物理 Win→E、Win+E/D/L/Tab、タスクバー、IME、Narrator、タッチ、DPI / 複数画面、ARM64、実サインインは別途手動確認が必要です。通常の検証で電源・削除・管理者実行を行いません。補助ツールの入力制限を迂回しません。[検証記録](validation.md)に結果と未検証を分けて残します。

## v1.0 の現行契約

スキーマ4へ旧1～3から原本を保存して移行します。8言語、12種類のJSタイル、4ワーカー共有、PC時刻・集計メトリクス・選択フォルダ写真・タイル内クリックに対応します。新規カスタムタイルは既存グループに割り込まず空き場所へ独立配置します。下端グループの移動中も盤面範囲を保ち、同一プレビューの再レイアウトを省きます。更新確認は公開APIを認証なしで利用し、資格情報UIはありません。最新の詳細はリンク先の英語版と[JSガイド](javascript-tiles.md)を参照してください。
