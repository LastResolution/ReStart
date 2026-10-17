# APIと状態の契約

実装の公開型・メンバーにはXMLドキュメントコメントを付け、App / Core / TileHost のビルドで `.xml` を出力します。コメント不足のCS1591はビルドエラーです。コメントは名前の言い換えにせず、呼び出し側が守る条件と副作用を説明します。

| 境界 | 契約 |
| --- | --- |
| TextCatalog / TextKey | 言語と独立した型付きキー。翻訳対象はアプリ所有のUI文言だけ |
| LayoutDocument | schema versionを検証。表示値・IDは正規化し、入れ子フォルダーは拒否 |
| LayoutStore | 一時ファイルから置き換え、直前のバックアップを保持。未対応バージョンは型で識別して上書き禁止 |
| TileLayout | 論理セルで配置し、入力モデルを変更しない。既存座標を予約してから衝突・未配置項目を詰める |
| LayoutEditor | 移動・フォルダー化のモデル変更。IDと一段の親子関係を維持 |
| AppCatalog | Shell COMを専用STAで使用。アイコンはFreezeして共有。バッチ通知からUIを変更する場合はDispatcherへ渡す |
| SingleInstanceGate | 実行ファイルの場所や版とは独立したユーザー・セッション単位の寿命。ActivationハンドルはGateが所有 |
| StartIntegration | フックは短く終了し、WPF操作はDispatcherへ渡す。終了時に登録解除 |
| StartupRegistration | 現ユーザーのRun値1つだけを更新。設定の保存失敗に備え、値と型を復元可能 |
| ScriptTilePackages | GUIDでインストール先を解決。任意のJSパスをレイアウトから実行しない |
| JsTileRunner | 1回ごとに新しいrealm。JSON以外のホストオブジェクトを渡さない |
| ScriptTileSession | 1セッションに同時リクエスト1つ。現ユーザーだけが接続できる非同期名前付きパイプを使い、通信キャンセルとは独立した待機期限も持つ |
| WorkerJob | メモリ・プロセス数・寿命の制限。OSの権限を落としたサンドボックスではない |

タイルの画面座標はWPFのDIP、保存する座標は論理セル、スクリプトのprogressは0〜1です。メニュー非表示時のJSプロセス終了は状態JSONを保持し、タイル再読み込みとアプリ終了では状態をリセットします。詳細は各型のコメントと [JavaScript API](javascript-tiles.md) を参照してください。
