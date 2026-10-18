<p align="center"><img src="assets/ReStart.png" width="96" alt="ReStart" /></p>
<h1 align="center">ReStart</h1>
<p align="center"><strong>フルスクリーンのスタートを、もう一度。</strong><br>いつものアプリと、自分で作るライブタイルのための場所。</p>
<p align="center">
  <a href="https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip"><strong>Windows 版をダウンロード · x64</strong></a> ·
  <a href="https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-arm64.zip">ARM64</a> ·
  <a href="https://github.com/LastResolution/ReStart/releases/latest">Release notes</a> ·
  <a href="docs/ja/javascript-tiles.md">タイルを作る</a> ·
  <a href="CONTRIBUTING.ja.md">開発に参加</a> ·
  <a href="README.md">English</a>
</p>
<p align="center">
  <a href="https://github.com/LastResolution/ReStart/actions/workflows/ci.yml"><img src="https://github.com/LastResolution/ReStart/actions/workflows/ci.yml/badge.svg" alt="Windows CI" /></a>
  <a href="https://github.com/LastResolution/ReStart/releases/latest"><img src="https://img.shields.io/github/v/release/LastResolution/ReStart?color=0078d4" alt="Latest release" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-0078d4" alt="MIT license" /></a>
</p>

![アプリ・グループ・フォルダ・ライブタイルを含む全画面の ReStart](docs/images/start.png)

**Windows 10 のフルスクリーンスタートが恋しい人へ。** ReStart は Windows 11 と Windows 10 で使えるデスクトップランチャーです。いつものアプリをピン留めし、画面を自由に並べ、JavaScript で自分のライブタイルを作れます。

**Windows 11 / 10 · x64 / ARM64 · ポータブル · 通常利用に管理者権限は不要**

## 使い始める

1. Intel / AMD の PC は **[x64 ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip)**、Windows on ARM は **[ARM64 ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-arm64.zip)** を保存します。
2. 使い続けたい場所に **ZIP 全体を展開**します。`ReStart.exe` と同じ場所に `TileHost`、`Examples`、`third-party` を残してください。
3. **ReStart.exe** を実行します。**Windows キー**で開いて、そのままアプリ名を入力すると検索できます。**Ctrl+Alt+Space** と通知領域のメニューからも開けます。

歯車からテーマ、サインイン時の起動、Windows キー・Start ボタンとの連携、更新確認を設定できます。配布物は未署名で、[リリースページ](https://github.com/LastResolution/ReStart/releases/latest)にチェックサムを添付しています。

| すぐにアプリへ | 配置を自分のものに | 必要な情報をひと目で |
| --- | --- | --- |
| Windows キー、タスクバーの Start、`Ctrl+Alt+Space`、トレイから開いてそのまま検索。 | 4サイズのタイル、独立ピン、自由なグループ移動、インラインフォルダ、個別の色。 | 同梱12種類。グラフ、PC時刻、読み取り専用のシステム情報、ローカル写真のスライドショー。 |

## 自分のスタートに

![アプリとライブタイルを表示する ReStart の開閉アニメーション](docs/images/start-motion.gif)

- **自由に配置。** グループ間や空白へタイルを移動できます。グループは内容幅に合わせた長方形とし、ホバー時・ドラッグ中に境界を表示。重なり判定も同じ長方形に揃えます。
- **フォルダをその場で展開。** グループ内のタイルに重ねて待つとフォルダを作成。単独タイルに重ねるとグループを作成します。開くと影響する隣接項目だけを移動し、閉じると元の位置へ戻ります。
- **ファイルもピン留め。** Explorer の **Pin to ReStart** からファイル・フォルダ・ショートカットを追加。通常は空きスペースに置き、メニューからグループも指定できます。
- **ライブタイルをワンクリックで。** 背景やグループの右クリック → ライブタイルを追加、または設定から選択。ローカルライブラリに置いた自作パッケージも自動検出。4サイズに合わせた表示例も同梱します。
- **好みの外観。** ダーク・ライト・システム・ハイコントラスト、壁紙、アクセント、タイルごとの名前と色。縦横のスクロールバーもテーマに追従します。
- **開閉の動き。** 背景とタイルを分け、下側からタイルを表示します。閉じる途中でも再表示でき、入力すると表示アニメーションを完了してすぐ操作できます。アプリと Windows のアニメーション設定に従います。
- **8言語に対応。** 英語、日本語、ドイツ語、フランス語、スペイン語、韓国語、簡体字・繁体字中国語。ライブタイルの既定名は言語に追従し、変更した名前は維持します。
- **更新を見通せる。** 単一起動、サインイン時の起動、設定移行のバックアップ、バージョン・ライセンス表示、APIキー不要の公開リリース確認。

スクリーンショットはデモ配置、サンプル計測値、単色の写真素材を使っています。実際のタイルは許可した機能からPCの情報を読み取ります。[ライブタイル API とスタイリング例](docs/ja/javascript-tiles.md)を参照してください。

<details>
<summary>ライトテーマ・ライブタイル・アプリ一覧を見る</summary>

![ライトテーマの全画面表示](docs/images/start-light.png)

![同梱ライブタイル](docs/images/live-tiles.png)

デモデータでの表示：[アプリ一覧](docs/images/app-list.png)、[アプリフォルダ](docs/images/app-folders.png)、[グループ配置](docs/images/group-placement.png)、[インラインフォルダ](docs/images/inline-folder.png)。通常モードではこのPCのアプリ名とアイコンを読み込みます。

</details>

Microsoft とは独立したプロジェクトです。Explorer のパッチは行わず、解除可能な連携を使います。従来の UWP タイル通知、Windows Search 全体、Cortana、ジャンプリスト、使用頻度による推薦は対象外です。[互換性](docs/ja/fidelity.md)と[検証手順](docs/ja/testing.md)を参照してください。

## 自作タイルとコミュニティ

パッケージは `manifest.json` と `tile.js` の2ファイルです。PC時刻、言語、タイルサイズ、クリック、保持状態を利用でき、能力を明示すると読み取り専用のシステム情報やホストで選んだ写真も使えます。

`%LOCALAPPDATA%\ReStart\TileLibrary` 内のサブフォルダへ置くと自動検出します。[同梱サンプル](examples/tiles)、[作成ガイド](docs/ja/javascript-tiles.md)、[API契約](docs/ja/api-contracts.md)から始められます。

配置例、タイルのアイデア、不具合は [Issue](https://github.com/LastResolution/ReStart/issues/new/choose) へ。タイル、翻訳、アクセシビリティ、ランチャー本体への貢献も歓迎します。[開発参加ガイド](CONTRIBUTING.ja.md)を参照してください。

<details>
<summary><strong>初回起動と対応環境</strong></summary>

1. [Releases](https://github.com/LastResolution/ReStart/releases) の Windows 用 ZIP をダウンロードします。
2. `ReStart-win-x64.zip` を保存したいフォルダーに展開します。ARM PC では `win-arm64` を選んでください。
3. `ReStart.exe` を起動します。管理者権限や .NET の別途インストールは不要です。
4. Windows キーを単押しすると ReStart が開きます。設定は左下の歯車です。

ZIP 配布は未署名です。組織の実行ポリシーに従ってください。Windows 11 x64 が実機検証対象です。Windows 10 22H2 と ARM64 は対応ビルド対象ですが、実機検証は別途必要です。

初回はアプリの登録情報を読み取ります。アイコンはバックグラウンドで取得します。既存の Windows スタートや Explorer のファイルを書き換えることはありません。ReStart 終了時にキーの捕捉を解除します。

</details>

<details>
<summary><strong>キーボード・マウス操作</strong></summary>

| 操作 | 動作 |
| --- | --- |
| Windows キー単押し | 開く / 閉じる。設定で無効化可能 |
| `Ctrl+Alt+Space` | 開く / 閉じる |
| `Esc` | 検索やフォルダーを閉じる。もう一度で ReStart を閉じる |
| 文字入力 / `Ctrl+F` | アプリを検索 |
| 検索欄で `Enter` | 先頭の結果を起動 |
| `Tab` / 矢印 / `Enter` | フォーカス移動 / 選択項目の起動 |
| タイルを右クリック | サイズ、名前、移動先、フォルダー、解除 |
| タイルをドラッグ | グループ内・グループ間で移動 |
| タイルを空き領域へドロップ | その位置に単独タイルとして配置 |
| アプリ一覧・検索結果からドラッグ | タイル画面へ切り替えて配置。キャンセルで元の表示へ復帰 |
| 単独タイルに重ねて約1秒待ち、「グループを作成」でドロップ | 通常のグループにまとめる |
| グループ内のタイルに重ねて約1秒待ち、フォルダーのプレビューでドロップ | フォルダーを作成。既存フォルダーなら中に追加 |
| 「配置を編集」 / `Ctrl+E` | クリックでタイルを選択。アプリを起動せず、選択したタイルをまとめてドラッグ |
| 編集中の `Ctrl+A` / `Delete` | ルートタイルをすべて選択 / 選択したタイルをピン留め解除 |
| `Ctrl+Z` / `Ctrl+Y` または `Ctrl+Shift+Z` | 配置を元に戻す / やり直す。文字入力欄では通常の文字編集の取り消し |
| グループ名をダブルクリック / `Enter` / `F2` | その場で名前を編集。`Enter`で確定、`Esc`で取消 |
| グループ名をドラッグ | グループの列・縦位置を移動。空白も保持 |
| 背景の空白を右クリック | グループやカスタムタイルを追加 |
| アプリ一覧を右クリック | スタートにピン留め / 解除。Windowsが提供する「その他」やアンインストール |
| 右クリック → ReStart | 名前・配置などの追加操作。アプリのピン留め先も選択可能 |
| 一覧の文字見出しをクリック | 文字を選んで移動 |
| 一覧のフォルダーをクリック / `Space` | 内容を開閉。`→`で開く、`←`で閉じる |
| 一覧にフォーカス中の英数字 | 同じ頭文字の表示行へ移動。検索は`Ctrl+F` |
| 待機中の検索欄で`↓` | 一覧またはタイルへフォーカスを移す |

`Win+E` などは Windows 側に渡します。キーフックとの組み合わせは、IME・常駐アプリ・管理者アプリなど環境ごとの検証が必要です。困ったときは通知領域から終了するか、設定で Windows キーの捕捉を無効にしてください。

編集モードには「グループへ移動…」「選択したタイルを外す」「完了」があります。ドラッグでは相対位置を保ち、グループへの移動メニューではコンパクトに並べます。単独の通常タイルに重ねると選択をコンパクトなグループにまとめます。グループ内では1枚の重ね合わせでフォルダーを作成でき、複数選択は既存フォルダーへのまとめ入れに対応します。フォルダー内を編集するときは右クリックの「フォルダを開く」を使います。中身が1つでもフォルダーを維持し、最後のタイルを出すと削除します。`Esc` は選択解除、もう一度で編集終了。Startを開き直すと通常モードです。配置履歴は起動中のみ、100操作・32 MiB の範囲で保持し、設定やディスク上の履歴には含めません。ピン留め、移動、サイズ、名前、色、フォルダー、インポート、初期化を操作単位で戻せます。テーマなどの設定は巻き戻しません。

</details>

<details>
<summary><strong>データ・プライバシー・更新</strong></summary>

設定・レイアウトは `%LOCALAPPDATA%\ReStart\layout.json` に保存します。直前の保存が `layout.json.bak` に残ります。壊れたファイルは保全し、利用可能ならバックアップを読み込みます。将来の形式を古い ReStart で読み込んだ場合は上書きを禁止します。

v1.0 は schema 4 です。旧 schema 1～3 と同じ形式内のアプリ更新で原本バックアップを残し、設定 → レイアウトとデータに移行記録を表示します。旧形式の名前なし・1個だけのグループは単独タイルへ移行します。旧版へ戻す場合は[移行と復元](docs/ja/migration.md)に従ってください。

設定 → 情報で版・ビルド・ライセンス全文、設定 → 更新で API キー不要の公開リリース確認を使えます。自動インストールは行いません。[更新チェック](docs/ja/updates.md)を参照してください。

横スクロールと Shift＋ホイールに対応し、端へドラッグした場合だけ作業領域を広げます。[全体の動作確認](docs/ja/fidelity.md)で、本家に基づく動作・要望による拡張・未検証を整理しています。

レイアウトにはアプリ名、起動先、ファイルやフォルダーのパスが含まれます。エクスポートする際は共有内容を確認してください。インポートしても自動起動やキーフックの設定は引き継ぎません。

アンインストーラーは不要です。設定でサインイン時の起動をオフにし、通知領域から ReStart を終了して、展開したフォルダーを削除してください。設定も消す場合は上記のデータフォルダーを削除します。

ReStart に広告・利用状況の解析はありません。更新確認は GitHub API へアプリのバージョンを付けて問い合わせます。認証・アプリ一覧・配置・写真フォルダを送りません。クリックした Web サイトや外部アプリは、それぞれの動作に従います。

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

詳しくは [開発への参加](CONTRIBUTING.ja.md)、[アーキテクチャ](docs/ja/architecture.md)、[検証方法](docs/ja/testing.md)、[リリース手順](docs/ja/releasing.md) を参照してください。

バージョンごとの変更は [GitHub Releases](https://github.com/LastResolution/ReStart/releases) に英語で記載します。

## 参考とライセンス

参考にした Microsoft の一次資料は [互換性](docs/fidelity.md#reference-material) に記載しています。Microsoft のバイナリ、旧 Start のコードや画像は同梱していません。Windows および各アプリの名称・アイコンは各権利者に帰属します。ReStart は Microsoft とは独立したプロジェクトです。

ReStart のコードは [MIT License](LICENSE) です。

[セキュリティ](SECURITY.ja.md) · [サポート](SUPPORT.ja.md) · [英語表記でのブランチ保護](docs/ja/github-protection.md)
