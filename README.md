<p align="center"><img src="assets/ReStart.png" width="88" alt="ReStart" /></p>

<h1 align="center">ReStart</h1>

<p align="center">全画面のスタートを、もう一度。</p>

<p align="center">
  <a href="https://github.com/LastResolution/ReStart/actions/workflows/ci.yml"><img src="https://github.com/LastResolution/ReStart/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <img src="https://img.shields.io/badge/Windows-11%20%2F%2010-0078D7" alt="Windows 11 / 10" />
  <img src="https://img.shields.io/badge/.NET-10-512BD4" alt=".NET 10" />
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-green" alt="MIT" /></a>
</p>

ReStart は、Windows 10 の「全画面表示のスタート」を Windows 11 で使うためのデスクトップランチャーです。左の操作バー、画面に広がるタイル、名前付きグループ、全アプリ一覧を、Windows ネイティブの WPF で独立実装しています。

**現在は v0.2 の開発・検証段階です。Windows 10 の内部実装を移植したものではなく、完全互換を達成したという意味ではありません。** 調査した動作、実装状況、既知の差分は [再現仕様](docs/fidelity.md) で管理します。

![ReStart のピン留めタイル画面](docs/images/start.png)

画面はデモ用アプリ名を使った ReStart のプレビューです。通常起動では、この PC のアプリとアイコンが表示されます。

[全アプリ一覧のプレビュー](docs/images/app-list.png)は、架空のアプリを使った横幅1920 DIPの配置検証です。

## できること

- タスクバーを残した全画面表示。マウスポインターのあるモニターに開きます。
- Windows キー単押し、`Ctrl+Alt+Space`、通知領域のアイコンで表示・非表示。
- 左のバーでピン留めタイルと「すべてのアプリ」を切り替え。開き直すとタイル画面の先頭へ戻ります。
- 全アプリ一覧の列数・列幅を画面幅に合わせ、右側の表示領域まで使います。
- インストール済みの Win32 / Store アプリとスタートメニューのショートカットを列挙し、実際のアプリを起動。
- 小・中・横長・大の4サイズ、名前付きグループ、タイルとグループの並べ替え。
- フォルダー作成、フォルダーからの取り出し、ピン留め・解除。
- 入力してアプリを検索。大文字・小文字、全角英数、ひらがな・カタカナの差を吸収。
- ファイル、フォルダー、Web サイトをピン留め。
- ローカルで更新する時計・日付タイル。
- 自分でJavaScriptを書くライブタイル。作成・インポート・編集、時計 / カウントダウンのサンプル。[開発ガイド](docs/javascript-tiles.md)を参照。
- 日本語 / English / システム言語に対応。設定の保存後すぐに切り替わり、アプリ名や自分で付けた名前は保持します。
- ダーク / ライト / システムテーマ、色付きタイル、壁紙背景、タイル倍率、グループ幅の変更。
- レイアウトの自動保存、バックアップ、JSON のインポート・エクスポート。
- ドキュメント、ピクチャ、アカウント設定、ロック、電源メニュー。
- サインイン時の自動起動は「設定 → 起動と操作」から選択。画面を開かず通知領域に常駐します。
- 通常起動はユーザー・ログオンセッションごとに1つ。別の場所の実行ファイルを起動しても既存の画面を開きます。プレビューは独立した検証モードです。

旧 UWP のライブ通知や Cortana、Windows Search 全体は含みません。タスクバーのスタートボタン捕捉は Windows の構成に依存する試験機能です。通常は Windows キーまたは `Ctrl+Alt+Space` を使います。

## 使い始める

1. [Releases](https://github.com/LastResolution/ReStart/releases) の Windows 用 ZIP をダウンロードします。非公開リポジトリのためアクセス権が必要です。
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
| `Shift` を押しながら別のタイルにドロップ | フォルダーにまとめる |
| グループ名をクリック | 名前を変更 |
| グループ名をドラッグ | グループの順序を変更 |
| 背景の空白を右クリック | グループやカスタムタイルを追加 |
| アプリ一覧を右クリック | 任意のグループにピン留め |
| 一覧の文字見出しをクリック | 文字を選んで移動 |

`Win+E` などは Windows 側に渡します。キーフックとの組み合わせは、IME・常駐アプリ・管理者アプリなど環境ごとの検証が必要です。困ったときは通知領域から終了するか、設定で Windows キーの捕捉を無効にしてください。

## データと削除

設定・レイアウトは `%LOCALAPPDATA%\ReStart\layout.json` に保存します。直前の保存が `layout.json.bak` に残ります。壊れたファイルは保全し、利用可能ならバックアップを読み込みます。将来の形式を古い ReStart で読み込んだ場合は上書きを禁止します。

レイアウトにはアプリ名、起動先、ファイルやフォルダーのパスが含まれます。エクスポートする際は共有内容を確認してください。インポートしても自動起動やキーフックの設定は引き継ぎません。

アンインストーラーは不要です。設定でサインイン時の起動をオフにし、通知領域から ReStart を終了して、展開したフォルダーを削除してください。設定も消す場合は上記のデータフォルダーを削除します。

ReStart 自体に広告・解析・自動更新通信はありません。クリックした Web サイトや外部アプリは、それぞれの動作に従います。

## ソースから開発する

必要なものは Windows と [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) です。SDK は `global.json` に固定し、プレビュー版を使いません。JavaScript 実行用の別プロセスに Jint を使用します。テストには xUnit を使います。

```powershell
git clone https://github.com/LastResolution/ReStart.git
cd ReStart
dotnet restore ReStart.slnx
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
- `v*` タグで x64 / ARM64 の配布物とチェックサムを作成します。0.x はプレリリースです。
- Actions の権限を絞り、外部 Actions はコミット SHA に固定します。
- Dependabot が NuGet と GitHub Actions の更新を提案します。

詳しくは [開発への参加](CONTRIBUTING.md)、[アーキテクチャ](docs/architecture.md)、[検証方法](docs/testing.md)、[リリース手順](docs/releasing.md) を参照してください。

バージョンごとの変更は [CHANGELOG](CHANGELOG.md)、実施済みの確認は [検証記録](docs/validation.md) にまとめています。

## 参考とライセンス

参考にした Microsoft の一次資料は [調査記録](docs/research.md) にまとめています。Microsoft のバイナリ、旧 Start のコードや画像は同梱していません。Windows および各アプリの名称・アイコンは各権利者に帰属します。ReStart は Microsoft とは独立したプロジェクトです。

ReStart のコードは [MIT License](LICENSE) です。
