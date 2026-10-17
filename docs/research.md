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
