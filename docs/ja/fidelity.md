# 互換性

[English](../fidelity.md)

ReStart は後期 Windows 10 のフルスクリーン Start に着想を得た独立実装です。Microsoft の Start コードやバイナリを含めず、Explorer を置き換えません。機能の実装はアニメーションや内部仕様の完全一致を意味しません。

Windows 10 / 11、x64 / ARM64 向けのビルドを提供します。開発・ネイティブ自動検証は Windows 11 x64 で実施します。ARM64 実機、Windows 10 22H2、混在 DPI・複数画面、タッチ、Narrator、IME・物理ショートカット全経路は実機での確認が必要です。WPF のイベントによる検証と物理操作は区別します。

4サイズのタイル、グループ、行内フォルダ、アプリ検索、テーマ、壁紙、解除可能なデスクトップ連携に対応します。単独タイル、内容に合わせたグループ幅、半セル横配置、横スクロール、タイル個別色は ReStart の拡張です。

単独の通常タイルへの重なりでグループを作る動作も ReStart の拡張です。グループ内の重なりはフォルダー作成、既存フォルダーは中への追加になります。[Microsoft の Windows 10 向け案内](https://support.microsoft.com/en-au/windows/experience/personalization/customize-the-windows-start-menu)に合わせ、1つ残ったフォルダーは維持し、すべて取り出したときに削除します。

Windows Search 全体、Web / Cortana、旧 UWP タイル通知、Jump Lists、Windows の最近使用 / 使用頻度履歴、Windows の配置の直接取り込みは含みません。更新確認は認証なしで公開 GitHub Releases を参照し、インストールは手動です。

明示的な編集モード、複数選択の一括移動、起動中の配置 Undo / Redo は ReStart の拡張です。

開閉時は背景のフェード、下端を基準にした拡大、下のタイルから順に現れる動きを分けています。終了時も縮小とフェードを行い、途中の再表示は古い終了処理を取り消します。アプリと Windows のアニメーション設定、ハイコントラストを尊重します。公式 GIF に基づく独立した調整値であり、Windows 10 22H2 実機とのフレーム単位の一致は未確認です。入力時は表示アニメーションを完了してすぐ操作できます。

[検証手順](testing.md)、[JS API](javascript-tiles.md)、一次資料を含む[英語版の対応表](../fidelity.md)を参照してください。
