# JavaScript ライブタイル

[English / API reference](../javascript-tiles.md)

背景・グループの右クリック → ライブタイルを追加、または設定のギャラリーから15種類を追加できます。時計・カレンダー・CPU・メモリ・集中タイマー・年間進捗・カウントダウン・通信量・バッテリー・ストレージ・写真・天気・RSS/Atom・JSONモニターを同梱しています。追加時にコードエディタは開きません。データを使うタイルはネイティブ設定画面を開き、有効な接続を保存するまで通信しません。通常クリックは ctx.event = { type: "click", unixMs } を渡し、一時停止や表示切替、次の写真・見出しに使います。JS・設定の編集は右クリックからです。「サンプルを作成」は明示的な開発操作としてメモ帳を開きます。

manifest.json と tile.js の2ファイルを取り込み、以降は保存されたコピーを編集します。version は1、title は80文字、refreshSeconds は1～3,600秒、settings は8,192文字以内の JSON オブジェクトです。localizedTitles は8言語の既定表示名に使い、言語切替にも追従します。明示的に変更した名前は保持します。

render(ctx) は同期関数で表示用 JSON を返します。HTML / XAML を実行せずネイティブに描画します。ctx.settings は設定、ctx.state は前回成功時の状態、ctx.size は small / medium / wide / large。状態は再表示で保持し、リロード・終了でリセットされます。集中タイマーにアラームやバックグラウンド通知はありません。

## サイズごとの配置

![同じスクリプトの4サイズ表示](../images/live-tiles-sizes.png)

`ctx.size` は small / medium / wide / large、`ctx.bounds` は実際の幅・高さ（DIP）と scale です。初回の幅・高さは配置前に0の場合があるため、size を基本にしてください。

返り値の `layout` で、auto / overlay（従来の重ね表示）、centered（文字を中央、グラフなし）、split（左に文字、右にグラフ）、stacked（上に文字、下にグラフ）、caption（写真の下部に文字）を選べます。HTML / CSS は実行しません。

同梱例は、小で要点と短い数字、中で値を強調、横長で左右分割、大で詳細と大きなグラフに切り替えます。小では装飾・補助情報を省き、時計は数字を2行にします。写真は下部キャプションです。[英語版のコード例](../javascript-tiles.md#responsive-native-layouts)を参照してください。

## PC 時刻とメトリクス

ctx.now は PC の UTC オフセット付き時刻。ctx.time は unixMs・monotonicMs（稼働時間）・utc・local・offsetMinutes・Windows timeZoneId、ctx.calendar は翻訳済み shortDate・weekday・month と数値の day です。経過時間には monotonicMs を使用します。外部時刻サービスは使わず、OS の時刻・タイムゾーン変更で再描画します。

capabilities に system.metrics を指定すると ctx.system に CPU 使用率、論理 CPU 数、物理メモリ、稼働時間、固定ドライブ容量、送受信速度、バッテリー状態を渡します。全フィールドと単位は[英語 API 表](../javascript-tiles.md)を参照してください。CPU・通信速度は2回のサンプルが必要で、準備中・取得不能は null。通信速度は有効な非ループバック・非トンネルインターフェースの合計で、仮想アダプターでは重複計上する場合があります。ドライブ容量は約30秒キャッシュし、非表示中はサンプリングを停止します。ファイル内容・プロセス名を渡さず、スクリプト自身の通信 API はありません。

## 写真とスタイル

images.slideshow を指定すると、ネイティブ UI で選択したフォルダを使えます。ctx.slideshow は available・count・index のみ。パス・ファイル名・画素は渡さず image: "current" で表示します。直下の JPG/JPEG/PNG/BMP/GIF 最大1,000件、各20 MiB以内、幅768pxへデコード、GIF は静止画です。settings.intervalSeconds は5～3,600秒、既定15秒。クリックで次へ送り、そこから自動送りを続けます。非同期読み込み後はファイルを閉じ、アニメーション有効時はクロスフェードします。ハイコントラストと個別背景色を優先します。

フォルダ指定は保存済み host-bindings.json に置き、パッケージから取り込まずレイアウト出力にも含めません。別 PC で再指定してください。

text/detail/badge/kicker は最大256/512/16/48文字。background・foreground・backgroundEnd・accent は空または #RRGGBB。visual は空・ring・bars・sparkline・dots、series は0～1の有限値を最大32個、progress は0～1。小タイルは補助テキストを省略します。同梱ソースをスタイルの見本として使ってください。

## 天気・フィード・Web API・他アプリのデータ

![架空のローカル入力によるデータ連携タイル](../images/connected-data.png)

`capabilities: ["data.sources"]` と `dataSources` を宣言すると、ホストが明示的に設定された接続先を非同期で読み、`ctx.data.<id>` に最新のスナップショットを渡します。JS 自体に通信・ファイル API は追加しません。[宣言・コード例と全フィールド](../javascript-tiles.md#weather-feeds-web-apis-and-application-data)を参照してください。

右クリック →「データソースを設定…」から Web API / フィードまたはローカルデータファイルを指定します。HTTPS とループバック HTTP を GET で読み取れます。別アプリは既存の localhost エンドポイント、または原子的に置換する JSON ファイルで連携できます。[ファイル生成例](../../examples/integrations/write-tile-data.ps1)を自分で実行し、生成されたファイルを JSON モニターで選択してください。スクリプトの自動起動、書き込み、任意コマンド実行は行いません。

宣言は最大4入力。`format` は json / feed、`refreshSeconds` は1～86,400秒（HTTP は最低30秒、ファイルは最低1秒）。`jsonPointer` は最大256文字で、空はルート、`~1` は `/`、`~0` は `~`。設定画面の指定を優先します。`suggestedUrl` は公開の例示 URL に限り、自動接続しません。`suggestedAccess` は http / file で、パッケージからローカルファイルを指定できません。

各スナップショットの `status` は unconfigured / disabled / loading / ready / error、`value` は選択した JSON または初期値 null。`updatedAt` は最終成功・再検証時刻、`checkedAt` は失敗も含む確認時刻、`stale` は以前の正常値を表示しているか、`error` は接続先を含まない code と httpStatus です。RSS 2.0 / Atom は `{title, items:[{title,summary,url,published}]}` に正規化し最大20件、HTML は取り除き、DTD・実体参照を拒否します。リンクは表示データで、自動起動しません。

入力は256 KiB、JSON 深さ12、選択値はタイル全体でシリアライズ後8,192文字以内。共有の同時取得は4件、開始後8秒で打ち切ります。ETag / Last-Modified とキャッシュを使い、失敗時は前回値を保持して再試行間隔を最大1時間まで延ばします。「データを今すぐ更新」は5秒の抑制付きです。非表示で取得を中止し、再表示ではメモリ内のキャッシュを使い、再読み込み・終了で消去します。ファイルは明示した絶対ローカルパスのみで、UNC と直接の再解析ポイントを拒否します。

認証はネイティブ画面のパスワード欄に `Bearer …` などを入力します。空欄は同じサーバーの保存値を維持し、接続先サーバー変更・ファイル選択で消去します。Cookie や接続制御ヘッダーは指定できず、OS の資格情報を自動送信しません。リダイレクトは同一オリジン内3回までなので、別サーバーへ転送するフィードは最終 URL を指定します。プロバイダーの規約・取得頻度に従ってください。

URL・パス・セレクター・資格情報はインストール済み `data-bindings.dat` に現 Windows ユーザーの DPAPI で暗号化して保存し、JS・パッケージ取り込み・レイアウト出力には渡しません。別 PC では再設定します。同じユーザー権限の他プログラムから守る仕組みではありません。共有する manifest / JS にキーや個人 URL を書かないでください。API の返すデータ自体に機密情報があれば、選択対象と利用するパッケージを確認してください。

### 天気の地域

既定の天気は **ベルリン（52.52, 13.41）** の例です。PC の地域ではなく、IP 推定やデバイス位置情報は取得しません。接続設定の URL の latitude / longitude を変更し、current=temperature_2m,weather_code,wind_speed_10m とセレクター `/current` を維持してください。「タイルの設定を編集」で settings.location を地名へ変え、保存後に再読み込みします。表示単位は摂氏・km/h。座標は [Open-Meteo の API ビルダー](https://open-meteo.com/en/docs)で作成でき、公開無料エンドポイントの非商用条件を含む[利用規約](https://open-meteo.com/en/terms)が適用されます。

## 実行と保存

8タイル制限はなく、4つの分離ワーカーを共有します。各描画は新しい Jint 環境で20,000文・再帰32・割当8 MB・100 msまで。親の応答期限は通常1秒、初回起動は10秒で、ワーカーごと192 MBの Job Object と子プロセス禁止を設定します。失敗時は最後の正常な表示を保ちツールチップにエラーを出します。

CLR・Node・直接のファイル / ネットワーク API・DOM・シェル・eval・モジュールは利用できません。ソース64 KiB、manifest 16 KiB、設定/状態各8,192文字、コンテキスト32,768文字、要求/応答131,072/16,384文字が上限。プロセス分離は応答性の保護で、OS 権限サンドボックスではありません。内容を確認して取り込んでください。

保存先は %LOCALAPPDATA%\ReStart\tiles\<id>。プレビューは別領域です。レイアウト出力に JS ソースは含めません。TileHost と Examples を本体と一緒に保持してください。

## タイルライブラリ

設定の「ライブラリフォルダを開く」で %LOCALAPPDATA%\ReStart\TileLibrary を開き、各パッケージのフォルダ（manifest.json と tile.js）を置きます。設定画面が追加・変更を自動検出し、表示されたタイルをクリックして追加できます。毎回 JSON を選ぶ必要はありません。直下最大128フォルダを検査し、不正パッケージは実行せず無効表示します。「再読み込み」も使えます。追加先は別のインストール済みコピーで、既存グループを動かさず独立した空き場所へ配置します。
