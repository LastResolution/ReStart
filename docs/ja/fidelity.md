# 互換性

[English](../fidelity.md)

ReStart は後期 Windows 10 のフルスクリーン Start に着想を得た独立実装です。Microsoft の Start コードやバイナリを含めず、Explorer を置き換えません。機能の実装はアニメーションや内部仕様の完全一致を意味しません。

Windows 10 / 11、x64 / ARM64 向けのビルドを提供します。開発・ネイティブ自動検証は Windows 11 x64 で実施します。ARM64 実機、Windows 10 22H2、混在 DPI・複数画面、タッチ、Narrator、IME・物理ショートカット全経路は実機での確認が必要です。WPF のイベントによる検証と物理操作は区別します。

4サイズのタイル、グループ、行内フォルダ、アプリ検索、テーマ、壁紙、解除可能なデスクトップ連携に対応します。単独タイル、内容に合わせたグループ幅、半セル横配置、横スクロール、タイル個別色は ReStart の拡張です。

Windows Search 全体、Web / Cortana、旧 UWP タイル通知、Jump Lists、Windows の最近使用 / 使用頻度履歴、Windows の配置の直接取り込みは含みません。更新確認は認証なしで公開 GitHub Releases を参照し、インストールは手動です。

[検証手順](testing.md)、[JS API](javascript-tiles.md)、一次資料を含む[英語版の対応表](../fidelity.md)を参照してください。
