# JavaScript ライブタイルを作る

![時計とカウントダウンのプレビュー](images/live-tiles.png)

設定 → JavaScript タイル → サンプルを作成すると、時計タイルが追加され、`tile.js` がメモ帳で開きます。編集して保存すると次の更新で反映します。コンパイルは不要です。タイルの右クリックから JavaScript・設定の編集、フォルダー表示、状態をリセットして再読み込みができます。

自分でパッケージを用意する場合は、同じフォルダーに `manifest.json` と `tile.js` を置き、「JavaScript タイルをインポート」で manifest を選びます。ReStart がデータフォルダーにコピーします。以降はコピー先を編集してください。配布ZIPの `Examples/clock` と `Examples/countdown` がサンプルです。

```json
{
  "version": 1,
  "title": "My tile",
  "refreshSeconds": 5,
  "settings": { "caption": "Hello" }
}
```

```js
function render(ctx) {
  return {
    text: ctx.settings.caption,
    detail: ctx.now,
    badge: "JS",
    background: "#0078D7",
    foreground: "#FFFFFF",
    progress: 0.5,
    state: { count: (ctx.state?.count ?? 0) + 1 }
  };
}
```

`render(ctx)` は同期関数で、JSON に変換できるオブジェクトを返します。HTML や XAML は返しません。表示は ReStart のネイティブコントロールで構成します。

| 入力 | 内容 |
| --- | --- |
| `ctx.now` | オフセット付きISO日時文字列。`new Date(ctx.now)` で使用 |
| `ctx.locale` | `ja-JP` または `en-US` |
| `ctx.size` | `small` / `medium` / `wide` / `large` |
| `ctx.settings` | manifest の設定オブジェクト |
| `ctx.state` | 前回成功した結果の state。初回は `{}` |

| 出力 | 制限 |
| --- | --- |
| `text` | 本文、256文字まで |
| `detail` | 補足、512文字まで。小サイズでは非表示 |
| `badge` | 16文字まで |
| `background`, `foreground` | 空文字または `#RRGGBB`。ハイコントラストではシステム色を優先 |
| `progress` | 0〜1、省略可能 |
| `state` | JSON表現で8,192文字まで。更新間で保持。アプリ終了・再読み込みでリセット |

タイルが小さい場合、文章は表示可能な範囲で省略します。更新周期は1〜3600秒です。ライブ更新をオフにすると実行を停止します。メニューを閉じている間はワーカーを終了し、再表示で再開します。同時更新は最大8タイルです。

## 実行の範囲

Jint 4.17.0 を専用の `ReStart.TileHost.exe` で動かします。JavaScript には JSON だけを渡し、CLR・Node.js・ファイル・ネットワーク・DOM・シェル起動API・ホスト側コールバックを公開しません。`eval` と文字列からの関数生成は無効です。モジュールの読み込みや `fetch` は使えません。旧 UWP 通知を受信するものではありません。

各更新は新しいエンジンで実行し、状態は返したJSONだけを引き継ぎます。20,000文、再帰32段、エンジン割り当て8 MB、100 msの制限を設定します。さらに親プロセスが通常1秒（初回起動4秒）の応答期限を持ち、超過時はワーカーを停止します。Windows Job Object はプロセス当たり192 MB・子プロセス作成不可・親終了時に終了する設定です。エラー時は最後に成功した表示を保持し、ツールチップにエラーを出します。初回から失敗した場合はエラー表示になります。

ソースファイルは64 KiB、manifestファイルは16 KiBまでです。JSONの設定と状態は8,192文字、通信は入力131,072文字・出力16,384文字までです。文字数は.NETのUTF-16コード単位で数えます。インポートするのは決まった2ファイルだけです。レイアウトJSONにはインストール済みパッケージのIDだけを保存し、任意の外部JSパスを指定できません。レイアウトのエクスポートにはJS本体を含めません。

別プロセスとリソース制限は応答性・障害の分離のためです。OSの権限を落としたコンテナではなく、任意の悪意あるコードに対する安全性を保証するものではありません。自分で作成した、または内容を確認したパッケージを使用してください。編集にはメモ帳を明示して使用し、Windows Script Host で `.js` を実行しません。

## 配置

通常は `%LOCALAPPDATA%/ReStart/tiles/<id>/`、プレビューは実行ファイル隣の `preview-data/tiles/<id>/` に保存します。アプリ更新時はZIP全体を展開し、`TileHost` と `Examples` も一緒に置いてください。自動起動の登録は ReStart 本体だけです。
