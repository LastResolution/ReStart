# 検証

## 自動検証

```powershell
dotnet restore ReStart.slnx --locked-mode
dotnet format whitespace ReStart.slnx --verify-no-changes --no-restore
dotnet build ReStart.slnx -c Release --no-restore
dotnet test ReStart.slnx -c Release --no-build --logger trx --results-directory artifacts/test-results
./scripts/smoke.ps1
./scripts/test-navigation.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/test-navigation.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe -AppFolders
./scripts/test-placement.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
./scripts/package.ps1 -Runtime win-x64
```

スモークテストは隔離したプレビューで WPF を描画し、シェルの実アプリ数・アイコン数を確認します。タイルの表示名はデモ用に置き換え、Windows キー捕捉・トレイ・自動起動を使いません。`artifacts/smoke/start.png` と JSON を出力して終了します。JSON の success だけで全 UI 操作の成功とは扱いません。

`test-navigation.ps1` はメモリ上の架空アプリ288個を使い、800 / 1280 / 1920 / 2560 DIP幅で一覧の右端、画面外へのはみ出し、文字見出しの分断を検証します。一覧と検索から実際のHide / Presentを8回実行し、タイル表示・先頭位置・検索解除・オーバーレイ終了も確認します。プレビューはサイズを維持し、本番の設定・レイアウトは変更しません。結果JSONと各サイズのPNGは `artifacts/navigation/` に出力し、CIにも保存します。物理Windowsキーやタスクバー操作の代わりにはなりません。

CIのデスクトップより大きい幅も検証できるよう、ネイティブウィンドウの最大サイズには依存せず、実際のWPFルートへ描画幅を指定します。要求幅が反映されたことも検証します。

`-AppFolders` は架空の多段フォルダーを使用します。開閉、左右キー、AutomationのExpandCollapse、隠れたアプリの検索、1920 / 3840 DIP幅の見出し連続、ダーク / ライト、頭文字ジャンプ、再描画後の文字ジャンプ先、再表示時の折り畳みを確認します。物理Space入力やNarratorの読み上げを検証するものではありません。

`test-placement.ps1` は新しい実行ファイル用フォルダーを作り、プレビューの保存先も分離します。グループの独立した位置、空き領域からのグループ作成、タイルの空白保持、狭い画面から戻った際のアンカー、schema 2の保存を確認します。命名は実際のWPFダブルクリック・キーイベント経路を使い、Enter確定 / Esc取消とモーダルウィンドウが開かないことを検証します。OSの物理マウスドラッグを模擬するものではありません。

同じチェックで、フォルダーの行内展開・名前の直接編集・展開先への追加・取り出し・閉じた後の位置復元も検証します。`placement-board.png` と `placement-folder.png` は架空データの描画です。

## 実機チェックリスト

| 分類 | 確認 |
| --- | --- |
| 表示 | タスクバーが残る、左右上下が切れない、背景と文字のコントラスト |
| 画面 | 100 / 125 / 150 / 200% DPI、縦画面、異なる DPI の複数画面 |
| ピン留め | 一覧から追加、解除、4サイズ、再起動して保持 |
| グループ | 作成・命名・削除、ヘッダーで移動、折り返し |
| フォルダー | 中央で約1秒保持してドロップ、メニュー作成、行内開閉、子の取り出し、最後の子を移動 |
| 検索 | 英数、全角英数、かな、IME、候補なし、Enter、Esc |
| キーボード | Tab / 矢印 / Enter、Win 単押し、Win+E / D / L / Tab、Ctrl+Alt+Space |
| アプリ | Win32、Store、.lnk、.url、未インストール済みの古いピン留め |
| 設定 | ライト / ダーク / システム、ハイコントラスト、倍率、濃さ |
| 保存 | 正常再起動、壊れた JSON、バックアップ、未対応バージョン、書込不可 |
| 常駐 | 別ウィンドウへの切替、2回目起動、トレイ終了後のキー復帰 |
| 自動起動 | 設定でオン / オフ、移動した実行ファイル、サインイン |
| アクセシビリティ | Narrator、フォーカスの視認性、タッチ長押し |

電源操作は作業中のファイルに影響するため、通常のスモークテストでは実行しません。Windows キーなどの OS ショートカットを制限する UI 自動化ツールでは、それらを無理に実行せず、手動確認待ちとして記録します。

## 初回の検証環境

- Windows 11 x64、OS build 26200、.NET SDK 10.0.113。
- コアテストの件数と結果、UI 確認の結果はリリース時点の `validation.md` に記録します。
- Windows 10 と ARM64 の実機検証、複数モニター、すべての DPI 組み合わせは未実施です。

## 多重起動とスタートアップ

`./scripts/test-lifecycle.ps1 -Executable artifacts/publish/win-x64/ReStart.exe` は、2か所にコピーした配布物をプレビューとして10回起動し、残る常駐プロセスが1つであることを確認します。プレビューを先に終了して実行してください。テストは自分が生成したプロセスだけを後片付けします。

Core テストの自動起動登録検証は `HKCU/Software/ReStart.Tests/<GUID>` だけを使用し、本番 Run キーを変更しません。引用符付きパス、パス更新、解除、復元、無効なパスの拒否を検証します。

通常モードの設定 → 起動と操作で自動起動を選択し、保存後に再度開いて実登録と一致するか確認してください。オンの場合、再サインインで通知領域に常駐し、全画面が勝手に開かないことを確認します。Windows のスタートアップ設定や組織のポリシーで無効化されている場合は、それらの指定が優先します。ログオフを伴う自動検証は行いません。

`--background` の二度目起動は既存画面を開きません。通常の二度目起動は既存画面を表示します。`--smoke-test` で既存プレビューがあれば終了コード2を返し、古い結果を成功扱いしません。プレビューの設定画面では自動起動の変更を無効にしています。

## 入力直後の検索とタスクバー

`./scripts/smoke.ps1 -SearchQuery E` で検索画面を描画し、結果JSONとPNGを保存します。`-Executable <path>` を指定すると、ソースをビルドせず配布物を確認できます。デモデータでMicrosoft Edgeが候補に含まれ、検索入力が表示ツリーに存在することを確認します。このsmokeは検索クエリを設定する描画確認であり、物理キー入力の検証ではありません。

通常モードではWindowsキー単押し後にEを入力し、Edgeが候補に入ること、Enterで起動できることを手動で確認してください。IMEの確定、Backspace、Ctrl+F、Esc、再表示も対象です。タスクバーの空白・アプリボタン・複数画面側のタスクバーをクリックし、ReStartが閉じてクリック自体がWindowsに届くことも確認します。設定ダイアログやドラッグ中は閉じません。物理Windowsキーとタスクバークリックの通し検証は未実施です。
