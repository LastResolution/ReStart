# API と状態の契約

[English](../api-contracts.md)

公開 / protected API は XML コメントを必須とし、CS1591 をビルドエラーにしています。条件・所有権・副作用を記載します。

| 境界 | 契約 |
| --- | --- |
| TextKey / TextCatalog | 言語に依存しないキー。ユーザー名や OS の名前は翻訳しない |
| LayoutDocument | schema・値・ID を検証。入れ子フォルダーは拒否 |
| IsStandalone | ルート1個だけ。見出しなし。複数ある不正入力は普通のグループへ戻し、内容を捨てない |
| TileLayout / GroupLayout | 計算・プレビューは非破壊。グループ確定はアンカーだけを変更 |
| FolderExpansion | 展開は一時状態。保存した行を変更しない |
| LayoutEditor | ID を保持して所有権を移動。単独タイルはグループの追加先にしない |
| LayoutStore | 原本保全後に置換。将来の schema / 保存元アプリ版は上書き禁止 |
| AppCatalog / ShellCommands | COM は STA。有効な言語非依存 verb を読み、実行時に再照会 |
| SingleInstanceGate | パス・版に依存しないユーザー / セッション単位 |
| StartIntegration | 短いフック、UI は Dispatcher、終了時に解除 |
| StartupRegistration | 現ユーザーの値1個だけ。失敗時に元の値と型を復元 |
| ReleaseClient | 上限付きデータと SemVer、対応 ZIP と checksum を検証。認証・障害を最新と区別 |
| ScriptTileSession / WorkerJob | 同時要求1個、期限・メモリ・子プロセスを制限。OS 権限の隔離ではない |

画面は DIP、タイル座標は論理セル、グループの縦位置は半セル、横位置 X は論理セル、JS progress は0～1です。表示の折り返し・プレビュー・フォルダー展開は保存状態を変えてはいけません。schema / プロトコルの変更には互換入力・往復保存の検証と移行手順が必要です。

## v0.6 の現行契約

スキーマ4へ旧1～3から原本を保存して移行します。8言語、12種類のJSタイル、4ワーカー共有、PC時刻・集計メトリクス・選択フォルダ写真・タイル内クリックに対応します。新規カスタムタイルは既存グループに割り込まず空き場所へ独立配置します。下端グループの移動中も盤面範囲を保ち、同一プレビューの再レイアウトを省きます。更新確認は公開APIを認証なしで利用し、資格情報UIはありません。最新の詳細はリンク先の英語版と[JSガイド](javascript-tiles.md)を参照してください。
