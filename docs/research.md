# Windows 10 全画面スタートの調査

調査日: 2026-10-17（日本時間）。再現の基準は Windows 10 後期の「全画面表示のスタート」で、旧版の色付きタイルを選択肢として残します。Windows 8 の横スクロールするスタート画面や、タブレットモード全体の置き換えとは区別します。

## 一次資料

| 資料 | 確認した仕様 | ReStart への反映 |
| --- | --- | --- |
| [Windows 10 Tip: How to make Start full screen](https://blogs.windows.com/windowsexperience/2017/09/11/windows-10-tip-make-start-full-screen/)（2017-09-11） | 全画面のピン留め表示と、左のボタンから切り替える全アプリ表示。埋め込み画像で左バー・背景・グループの配置を確認 | 通常表示では余分なヘッダーを置かず、左バーで表示切り替え |
| [Customize the Windows Start menu](https://support.microsoft.com/en-gb/windows/experience/personalization/customize-the-windows-start-menu?nochrome=true) | アプリ一覧、タイル、サイズとグループ、ピン留め | アプリ列挙・メニュー・編集操作 |
| [Customize the Start layout for managed Windows devices](https://learn.microsoft.com/en-us/windows/configuration/start/layout) | 論理グリッド、名前付きグループ、UWP / デスクトップ / フォルダーの区別 | 永続モデルとタイル配置 |
| [Customize the Start layout](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/customize-the-start-screen) | OEM 配置、座標、タイル形状とバージョンによる差 | Small / Medium / Wide / Large の配置検証 |
| [Announcing Windows 10 Insider Preview Build 10547](https://blogs.windows.com/windows-insider/2015/09/18/announcing-windows-10-insider-preview-build-10547/) | 3列と4列の中タイル幅の選択 | グループ幅の設定 |
| [Use a screen reader to explore and navigate the Start menu](https://support.microsoft.com/en-us/accessibility/windows/use-a-screen-reader-to-explore-and-navigate-the-start-menu-in-windows) | キーボードでの移動とフォルダーの扱い | WPF ボタン・フォーカス・アクセシビリティ名 |

## 実装に使用した Windows API 資料

- [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc): フックは短時間で処理し、専用スレッドとメッセージループで動かします。UI 処理は Dispatcher に渡します。
- [Launching applications](https://learn.microsoft.com/en-us/windows/win32/shell/launch): シェルによる起動。コマンド文字列を評価しません。
- [Shell functions](https://learn.microsoft.com/en-us/windows/win32/shell/functions): シェル項目と画像の取得。
- [WPF overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/): Windows ネイティブ UI、描画と入力。

## 調査と検証の区別

Microsoft の資料と当時の画像は、構造・操作の根拠です。Windows 10 実機での全操作の並行比較はまだ行っていません。座標やアニメーションのフレーム単位の一致を保証するものではありません。壁紙のぼかし、グループの折り返し、アプリ検索の順位は ReStart 側の実装です。

元資料の画像や Windows の実装コードは配布物へ転載しません。README の画面は ReStart のプレビューで生成します。

### 配置・名前編集の再調査（2026-10-18）

- [管理対象デバイスのStart配置](https://learn.microsoft.com/en-us/windows/configuration/start/layout)は、空き領域へのドラッグによるグループ作成と、グループ上のName group入力欄を説明しています。グループ名変更に別ウィンドウを開く実装は置き換えます。
- [Windows 10用OEM配置](https://learn.microsoft.com/en-us/windows-hardware/customize/desktop/customize-start-layout)は、4種類のタイル寸法とグループ内のRow / Columnを定義しています。余白を保持する行・列の配置を基準にします。
- [タイルフォルダーの名前](https://blogs.windows.com/windowsexperience/2018/12/03/windows-10-tip-name-your-tile-folders/)は、タイルを重ねて約1秒保持してフォルダーを作り、展開後に名前を付ける操作を説明しています。Shift必須の操作とモーダルな表示は見直し対象です。
- グループ名のダブルクリック編集はユーザーの実操作報告に基づきます。グループの全画面内の移動範囲、スナップ間隔、衝突時アニメーションはWindows 10実機との追加照合が必要です。
- [2018年の公式デモGIF](https://blogs.windows.com/wp-content/uploads/prod/sites/2/2018/10/d48e2dc05daefba99b5170de49dc2f1e.gif)で、親フォルダータイルが上向き矢印になり、名前と内容がグループの行間に展開され、後続タイルを下へ送る表示を確認しました。ReStartの中央オーバーレイを廃止し、この構造へ変更します。資料画像は配布物へ含めません。

### 全アプリ一覧の階層と識別（2026-10-18）

- [スクリーンリーダーによるStartの操作](https://support.microsoft.com/en-us/accessibility/windows/use-a-screen-reader-to-explore-and-navigate-the-start-menu-in-windows)のWindows 10節で、折り畳まれたアプリフォルダーをSpaceで開き、内容を通常の一覧として操作する手順を確認しました。同じページのWindows 11節とは分けて参照します。
- [ShellFolderItem.ExtendedProperty](https://learn.microsoft.com/en-us/windows/win32/shell/shellfolderitem-extendedproperty)、[System.Link.TargetParsingPath](https://learn.microsoft.com/en-us/windows/win32/properties/props-system-link-targetparsingpath)、[System.Link.Arguments](https://learn.microsoft.com/en-us/windows/win32/properties/props-system-link-arguments)を使い、AUMIDとリンク先・引数を読み取ります。読み取り時にリンクの解決や実行は行いません。
- [SHGetFileInfoW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shgetfileinfow)のDISPLAYNAMEを用いてローカライズされた表示名を取得します。MAX_PATHを超える項目と取得失敗は実ファイル名へフォールバックします。
- 同名アプリの照合順序、列の均等化、フォーカス復元はReStart側の実装です。Windows 10内部アルゴリズムと一致することを示す資料ではありません。

### 多重起動と自動起動

- [Run / RunOnce registry keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys): ユーザー単位のサインイン時起動、コマンド260文字制限、起動時刻の保証がない点を確認。
- [Mutex constructors](https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex.-ctor?view=net-10.0): 名前付きオブジェクトの新規作成判定と寿命を利用。実行ファイルの場所によらず同じ名前を使用。

### JavaScriptタイル

- [Jint v4.17.0](https://github.com/sebastienros/jint/tree/v4.17.0) と [制約の説明](https://sebastienros.github.io/jint/guide/constraints): エンジン制限だけではBCL処理を強制停止できないため、外側にプロセス期限を設ける。採用版はNuGetの安定版に固定。
