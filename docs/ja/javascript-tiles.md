# JavaScript ライブタイル

[English / API reference](../javascript-tiles.md)

背景・グループの右クリック → ライブタイルを追加、または設定のギャラリーから12種類を追加できます。時計・カレンダー・CPU・メモリ・集中タイマー・年間進捗・カウントダウン・通信量・バッテリー・ストレージ・写真などを同梱しています。追加時にエディタは開きません。通常クリックは ctx.event = { type: "click", unixMs } を渡し、一時停止や表示切替、次の写真に使います。JS・設定の編集は右クリックからです。「サンプルを作成」は明示的な開発操作としてメモ帳を開きます。

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

## 実行と保存

8タイル制限はなく、4つの分離ワーカーを共有します。各描画は新しい Jint 環境で20,000文・再帰32・割当8 MB・100 msまで。親の応答期限は通常1秒、初回4秒で、ワーカーごと192 MBの Job Object と子プロセス禁止を設定します。失敗時は最後の正常な表示を保ちツールチップにエラーを出します。

CLR・Node・ファイル・ネットワーク・DOM・シェル・eval・モジュールは利用できません。ソース64 KiB、manifest 16 KiB、設定/状態各8,192文字、コンテキスト32,768文字、要求/応答131,072/16,384文字が上限。プロセス分離は応答性の保護で、OS 権限サンドボックスではありません。内容を確認して取り込んでください。

保存先は %LOCALAPPDATA%\ReStart\tiles\<id>。プレビューは別領域です。レイアウト出力に JS ソースは含めません。TileHost と Examples を本体と一緒に保持してください。

## タイルライブラリ

設定の「ライブラリフォルダを開く」で %LOCALAPPDATA%\ReStart\TileLibrary を開き、各パッケージのフォルダ（manifest.json と tile.js）を置きます。設定画面が追加・変更を自動検出し、表示されたタイルをクリックして追加できます。毎回 JSON を選ぶ必要はありません。直下最大128フォルダを検査し、不正パッケージは実行せず無効表示します。「再読み込み」も使えます。追加先は別のインストール済みコピーで、既存グループを動かさず独立した空き場所へ配置します。
