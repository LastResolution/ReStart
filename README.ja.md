<p align="center"><img src="assets/ReStart.png" width="88" alt="ReStart" /></p>
<h1 align="center">ReStart</h1>
<p align="center"><strong>フルスクリーンのスタートを、もう一度。</strong><br>いつものアプリと、自分で作るライブタイルのための場所。</p>
<p align="center">
  <a href="https://github.com/LastResolution/ReStart/releases/latest">ダウンロード</a> ·
  <a href="docs/ja/javascript-tiles.md">タイルを作る</a> ·
  <a href="CONTRIBUTING.ja.md">開発に参加</a> ·
  <a href="README.md">English</a>
</p>
<p align="center">
  <a href="https://github.com/LastResolution/ReStart/actions/workflows/ci.yml"><img src="https://github.com/LastResolution/ReStart/actions/workflows/ci.yml/badge.svg" alt="Windows CI" /></a>
  <a href="https://github.com/LastResolution/ReStart/releases/latest"><img src="https://img.shields.io/github/v/release/LastResolution/ReStart?color=0078d4" alt="Latest release" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-0078d4" alt="MIT license" /></a>
</p>

![ReStart が描画した12種類の同梱ライブタイル](docs/images/live-tiles.png)

ReStart は、Windows 10 のフルスクリーンスタートを目指す独立した Windows デスクトップランチャーです。アプリを並べ、フォルダをその場で開き、文字を打って検索。JavaScript のライブタイルで、自分のための画面にできます。

**Windows 11 / 10 · x64 / ARM64 · ポータブル · 通常利用に管理者権限は不要**

| すぐにアプリへ | 配置を自分のものに | 必要な情報をひと目で |
| --- | --- | --- |
| Windows キー、タスクバーの Start、`Ctrl+Alt+Space`、トレイから開いてそのまま検索。 | 4サイズのタイル、独立ピン、自由なグループ移動、インラインフォルダ、個別の色。 | 同梱12種類。グラフ、PC時刻、読み取り専用のシステム情報、ローカル写真のスライドショー。 |

## 自分のスタートに

- **自由に配置。** グループ間や空白へタイルを移動できます。グループは内容幅に合わせた長方形とし、ホバー時・ドラッグ中に境界を表示。重なり判定も同じ長方形に揃えます。
- **フォルダをその場で展開。** タイル中央でホバーしてフォルダを作成。開くと影響する隣接項目だけを移動し、閉じると元の位置へ戻ります。
- **ファイルもピン留め。** Explorer の **Pin to ReStart** からファイル・フォルダ・ショートカットを追加。通常は空きスペースに置き、メニューからグループも指定できます。
- **ライブタイルをワンクリックで。** 背景やグループの右クリック → ライブタイルを追加、または設定から選択。ローカルライブラリに置いた自作パッケージも自動検出。4サイズに合わせた表示例も同梱します。
- **好みの外観。** ダーク・ライト・システム・ハイコントラスト、壁紙、アクセント、タイルごとの名前と色。縦横のスクロールバーもテーマに追従します。
- **8言語に対応。** 英語、日本語、ドイツ語、フランス語、スペイン語、韓国語、簡体字・繁体字中国語。ライブタイルの既定名は言語に追従し、変更した名前は維持します。
- **更新を見通せる。** 単一起動、サインイン時の起動、設定移行のバックアップ、バージョン・ライセンス表示、APIキー不要の公開リリース確認。

上のギャラリーはサンプルの計測値と単色の写真素材で描画しています。実際のタイルは許可した機能からPCの情報を読み取ります。[ライブタイル API とスタイリング例](docs/ja/javascript-tiles.md)を参照してください。

<details>
<summary>ランチャーとアプリ一覧を見る</summary>

![ReStart のピン留め画面](docs/images/start.png)

デモデータでの表示：[アプリ一覧](docs/images/app-list.png)、[アプリフォルダ](docs/images/app-folders.png)、[グループ配置](docs/images/group-placement.png)、[インラインフォルダ](docs/images/inline-folder.png)。通常モードではこのPCのアプリ名とアイコンを読み込みます。

</details>

Microsoft とは独立したプロジェクトです。Explorer のパッチは行わず、解除可能な連携を使います。従来の UWP タイル通知、Windows Search 全体、Cortana、ジャンプリスト、使用頻度による推薦は対象外です。[動作の調査](docs/ja/behavior-audit.md)と[検証記録](docs/ja/validation.md)に根拠と実行済みの確認を記載しています。

## 使い始める

1. [Releases](https://github.com/LastResolution/ReStart/releases) の Windows 用 ZIP をダウンロードします。
2. `ReStart-win-x64.zip` を保存したいフォルダーに展開します。ARM PC では `win-arm64` を選んでください。
3. `ReStart.exe` を起動します。管理者権限や .NET の別途インストールは不要です。
4. Windows キーを単押しすると ReStart が開きます。設定は左下の歯車です。

ZIP 配布は未署名です。組織の実行ポリシーに従ってください。Windows 11 x64 が実機検証対象です。Windows 10 22H2 と ARM64 は対応ビルド対象ですが、実機検証は別途必要です。

初回はアプリの登録情報を読み取ります。アイコンはバックグラウンドで取得します。既存の Windows スタートや Explorer のファイルを書き換えることはありません。ReStart 終了時にキーの捕捉を解除します。

## 操作

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
| 別のタイルの中央で約1秒保持してドロップ | フォルダーにまとめる |
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

## データと削除

設定・レイアウトは `%LOCALAPPDATA%\ReStart\layout.json` に保存します。直前の保存が `layout.json.bak` に残ります。壊れたファイルは保全し、利用可能ならバックアップを読み込みます。将来の形式を古い ReStart で読み込んだ場合は上書きを禁止します。

v0.6 は schema 4 です。旧 schema 1～3 と同じ形式内のアプリ更新で原本バックアップを残し、設定 → レイアウトとデータに移行記録を表示します。旧形式の名前なし・1個だけのグループは単独タイルへ移行します。旧版へ戻す場合は[移行と復元](docs/ja/migration.md)に従ってください。

設定 → 情報で版・ビルド・ライセンス全文、設定 → 更新で API キー不要の公開リリース確認を使えます。自動インストールは行いません。[更新チェック](docs/ja/updates.md)を参照してください。

横スクロールと Shift＋ホイールに対応し、端へドラッグした場合だけ作業領域を広げます。[全体の動作確認](docs/ja/behavior-audit.md)で、本家に基づく動作・要望による拡張・未検証を整理しています。

レイアウトにはアプリ名、起動先、ファイルやフォルダーのパスが含まれます。エクスポートする際は共有内容を確認してください。インポートしても自動起動やキーフックの設定は引き継ぎません。

アンインストーラーは不要です。設定でサインイン時の起動をオフにし、通知領域から ReStart を終了して、展開したフォルダーを削除してください。設定も消す場合は上記のデータフォルダーを削除します。

ReStart に広告・利用状況の解析はありません。更新確認は GitHub API へアプリのバージョンを付けて問い合わせます。認証・アプリ一覧・配置・写真フォルダを送りません。クリックした Web サイトや外部アプリは、それぞれの動作に従います。

## ソースから開発する

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

## リポジトリの運用

- `main` を配布可能な基準にし、`feat/*`・`fix/*`・`docs/*` の短いブランチで作業します。
- 変更理由ごとにコミットし、Pull Request に動作・検証・制限を記載します。
- Windows CI でビルドとテストを実行してから、Squash merge で `main` に取り込みます。
- `v*` タグで x64 / ARM64 の配布物とチェックサムを作成します。v0.6 以降は公開安定版と英語リリースノートを使います。
- Actions の権限を絞り、外部 Actions はコミット SHA に固定します。
- Dependabot が NuGet と GitHub Actions の更新を提案します。

詳しくは [開発への参加](CONTRIBUTING.ja.md)、[アーキテクチャ](docs/ja/architecture.md)、[検証方法](docs/ja/testing.md)、[リリース手順](docs/ja/releasing.md) を参照してください。

バージョンごとの変更は [CHANGELOG](CHANGELOG.ja.md)、実施済みの確認は [検証記録](docs/ja/validation.md) にまとめています。

## 参考とライセンス

参考にした Microsoft の一次資料は [調査記録](docs/ja/research.md) にまとめています。Microsoft のバイナリ、旧 Start のコードや画像は同梱していません。Windows および各アプリの名称・アイコンは各権利者に帰属します。ReStart は Microsoft とは独立したプロジェクトです。

ReStart のコードは [MIT License](LICENSE) です。

[セキュリティ](SECURITY.ja.md) · [サポート](SUPPORT.ja.md) · [英語表記でのブランチ保護](docs/ja/github-protection.md)
