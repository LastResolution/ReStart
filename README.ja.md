<p align="center"><img src="assets/ReStart.png" width="96" alt="ReStart" /></p>
<h1 align="center">ReStart</h1>
<p align="center"><strong>フルスクリーンのスタートを、もう一度。</strong><br>いつものアプリと、自分で作るライブタイルのための場所。</p>
<p align="center">
  <a href="https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip"><strong>Windows 版をダウンロード · x64</strong></a> ·
  <a href="https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-arm64.zip">ARM64</a> ·
  <a href="https://github.com/LastResolution/ReStart/releases/latest">Release notes</a> ·
  <a href="docs/javascript-tiles.md">タイルを作る</a> ·
  <a href="CONTRIBUTING.md">開発に参加</a>
</p>
<p align="center">
  <a href="https://github.com/LastResolution/ReStart/actions/workflows/ci.yml"><img src="https://github.com/LastResolution/ReStart/actions/workflows/ci.yml/badge.svg" alt="Windows CI" /></a>
  <a href="https://github.com/LastResolution/ReStart/releases/latest"><img src="https://img.shields.io/github/v/release/LastResolution/ReStart?color=0078d4" alt="Latest release" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-0078d4" alt="MIT license" /></a>
</p>

<!-- languages:start -->
<p align="center">
  <a href="README.md">English</a> · <a href="README.de.md">Deutsch</a> · <a href="README.es.md">Español</a> · <a href="README.fr.md">Français</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a>
</p>
<!-- languages:end -->

<p align="center"><a href="https://lastresolution.github.io/ReStart/">Webサイト（英語）</a> · <a href="https://lastresolution.github.io/ReStart/developers/">開発者向けガイド（英語）</a></p>

> **AI翻訳:** この案内は英語を基にAIを用いて翻訳・編集しており、誤りが含まれる場合があります。[英語README](README.md)を原本とし、翻訳の修正を歓迎します。スクリーンショットは全言語版で英語のデモ環境を使用しています。

<p align="center"><strong>60秒のデモを見る</strong><br><sub>1080p · 英語 · 音声なし</sub></p>

https://github.com/user-attachments/assets/a893cd20-8c82-4c47-b5fb-bf22743f67e1

**Windows 10 のフルスクリーンスタートが恋しい人へ。** ReStart は Windows 11 と Windows 10 で使えるデスクトップランチャーです。いつものアプリをピン留めし、画面を自由に並べ、JavaScript で自分のライブタイルを作れます。

**Windows 11 / 10 · JavaScript ライブタイル · x64 / ARM64 · ポータブル · 通常利用に管理者権限は不要**

[ライブタイルを見る](#ライブタイルで必要な情報を) · [データをつなぐ](#天気や見出しをアプリと一緒に) · [タイルを作る](#タイルを作る) · [ソースからビルド](#自分でビルドする)

## 安定性と個人用バックアップ

表示範囲外のライブタイルの一時停止と、ブラウザ障害からの自動復旧に対応しています。どちらも設定で切り替えられます。**設定 → 配置とデータ**から、配置・外観・タイル本体・制作中のタイル・背景をまとめてバックアップ／復元できます。保存済みの接続・認証情報・写真フォルダの指定・キャッシュは除外しますが、アプリ名・起動先パス・独自ソースや素材を含む個人用ファイルです。[バックアップと復旧の手順（英語）](docs/backups.md)。

## 配置とライブタイルをもっと自由に

全体またはグループ単位で配置をロックできます。Ctrl+Shift+Lで全体を切り替えます。Ctrl+Eで選択し、Ctrl+矢印でタイル、Ctrl+Shift+矢印でグループを移動、Alt+1〜4でサイズ変更、Ctrl+Gでグループ化。Shift+F10でメニューを開け、Undo/Redoも使えます。デスクトップとは別の背景画像も指定できます。

**設定 → 外観**で背景の濃さを**10〜100%**、背景画像のぼかしを**0〜50**で調整できます。ぼかしの初期値は**12**で、**0**にすると鮮明な画像になります。ハイコントラスト設定が優先されます。「**選択を終了**」または「**ピン留めタイル**」から、直接タイル画面に戻れます。

外観・タイル倍率・グループ幅・言語を保存前にプレビューできます。未保存で閉じると「保存して閉じる」「破棄して閉じる」「編集を続ける」を選べ、破棄すると元の表示に戻ります。明示的にダウンロードした更新は準備完了の案内が残り、次回起動にも引き継がれます。通常の起動だけでは自動適用しません。

HTML/CSS/JavaScript製のライブタイルを19種類同梱。Tile Studioでソースとmanifestを編集し、オフラインのサンプル値で4サイズを確認して保存・ピン留めできます。天気のCSSアート、システムダッシュボード、RSSニュース、Canvasガーデンを追加し、オービット時計の秒針はPC時刻で動きます。設定の「ライブタイル名を表示」は初期値ONで、左下の名前を非表示にもできます。

ドラッグ中はタイル・グループ・複数選択の半透明コピーがマウスに追従します。青い目印で移動先を示し、押し出されるタイルもドロップ前に配置をプレビューします。

![Tile Studio: HTML, CSS and JavaScript with four live previews](docs/images/tile-studio.png)

![Weather scene, System dashboard, News board and Canvas garden](docs/images/web-examples.png)

**エディター操作：** JavaScript・HTML・CSS・JSON のシンタックスハイライト、<kbd>Tab</kbd> / <kbd>Shift</kbd> + <kbd>Tab</kbd> によるインデント、**ドキュメントを整形 / Ctrl+Shift+F** によるオフライン整形に対応。整形は1回の Undo で戻せます。Feed headlines と News board はタイル内でスクロールし、閉じたフォルダには同じ大きさのライブプレビューを表示します。初期配置とリセットには同梱の Month dots と Orbit clock を使用。任意の起動時更新チェックで新バージョンを閉じられる通知として案内します。

## 使い始める

1. Intel / AMD の PC は **[x64 ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip)**、Windows on ARM は **[ARM64 ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-arm64.zip)** を保存します。
2. 使い続けたい場所に **ZIP 全体を展開**します。`ReStart.exe` と同じ場所に `TileHost`、`Examples`、`third-party` を残してください。
3. **ReStart.exe** を実行します。**Windows キー**で開いて、そのままアプリ名を入力すると検索できます。<kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>Space</kbd> と通知領域のメニューからも開けます。

歯車からテーマ、サインイン時の起動、Windows キー・Start ボタンとの連携、更新確認を設定できます。[リリースページ](https://github.com/LastResolution/ReStart/releases/latest)にチェックサムを添付しています。

| すぐにアプリへ | 配置を自分のものに | 必要な情報をひと目で |
| --- | --- | --- |
| Windows キー、タスクバーの Start、<kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>Space</kbd>、トレイから開いてそのまま検索。 | 4サイズのタイル、独立ピン、自由なグループ移動、インラインフォルダ、個別の色。 | 同梱19種類。グラフ、PC時刻、システム情報、写真、天気・RSS/Atom・Web API・ローカルファイル連携。 |

## 自分のスタートに

![アプリとライブタイルを表示する ReStart の開閉アニメーション](docs/images/start-motion.gif)

**既定はグループ、必要なときは自由配置。** 空き領域へドロップすると名前のないグループを作成し、タイルに重ねて待つとフォルダーを作成します。<kbd>Shift</kbd>を押しながらドラッグするとフォルダー化せず並べ替え、<kbd>Alt</kbd>では空き領域へ単独配置します。**設定 → スタートの動作 → タイルの自由配置を使う**で単独配置を既定にできます。既存の配置・フォルダーは維持します。移動先の判定を保ちながら、周囲のタイルが滑らかに移動します。

ドラッグ中は画面下部にショートカットのヒントを一時表示します。スタートの動作設定で無効にできます。<kbd>F1</kbd>、または設定の **キーボードショートカット** から一覧を確認できます。


- **自由に配置。** グループ間や空白へタイルを移動できます。グループは内容幅に合わせた長方形とし、ホバー時・ドラッグ中に境界を表示。重なり判定も同じ長方形に揃えます。
- **起動待ちでも検索。** 初回は進捗画面でアイコンとライブタイルを準備してから表示します。待機中に入力した検索語を引き継ぎ、Enterで強調表示した結果を起動できます。Escで取り消せます。
- **誤起動せずに整理。** 編集モードでタイルを選択してまとめて移動し、Undo / Redo で操作を戻したりやり直したりできます。
- **フォルダをその場で展開。** タイルに重ねて待つとフォルダーを作成します。開くと影響する隣接項目だけを移動し、閉じると元の位置へ戻ります。
- **ファイルもピン留め。** Explorer の **Pin to ReStart** からファイル・フォルダ・ショートカットを追加。通常は空きスペースに置き、メニューからグループも指定できます。
- **検索と起動を自分向けに。** アプリ・Windowsの設定・インデックス済みファイルを検索。最近・よく使う文書、サイドバーのフォルダー、任意のアプリ履歴を選べます。タイルごとの起動引数や管理者実行にも対応。**設定 → スタートの動作**で機能を個別に切り替えられます。[設定一覧](docs/start-behavior.md)。
- **ライブタイルをワンクリックで。** 背景やグループの右クリック → ライブタイルを追加、または設定から選択。ローカルライブラリに置いた自作パッケージも自動検出。4サイズに合わせた表示例も同梱します。
- **好みの外観。** ミッドナイト・オーシャン・フォレスト・ローズ・サンドの全5配色にライト・ダーク版を用意。基本テーマとは別に選べ、システム設定にも追従します。背景色・アクセント色を手入力すると、そのRGB値を維持するカスタム配色になります。文字・操作部品・縦横のスクロールバー・スプラッシュがまとめて追従します。ライト・ダーク・システム・ハイコントラスト、壁紙、タイルごとの名前と色も使えます。
- **アプリ一覧を更新。** 起動したままインストール・アンインストールしたアプリも、再表示時などにバックグラウンドで一覧へ反映します。
- **開閉の動き。** 背景とタイルを分け、下側からタイルを表示します。閉じる途中でも再表示でき、入力すると表示アニメーションを完了してすぐ操作できます。アプリと Windows のアニメーション設定に従います。
- **8言語に対応。** 英語、日本語、ドイツ語、フランス語、スペイン語、韓国語、簡体字・繁体字中国語。ライブタイルの既定名は言語に追従し、変更した名前は維持します。
- **更新を見通せる。** 単一起動、サインイン時の起動、設定移行のバックアップ、バージョン・ライセンス表示、APIキー不要の公開リリース確認。

スクリーンショットはデモ配置、サンプル計測値、単色の写真素材を使っています。実際のタイルは許可した機能からPCの情報を読み取ります。[ライブタイル API とスタイリング例](docs/javascript-tiles.md)を参照してください。

<details>
<summary>ライトテーマ・アプリ一覧を見る</summary>

![ライトテーマの全画面表示](docs/images/start-light.png)

デモデータでの表示：[アプリ一覧](docs/images/app-list.png)、[アプリフォルダ](docs/images/app-folders.png)、[グループ配置](docs/images/group-placement.png)、[インラインフォルダ](docs/images/inline-folder.png)。通常モードではこのPCのアプリ名とアイコンを読み込みます。

</details>

Microsoft とは独立したプロジェクトです。Explorer のパッチは行わず、解除可能な連携を使います。従来の UWP タイル通知、Windows Search 全体、Cortana は対象外です。対応アプリでは最近使った文書・よく使う文書を表示し、アプリの使用履歴は任意で有効にできます。[互換性](docs/fidelity.md)と[検証手順](docs/testing.md)を参照してください。

## 全機能一覧

| 分野 | 対応機能 |
| --- | --- |
| 開閉とWindows連携 | Windowsキー・スタートボタンの任意の置き換え、Ctrl+Alt+Space、トレイ、フォーカス取得、別アプリ・タスクバー操作で閉じる、画面に合わせた表示、多重起動防止 |
| 起動 | サインイン時の自動起動、バックグラウンド起動、テーマ対応の準備画面と先行入力、アイコンキャッシュ、アプリ登録情報の自動更新 |
| タイルとグループ | 小・中・横長・大の4サイズ、単独ピン、内容幅に合わせた長方形グループ、自由な配置、半セル単位の横位置、縦横スクロール |
| フォルダー | 重ねて待つと作成、行内展開、子タイルの追加・取り出し・並べ替え、その場での名前変更と解除。中身が1つでも維持 |
| 配置の編集 | 編集モード、複数選択と一括移動、選択・ピン留め解除、起動中のUndo/Redo、ドラッグ取消時の復元 |
| D&Dの表示 | グループ・フォルダー・配置先の区別、待ち時間の進捗、ヒント、ホバー時の範囲、自動端スクロール。重ねて作るグループ・フォルダーは個別に無効化可能 |
| ピン留め | アプリ、ファイル、ディレクトリ、ショートカット、Webサイト。Explorerやアプリ一覧・検索の右クリック、ドラッグ、追加先の明示指定 |
| 検索 | 全半角・かなを正規化したアプリ名、実行ファイル名・OSが宣言する別名で検索。例：`notepad`や`cmd`。翻訳名・英語名で探せるWindows設定15項目、任意のインデックス済みファイル名検索、矢印・Enter操作、古い問い合わせの取消 |
| すべてのアプリ | 画面幅に合わせたアルファベット順の列、スタートメニュー内フォルダーの展開、文字ジャンプ、キーボード操作、インストール・削除の反映、任意の最近追加・よく使うアプリ |
| アプリの操作 | Windowsが提供する管理者実行、ファイルの場所、プロパティ、アンインストール。Windowsの設定と公開APIの範囲で最近・よく使う文書を表示 |
| 起動オプション | タイルごとの有効・無効、追加引数、作業フォルダー、対応するデスクトップアプリ・ショートカットの管理者実行。UACの確認はWindowsが担当 |
| サイドバー | 任意のホバー展開、ドキュメント・ダウンロード・音楽・画像・動画・ネットワーク・個人用フォルダーの個別選択、アカウントと電源操作 |
| 外観 | 5種類のカラーテーマとライト・ダーク・システム設定、背景・アクセントのRGB指定、壁紙、スタートのアクセント色、旧式・個別タイル色、コントロール・両スクロールバー、ハイコントラスト、任意のアニメーション |
| ライブタイル | JS製19種類を同梱、4サイズ対応、クリック時のローカル操作、更新の有効・無効、データ接続設定、サンプル作成、パッケージ取込、ローカルライブラリの自動検出 |
| JS API | PC時刻、言語・サイズ・保持状態、CPU・メモリ・通信・バッテリー・ストレージ、選択フォルダーの写真、JSON・Web API・RSS・Atom・ローカルファイルのキャッシュとアプリ連携 |
| 多言語 | 英語、日本語、ドイツ語、フランス語、スペイン語、韓国語、簡体字・繁体字中国語。UIと同梱タイルに対応 |
| データと更新 | ローカル設定、配置の取込・書出し・初期化、バックアップとバージョン移行、消去可能なアプリ履歴、バージョン・ビルド・ライセンス、APIキー不要の更新確認、検証・復旧付きのポータブル更新、診断情報の確認とコピー |
| 配布 | Windows 10/11のx64・ARM64向けポータブルZIP、ランタイム・ライセンス同梱、通常利用は管理者権限不要、Explorerを書き換えない解除可能な連携 |

[ON/OFF設定と既定値](docs/start-behavior.md) · [対応範囲](docs/fidelity.md) · [JS API](docs/javascript-tiles.md)

**初期配置を使いやすく。** 初回起動と配置リセットでは、アプリIDと各言語の正確な名前を使って、用途別にインストール済みアプリを選びます。少ない場合は重複しないオフラインの同梱タイルで補い、各グループを最低6枚にします。保存済みの配置は維持します。[選定とリセットの詳細（英語）](docs/start-behavior.md#initial-layout-and-reset)。

**実測した検証範囲。** 100%／125%／150%の2画面と、縦・横画面が混在する配置でウィンドウの位置・寸法をネイティブ検証しています。アクセシビリティ情報は8言語・ライト／ダークで確認し、Narratorの基本的な読み上げ内容はスピーチの要約で確認しました。画面の再接続、物理タッチ、スクリーンリーダーでの操作全体は別途実機確認が必要です。[検証結果と対象環境（英語）](docs/fidelity.md#observed-platform-coverage)。

### 検索と起動の設定

![アプリ・Windows設定・ファイルを分類した検索画面全体](docs/images/unified-search.png)

<details>
<summary><strong>動作の切り替えとタイルごとの起動オプション</strong></summary>

| スタートの動作 | 起動オプション |
| --- | --- |
| ![動作のスイッチとサイドバーのフォルダー選択](docs/images/start-behavior.png) | ![追加引数・作業フォルダー・管理者実行](docs/images/launch-options.png) |

![配置操作・履歴・検索の切り替え](docs/images/start-behavior-layout.png)

設定・ファイル検索、文書メニュー、ホバー表示、重ねて作るグループ・フォルダー、端スクロール、ヒントを個別に無効化できます。「よく使うアプリ」の記録は既定で無効です。起動オプションも入力値を残して無効にできます。スクリーンショットは英語UIと架空のアプリ・文書を使っています。

</details>

## ライブタイルで必要な情報を

時刻、PC の状態、選んだ写真、天気や見出しをアプリと同じ画面に表示できます。**19種類を同梱**し、4サイズに合わせたレイアウトを用意しています。

![時計・システム情報・集中タイマー・写真の同梱タイル](docs/images/live-tiles.png)

- **時刻と集中：** オービット時計、デジタル時計、月のドットカレンダー、集中タイマー、今年の進捗、カウントダウン。
- **PC の状態：** CPU、メモリ、通信量、バッテリー、ストレージ。
- **写真：** 自分で選んだフォルダのスライドショー。クリックで次の写真へ進めます。

背景を右クリック → **ライブタイルを追加**から選べます。通常クリックはタイマーの一時停止や写真の切り替えなど、タイル内の操作に使います。

### 天気や見出しをアプリと一緒に

<p align="center"><img src="docs/images/connected-weather.png" width="204" alt="Weather web tile" /> <img src="docs/images/connected-feed.png" width="204" alt="Feed headlines web tile" /> <img src="docs/images/connected-json-monitor.png" width="204" alt="JSON monitor web tile" /></p>

| タイル | 表示するもの | 接続先の指定 |
| --- | --- | --- |
| **天気** | Open-Meteo の気温・天候・風速 | 表示したい場所の緯度・経度を設定 |
| **フィードの見出し** | RSS / Atom の見出し。クリックで次の項目へ | フィードの URL を入力 |
| **JSONモニター** | 自作アプリの状態・値・進捗 | Web API、localhost の接続先、または選択した JSON ファイル |

1. 背景を右クリック → **ライブタイルを追加** → 上の3種類から選びます。
2. **データソースを設定…**でアドレスを入力するかローカルファイルを選び、**保存**します。認証情報が必要な場合もこの画面で指定します。
3. 追加済みのタイルは右クリック → **データソースを設定…**から変更できます。**データを今すぐ更新**で新しい値を取得します。

Start の起動準備中と表示中に非同期で更新し、取得に失敗した場合は前回の値を保持します。非表示にすると取得を停止します。接続先と認証情報は現 Windows ユーザー向けに暗号化し、共有するレイアウトには含めません。パッケージを取り込んだだけでは接続しません。

**天気の初期設定はベルリンの例で、PC の地域ではありません。** 位置推定は行いません。[地域の変更手順](docs/javascript-tiles.md#weather-location)から座標と表示名を変更できます。上の画像は架空のローカルデータで描画しています。

自作アプリは既存の localhost エンドポイントや JSON ファイルで連携できます。[ファイル生成例](examples/integrations/write-tile-data.ps1)と[データ連携ガイド](docs/javascript-tiles.md#weather-feeds-web-apis-and-application-data)で、接続・セレクター・認証の指定を紹介しています。

## タイルを作る

パッケージは `manifest.json` と `tile.js` の2ファイルです。PC時刻、言語、タイルサイズ、クリック、保持状態を利用でき、能力を明示すると読み取り専用のシステム情報やホストで選んだ写真、明示的に設定した Web API / フィード / ローカルファイルも使えます。

`manifest.json` で読み取るデータに名前を付けます。

```json
{
  "version": 1,
  "title": "My service",
  "refreshSeconds": 1,
  "capabilities": ["data.sources"],
  "dataSources": {
    "service": { "format": "json", "refreshSeconds": 60, "jsonPointer": "/status" }
  },
  "settings": {}
}
```

`tile.js` は最新のスナップショットから表示を返します。

```js
function render(ctx) {
  const source = ctx.data?.service;
  return {
    text: source?.value?.text ?? "Waiting for data",
    detail: source?.stale ? "Showing cached data" : "",
    kicker: "My service",
    background: "#12344B", backgroundEnd: "#10202F",
    foreground: "#FFFFFF",
    layout: "centered"
  };
}
```

2ファイルを `%LOCALAPPDATA%\ReStart\TileLibrary` 内のサブフォルダへ置き、ギャラリーや右クリックから追加します。接続画面で `{"status":{"text":"Build passed"}}` を返す API またはファイルを選ぶと、`/status` で選択したオブジェクトが `ctx.data.service.value` に届きます。個人用 URL やキーは接続画面で指定し、パッケージには書きません。

描画は ReStart がネイティブで行います。[同梱サンプル](examples/tiles)にはシステム情報・写真・サイズごとの配置・クリック操作もあります。[作成ガイド](docs/javascript-tiles.md)と[API契約](docs/api-contracts.md)を参照してください。

## 使い方を共有する

使い方の質問、配置例、タイルのアイデアは [Discussions](https://github.com/LastResolution/ReStart/discussions) へ。[基本FAQ（英語）](https://github.com/LastResolution/ReStart/discussions/30)もあります。再現できる不具合は [Issue](https://github.com/LastResolution/ReStart/issues/new/choose) で報告してください。タイル、翻訳、アクセシビリティ、ランチャー本体への貢献も歓迎します。[開発参加ガイド](CONTRIBUTING.md)を参照してください。

<details>
<summary><strong>初回起動と対応環境</strong></summary>

1. [Releases](https://github.com/LastResolution/ReStart/releases) の Windows 用 ZIP をダウンロードします。
2. `ReStart-win-x64.zip` を保存したいフォルダーに展開します。ARM PC では `win-arm64` を選んでください。
3. `ReStart.exe` を起動します。管理者権限や .NET の別途インストールは不要です。
4. Windows キーを単押しすると ReStart が開きます。設定は左下の歯車です。

Windows 11 x64 が実機検証対象です。Windows 10 22H2 と ARM64 は対応ビルド対象ですが、実機検証は別途必要です。

初回はアプリの登録情報を読み取ります。アイコンはバックグラウンドで取得します。既存の Windows スタートや Explorer のファイルを書き換えることはありません。ReStart 終了時にキーの捕捉を解除します。

</details>

<details>
<summary><strong>キーボード・マウス操作</strong></summary>

| 操作 | 動作 |
| --- | --- |
| Windows キー単押し | 開く / 閉じる。設定で無効化可能 |
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>Space</kbd> | 開く / 閉じる |
| <kbd>Esc</kbd> | 検索やフォルダーを閉じる。もう一度で ReStart を閉じる |
| 文字入力 / <kbd>Ctrl</kbd> + <kbd>F</kbd> | アプリと、有効にしたWindows設定・ファイル名を検索 |
| 検索欄で <kbd>Enter</kbd> | 強調表示した結果を起動。`↓` / `↑`で選択を変更 |
| <kbd>Tab</kbd> / 矢印 / <kbd>Enter</kbd> | フォーカス移動 / 選択項目の起動 |
| タイルを右クリック | サイズ、名前、移動先、フォルダー、解除 |
| タイルをドラッグ | グループ内・グループ間で移動 |
| タイルを空き領域へドロップ | 名前のないグループを作成 |
| アプリ一覧・検索結果からドラッグ | タイル画面へ切り替えて配置。キャンセルで元の表示へ復帰 |
| <kbd>Alt</kbd>を押しながら空き領域へドラッグ | 今回だけ単独配置 |
| <kbd>Shift</kbd>を押しながらドラッグ | 重なってもグループ・フォルダーにせず、周囲のタイルを押し出して並べ替え |
| タイルに重ねて約0.6秒待ち、フォルダーのプレビューでドロップ | フォルダーを作成。既存フォルダーなら中に追加 |
| サイドバー・背景の右クリックの「タイルを選択」 / <kbd>Ctrl</kbd> + <kbd>E</kbd> | アプリを起動せずに複数選択。通常時もタイルやグループを直接ドラッグできます |
| 編集中の <kbd>Ctrl</kbd> + <kbd>A</kbd> / <kbd>Delete</kbd> | ルートタイルをすべて選択 / 選択したタイルをピン留め解除 |
| <kbd>Ctrl</kbd> + <kbd>Z</kbd> / <kbd>Ctrl</kbd> + <kbd>Y</kbd> または <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Z</kbd> | 配置を元に戻す / やり直す。文字入力欄では通常の文字編集の取り消し |
| グループ名をダブルクリック / <kbd>Enter</kbd> / <kbd>F2</kbd> | その場で名前を編集。<kbd>Enter</kbd>で確定、<kbd>Esc</kbd>で取消 |
| グループ名をドラッグ | グループの列・縦位置を移動。空白も保持 |
| 背景の空白を右クリック | グループやカスタムタイルを追加 |
| アプリ一覧を右クリック | スタートにピン留め / 解除。Windowsが提供する「その他」やアンインストール |
| 右クリック → ReStart | 名前・配置などの追加操作。アプリのピン留め先も選択可能 |
| 一覧の文字見出しをクリック | 文字を選んで移動 |
| 一覧のフォルダーをクリック / <kbd>Space</kbd> | 内容を開閉。`→`で開く、`←`で閉じる |
| 一覧にフォーカス中の英数字 | 同じ頭文字の表示行へ移動。検索は<kbd>Ctrl</kbd> + <kbd>F</kbd> |
| 待機中の検索欄で`↓` | 一覧またはタイルへフォーカスを移す |

`Win+E` などは Windows 側に渡します。キーフックとの組み合わせは、IME・常駐アプリ・管理者アプリなど環境ごとの検証が必要です。困ったときは通知領域から終了するか、設定で Windows キーの捕捉を無効にしてください。

編集モードには「グループへ移動…」「選択したタイルを外す」「完了」があります。ドラッグでは相対位置を保ち、グループへの移動メニューではコンパクトに並べます。重ねて待つと、1枚でも複数選択でもフォルダーを作成し、既存フォルダーへ追加できます。自由配置モードでは、単独タイルを重ねて通常のグループにまとめる機能も選べます。Shiftを押しながらドラッグすると重ねた際の動作を抑えて並べ替えます。フォルダー内を編集するときは右クリックの「フォルダを開く」を使います。中身が1つでもフォルダーを維持し、最後のタイルを出すと削除します。<kbd>Esc</kbd> は選択解除、もう一度で編集終了。Startを開き直すと通常モードです。配置履歴は起動中のみ、100操作・32 MiB の範囲で保持し、設定やディスク上の履歴には含めません。ピン留め、移動、サイズ、名前、色、フォルダー、インポート、初期化を操作単位で戻せます。テーマなどの設定は巻き戻しません。

</details>

<details>
<summary><strong>データ・プライバシー・更新</strong></summary>

設定・レイアウトは `%LOCALAPPDATA%\ReStart\layout.json` に保存します。直前の保存が `layout.json.bak` に残ります。壊れたファイルは保全し、利用可能ならバックアップを読み込みます。将来の形式を古い ReStart で読み込んだ場合は上書きを禁止します。

設定の形式は schema 4 です。旧 schema 1～3 と同じ形式内のアプリ更新で原本バックアップを残し、設定 → レイアウトとデータに移行記録を表示します。旧形式の名前なし・1個だけのグループは単独タイルへ移行します。旧版へ戻す場合は[移行と復元](docs/migration.md)に従ってください。

設定 → 更新でAPIキー不要の公開リリース確認を行い、「更新をダウンロード」→検証後に「再起動して更新」を選べます。完全で未改変のポータブルパッケージと書き込み可能なフォルダが必要で、インストールや起動に失敗した場合は以前のファイルへ戻します。開発ビルドや古いパッケージは引き続き手動更新できます。設定 → 情報 →「診断情報」で、パス・アプリ一覧・タイルのデータ・認証情報・生ログを含めないレポートを確認してコピーできます。自動送信は行いません。[更新と診断情報（英語）](docs/updates.md)を参照してください。

横スクロールと Shift＋ホイールに対応し、端へドラッグした場合だけ作業領域を広げます。[全体の動作確認](docs/fidelity.md)で、本家に基づく動作・要望による拡張・未検証を整理しています。

レイアウトにはアプリ名、起動先、ファイルやフォルダーのパスが含まれます。エクスポートする際は共有内容を確認してください。インポートしても自動起動やキーフックの設定は引き継ぎません。

アンインストーラーは不要です。設定でサインイン時の起動をオフにし、通知領域から ReStart を終了して、展開したフォルダーを削除してください。設定も消す場合は上記のデータフォルダーを削除します。

ReStart に広告・利用状況の解析はありません。更新確認は GitHub API へアプリのバージョンを付けて問い合わせます。認証・アプリ一覧・配置・写真フォルダを送りません。データ接続を設定した場合、その接続先にはリクエストと明示したパラメーター・資格情報が届きます。クリックした Web サイトや外部アプリは、それぞれの動作に従います。

</details>

## 自分でビルドする

<details>
<summary><strong>Release ビルドとポータブルパッケージのコマンド</strong></summary>

必要なものは Windows と [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) です。SDK は `global.json` に固定し、プレビュー版を使いません。JavaScript 実行用の別プロセスに Jint を使用します。テストには xUnit を使います。

```powershell
git clone https://github.com/LastResolution/ReStart.git
cd ReStart
dotnet restore ReStart.slnx --locked-mode
dotnet build ReStart.slnx -c Release --no-restore
dotnet test ReStart.slnx -c Release --no-build
dotnet run --project src/ReStart.App -c Release
```

常駐フックを有効にせず、デモ用データで UI を確認するには:

```powershell
dotnet run --project src/ReStart.App -c Release -- --preview
```

表示・シェル列挙・アイコン取得を検証して PNG と結果 JSON を保存するには:

```powershell
./scripts/smoke.ps1
```

配布用 ZIP と SHA-256 を作るには:

```powershell
./scripts/package.ps1 -Runtime win-x64
# ./scripts/package.ps1 -Runtime win-arm64
```

通常のReleaseビルドと起動は次のとおりです。出力先の実行ファイルを使用中の場合は、通知領域からReStartを終了してからビルドしてください。

```powershell
dotnet build ReStart.slnx -c Release
.\src\ReStart.App\bin\Release\net10.0-windows\ReStart.exe
```

このビルドは開発PCの.NETランタイムを使用します。配布用の `package.ps1` はランタイムを同梱し、`artifacts/packages/ReStart-win-x64.zip` または `ReStart-win-arm64.zip` を出力します。ZIP全体を展開し、`TileHost`・`Examples` も一緒に置いてください。

生成物は `artifacts/` に作られ、Git の対象外です。プレビューのデータも通常の設定とは分離されます。

</details>

## リポジトリの運用

- `main` を配布可能な基準にし、`feat/*`・`fix/*`・`docs/*` の短いブランチで作業します。
- 変更理由ごとにコミットし、Pull Request に動作・検証・制限を記載します。
- Windows CI でビルドとテストを実行してから、Squash merge で `main` に取り込みます。
- `v*` タグで x64 / ARM64 の配布物とチェックサムを作成します。v1.0 以降は公開安定版と英語リリースノートを使います。
- Actions の権限を絞り、外部 Actions はコミット SHA に固定します。
- Dependabot が NuGet と GitHub Actions の更新を提案します。

詳しくは [開発への参加](CONTRIBUTING.md)、[アーキテクチャ](docs/architecture.md)、[検証方法](docs/testing.md)、[リリース手順](docs/releasing.md) を参照してください。

バージョンごとの変更は [GitHub Releases](https://github.com/LastResolution/ReStart/releases) に英語で記載します。

## 参考とライセンス

参考にした Microsoft の一次資料は [互換性](docs/fidelity.md#reference-material) に記載しています。Microsoft のバイナリ、旧 Start のコードや画像は同梱していません。Windows および各アプリの名称・アイコンは各権利者に帰属します。ReStart は Microsoft とは独立したプロジェクトです。

ReStart のコードは [MIT License](LICENSE) です。[サードパーティーのライセンスと同梱表記（英語）](docs/third-party.md)も参照してください。

[セキュリティ](SECURITY.md) · [サポート](SUPPORT.md) · [英語表記でのブランチ保護](docs/github-protection.md)

---

<!-- support:start -->
<h2 align="center">個人開発を応援する</h2>
<p align="center">
  ReStartが気に入ったら、LastResolutionによるReStartの改善や、これからの便利なツールづくりを応援できます。<br>
  <sub>ReStartは無料のオープンソースソフトウェアです。支援は任意です。</sub>
</p>
<p align="center">
  <a href="https://buymeacoffee.com/LastResolution"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" height="48" alt="Buy Me a Coffee · LastResolution" /></a>
</p>
<!-- support:end -->
