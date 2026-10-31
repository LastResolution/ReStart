<p align="center"><img src="assets/ReStart.png" width="96" alt="ReStart" /></p>
<h1 align="center">ReStart</h1>
<p align="center"><strong>讓全螢幕開始功能表回歸。</strong><br>你的應用程式、你的配置、你自己的動態磚。</p>

<!-- languages:start -->
<p align="center">
  <a href="README.md">English</a> · <a href="README.de.md">Deutsch</a> · <a href="README.es.md">Español</a> · <a href="README.fr.md">Français</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a>
</p>
<!-- languages:end -->

<p align="center"><a href="https://lastresolution.github.io/ReStart/">網站（英文）</a> · <a href="https://lastresolution.github.io/ReStart/developers/">開發者指南（英文）</a></p>

> **AI 翻譯：** 本指南使用 AI 從英文翻譯，可能含有錯誤。[英文 README](README.md) 為主要參考，包含更多範例。歡迎協助修正。螢幕擷取畫面使用英文示範環境與範例資料。

<p align="center"><strong>觀看 60 秒示範</strong><br><sub>1080p · 英文 · 無音訊</sub></p>

https://github.com/user-attachments/assets/a893cd20-8c82-4c47-b5fb-bf22743f67e1

懷念 Windows 10 的全螢幕開始功能表？ReStart 在 Windows 11 和 Windows 10 上提供熟悉的應用程式空間。釘選常用內容，自由安排配置，再加入能以 JavaScript 撰寫的動態磚。

**可攜式 · JavaScript 動態磚 · x64 與 ARM64 · 八種語言 · 不必另外安裝 .NET**

**平板與工作列設定。** 啟動設定可在 ReStart 開啟時，僅顯示其所在螢幕上自動隱藏的工作列。選用平板整合預設關閉，保留配置與桌面開始快捷鍵；鍵盤偵測與工作列隱藏可分別設定。每個已放置磚的資料連線均需「儲存並授權」，存取要求變更後需要重新授權。[行為與驗證範圍（英文）](docs/fidelity.md)。

## 可靠性與私人備份

可暫停可見區域外的動態磚，並以有限次數的重試復原瀏覽器故障。兩項功能皆可單獨設定。**設定 → 配置與資料**可一併備份及還原配置、外觀、已安裝動態磚、草稿與背景。不含連線設定、認證資訊、照片資料夾路徑與快取，但仍包含應用程式名稱、啟動路徑及自訂原始碼與素材。請妥善保管私人備份。[英文指南](docs/backups.md)。

## 更多配置控制與創作空間

可鎖定全部配置或個別群組。Ctrl+Shift+L 切換全域鎖定；Ctrl+E 選取動態磚，Ctrl+方向鍵移動動態磚，Ctrl+Shift+方向鍵移動群組，Alt+1–4 更改大小，Ctrl+G 將所選動態磚分組，Shift+F10 開啟選單。仍支援復原/重做。亦可儲存獨立於桌面的背景圖片。

在**設定 → 外觀**中，可將背景濃度設為 **10–100%**，背景圖片模糊設為 **0–50**。模糊預設值為 **12**，設為 **0** 可顯示清晰圖片。高對比設定優先。按下**結束選取**或**已釘選的動態磚**可直接回到動態磚首頁。

外觀、動態磚縮放、群組寬度和語言可在儲存前預覽。未儲存時關閉會詢問儲存、捨棄或繼續編輯，捨棄會還原已儲存的顯示。主動下載的更新會保留準備就緒提醒，並於下次啟動時還原已驗證的準備狀態；一般啟動不會自動安裝。

內建 19 個 HTML/CSS/JavaScript 動態磚。Tile Studio 可編輯原始碼與資訊清單，以離線範例資料預覽四種大小，再儲存草稿或釘選。新增天氣風景、系統儀表板、RSS 新聞看板及 Canvas 花園；SVG 時鐘指針依電腦時間運作。動態磚名稱預設顯示，可在設定中隱藏。

拖曳時，動態磚、群組或多選項目的半透明副本會跟隨指標。藍色標記指示目標位置，被推開的動態磚也會在放下前預覽新位置。

![Tile Studio: HTML, CSS and JavaScript with four live previews](docs/images/tile-studio.png)

![Weather scene, System dashboard, News board and Canvas garden](docs/images/web-examples.png)

**編輯器操作：** 支援 JavaScript、HTML、CSS 和 JSON 語法醒目提示，<kbd>Tab</kbd> / <kbd>Shift</kbd> + <kbd>Tab</kbd> 縮排，以及 **Format document / Ctrl+Shift+F** 離線格式化；一次復原即可還原。Feed headlines 和 News board 在動態磚內部捲動。關閉的資料夾顯示大小一致的即時預覽。初始與重設配置使用隨附的 Month dots 和 Orbit clock。選用的啟動更新檢查會透過可關閉的通知提示新版本。

## 快速開始

1. Intel/AMD 電腦下載 **[x64 ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip)**；Windows on ARM 下載 **[ARM64 ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-arm64.zip)**。
2. 解壓縮**整個 ZIP**。保留 `ReStart.exe` 旁的 `TileHost`、`Examples` 與 `third-party`。
3. 執行 **ReStart.exe**，按 **Windows 鍵**後輸入應用程式名稱。也可用 <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>Space</kbd> 或通知區域選單開啟。

齒輪設定可選擇佈景主題、登入時啟動、Windows 鍵/開始按鈕整合與更新。一般使用不需要系統管理員權限。提供 [SHA-256 校驗值](https://github.com/LastResolution/ReStart/releases/latest)。

## Windows 應用程式整合

搜尋也包含 Windows 提供的設定關鍵字及控制台項目。選用 AQS 可使用原生檔案篩選及相關性排序；選取 **在 Windows 中搜尋** 後，才會將查詢交給 Windows 的其他搜尋來源。

Windows 應用程式使用次數，以及 Windows 動態磚與徽章的唯讀相容功能，是兩個獨立選項，預設皆關閉。支援常用尺寸配置、本機影像及資料繫結，不會恢復已停用的推播服務；沒有可用資料時會保留應用程式圖示。

[設定與相容性說明](docs/start-behavior.md) · [支援範圍](docs/fidelity.md)

## 功能

![ReStart 的開啟與關閉動畫](docs/images/start-motion.gif)

**預設使用群組，需要時自由放置。** 在空白處放下會建立未命名群組；在磚上停留會建立資料夾。拖曳時按住 <kbd>Shift</kbd> 可重新排列而不建立資料夾，按住 <kbd>Alt</kbd> 可在空白處獨立放置。Settings → Start behavior → **Use free tile placement** 可將獨立放置設為預設。既有配置和資料夾保持不變。相鄰磚平滑移位，指標下的目標判定保持穩定。

拖曳時，畫面底部會短暫顯示快捷鍵提示，可在開始功能表行為設定中關閉。按 <kbd>F1</kbd>，或在設定中開啟 **鍵盤快捷鍵**，即可查看完整清單。


| 類別 | 包含功能 |
| --- | --- |
| 開啟與關閉 | 選擇性接管 Windows 鍵及工作列開始按鈕、Ctrl+Alt+Space、通知區域控制、前景啟用、切換應用程式/工作列時關閉、依螢幕工作區域顯示、單一執行個體 |
| 啟動 | 選擇性登入啟動、背景模式、跟隨主題的準備畫面與預先輸入、圖示快取、背景應用程式目錄重新整理 |
| 動態磚與群組 | 四種大小、獨立釘選、依內容調整大小的可移動矩形群組、水平半格錨點、自動放置、雙向捲動 |
| 資料夾 | 重疊並等待建立、原位展開、新增/移除/排序、就地重新命名及解散；只剩一個項目仍保留資料夾 |
| 配置編輯 | 編輯模式、多重選取、批次移動、工作階段內復原/重做；取消拖曳保留原配置 |
| 拖曳回饋 | 區分群組/資料夾目標、停留進度、選擇性提示及群組邊界、邊緣自動捲動；可關閉自動群組/資料夾建立 |
| 釘選內容 | 應用程式、檔案、目錄、捷徑、網站；檔案總管 **Pin to ReStart**、右鍵選單、從應用程式清單拖曳、明確指定目標 |
| 搜尋 | 正規化應用程式名稱、執行檔名和系統宣告的別名；63 個本地化 Windows 設定頁面及英文別名，可選的索引檔名及需主動啟用的文件內容搜尋（最多 50 個本機結果），方向鍵/Enter 導覽與過期查詢取消 |
| Windows 工作階段 | 透過 Windows 登入畫面切換使用者；依裝置能力和原則顯示睡眠、休眠，提供關機、重新啟動及 Windows 更新等待重新啟動時的更新電源操作 |
| 所有應用程式 | 使用 Windows 語言分類的回應式欄位（假名、韓文聲母、拼音和筆畫）、展開開始功能表目錄、可捲動的字母選擇器、Unicode 首字導覽、即時反映安裝/解除安裝、最近新增與選擇性最常使用 |
| 應用程式命令 | 依情境顯示選單：開始畫面的「更多」、搜尋中的直接操作、檔案/資料夾位置、已安裝應用程式管理/評分及 Windows 允許的命令。選用的最近/常用文件有獨立操作；也可隱藏應用程式歷程項目並個人化導覽清單。[選單與 Windows 10 支援範圍](docs/context-menus.md) 亦支援應用程式註冊的工作，以及與 Windows 共用的跳躍清單釘選與取消釘選。歷程、工作和釘選項目可分別設定顯示。 共用僅適用於受支援 Windows 系統上可存取的實際檔案；內容適用於存在的檔案、資料夾或原始捷徑。無法使用的操作會隱藏。 應用程式自訂跳躍清單類別保留其註冊名稱與原始動作。 |
| 啟動選項 | 每個動態磚獨立啟用、引數、工作目錄、支援桌面程式/捷徑的系統管理員執行；UAC 由 Windows 處理 |
| 導覽 | 選擇性暫留展開、個別選擇文件/下載/音樂/圖片/影片/網路/個人資料夾、帳戶及電源操作 |
| 外觀 | 五種色系各有明暗變體、系統模式、自訂 RGB 背景/輔色、桌布、開始畫面著色、個別磚色彩、主題控制項/捲軸、高對比、選擇性動畫 |
| 動態磚 | 19 個 JavaScript 範例、四種回應式大小、本機點擊動作、即時更新開關、資料設定、建立範例、匯入套件、自動監看本機程式庫 |
| 動態磚 API | 電腦時間、語言/大小、保留狀態、CPU/記憶體/網路/電池/儲存、選定照片、快取 JSON/Web API/RSS/Atom/檔案輸入、本機程式整合 |
| 語言 | 英文、日文、德文、法文、西班牙文、韓文、簡體中文、繁體中文，包括內建動態磚文字 |
| 資料與更新 | 本機設定、配置匯入/匯出/重設、備份與移轉、可清除的程式活動、版本/組建/授權、免 API 金鑰檢查、具驗證及復原的可攜更新、診斷報告預覽 |
| 發行 | Windows 10/11 x64 與 ARM64 可攜式套件，內含執行階段及聲明，可還原整合且不修改 Explorer |

[開關與預設值（英文）](docs/start-behavior.md) · [相容性（英文）](docs/fidelity.md)

**實用的初始配置。** 首次啟動及重設配置時，會依應用程式識別碼與精確的本地化名稱，按用途選擇已安裝的應用程式。數量不足時，以不重複的內建離線動態磚補足，讓每組至少有六個動態磚。已儲存的配置維持不變。[選擇與重設方式（英文）](docs/start-behavior.md#initial-layout-and-reset)。

**實際量測的驗證範圍。** 原生檢查驗證兩個螢幕在 100%/125%/150% 縮放及橫直螢幕混合配置下的視窗位置與尺寸。協助工具提供者已在八種語言、兩種佈景主題下檢查；Narrator 的基本播報內容已透過語音回顧確認。在獨立的測試介面中，也已透過實機確認觸控點按、長按選單、動態磚與群組拖曳、資料夾建立和捲動。螢幕重新連接、其他觸控情境和螢幕閱讀程式的完整互動仍需額外實機驗證。[結果與裝置涵蓋範圍（英文）](docs/fidelity.md#observed-platform-coverage)。

## 搜尋與設定

![應用程式、Windows 設定及本機檔案搜尋結果](docs/images/unified-search.png)

在 **Settings → Start behavior** 可分別關閉設定/檔案搜尋、文件選單、暫留效果、自動群組/資料夾、邊緣捲動與拖曳提示。最常使用程式的記錄預設關閉。

| 開始功能表行為 | 個別動態磚啟動選項 |
| --- | --- |
| ![行為設定](docs/images/start-behavior.png) | ![引數、工作目錄及系統管理員執行](docs/images/launch-options.png) |

關閉 **Use custom launch options** 仍會保留填入的欄位。未啟用時，捷徑維持 Windows 的一般啟動設定。

## 動態磚與資料

![內建動態磚](docs/images/live-tiles.png)

- **時間與專注：** Orbit clock、Digital clock、Month dots、Focus cycle、Year in motion、Countdown。
- **電腦：** CPU pulse、Memory flow、Network flow、Battery、Storage。
- **照片：** Photo moments，指定資料夾，按一下切換下一張。
- **資料連線：** Weather、Feed headlines、JSON monitor。

在背景按右鍵 → **Add live tile**。按一下會執行磚內動作，例如暫停計時或切換照片。Weather/Feed/JSON 可在 **Configure data sources…** 填入網址或選擇檔案後儲存。**Refresh data now** 可要求最新資料。

**天氣初始採用柏林範例座標，不是電腦位置。** ReStart 不會推測所在地區。依[天氣指南](docs/javascript-tiles.md#weather-location)變更座標與顯示名稱。認證資料依目前 Windows 使用者加密，不包含在配置匯出中。連線失敗時可顯示快取，隱藏開始畫面後停止讀取。

Web 動態磚包含 manifest.json、index.html、tile.css、tile.js 及選用 assets。在四種固定大小內可自由使用 HTML/CSS、SVG、Canvas 與透明背景，並相容原生 JS 套件。需要 WebView2 Runtime；直接連網須宣告能力並逐磚授權，不提供通用檔案、CLR 或 shell 橋接。 [HTML/CSS/JavaScript API](docs/javascript-tiles.md) · [Examples](examples/tiles)

## 操作

| 操作 | 結果 |
| --- | --- |
| Windows 鍵 / <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>Space</kbd> | 開啟或關閉 |
| 輸入 / <kbd>Ctrl</kbd> + <kbd>F</kbd> | 搜尋；Enter 啟動反白結果，方向鍵變更選取 |
| <kbd>Esc</kbd> | 依序關閉搜尋、資料夾或覆疊，再關閉開始畫面 |
| 拖曳動態磚 / 群組標題 | 移動；空白處建立未命名群組 |
| <kbd>Alt</kbd> + 拖到空白處 | 僅本次獨立放置 |
| <kbd>Shift</kbd> + 拖曳 | 移動相鄰磚以重新排列，不觸發重疊群組或資料夾操作 |
| 在磚上重疊約 0.6 秒 | 預覽就緒後建立資料夾；既有資料夾可接收新增項目 |
| 側邊欄/背景選單的 **選取磚塊** / <kbd>Ctrl</kbd> + <kbd>E</kbd> | 多重選取而不開啟程式；平時也可以直接拖曳磚塊 |
| 編輯時 <kbd>Ctrl</kbd> + <kbd>A</kbd> / <kbd>Delete</kbd> | 選取根磚 / 取消釘選所選項目 |
| <kbd>Ctrl</kbd> + <kbd>Z</kbd> / <kbd>Ctrl</kbd> + <kbd>Y</kbd> 或 <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Z</kbd> | 復原 / 重做配置 |
| 鉛筆 / 按兩下名稱 | 就地重新命名群組或資料夾 |
| <kbd>Shift</kbd> + 滾輪 | 水平捲動 |

Windows 組合鍵會轉送給系統。配置歷程在工作階段內保留最多 100 次變更、上限 32 MiB；文字欄位保有自己的文字復原。再次開啟會回到釘選首頁。

## 設定、隱私與更新

設定位於 `%LOCALAPPDATA%\ReStart\layout.json`。選擇性的程式活動另外存於 `activity.json`，可清除。移轉會保留備份，參閱[說明（英文）](docs/migration.md)。About 顯示版本、組建與授權。

更新檢查讀取公開 GitHub Releases，免 API 金鑰。在設定 → 更新中選擇「下載更新」，驗證後選擇「重新啟動並更新」。可寫入資料夾內完整、未修改的可攜套件支援驗證更新，並在安裝或啟動失敗時還原先前檔案。開發版本及舊套件仍可手動替換。關於 → 診斷資訊可預覽並複製不含路徑、程式清單、磚資料、認證資訊或原始記錄的報告。不會自動上傳。[更新及診斷（英文）](docs/updates.md)。位置變更時重新儲存啟動註冊與檔案總管整合。沒有廣告或使用分析，不上傳程式清單、配置或照片選擇。設定的資料提供者會收到相應請求。[安全（英文）](SECURITY.md)

移除時關閉登入啟動與檔案總管整合，從通知區域退出並刪除安裝目錄。只有同時要刪除設定與自訂磚時，才移除本機資料目錄。

## 自行建置

使用 Windows 與 `global.json` 選定的穩定版 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。覆寫檔案前先關閉執行中的程式。

```powershell
git clone https://github.com/LastResolution/ReStart.git
cd ReStart
dotnet restore ReStart.slnx --locked-mode
dotnet build ReStart.slnx -c Release --no-restore
dotnet test ReStart.slnx -c Release --no-build
.\src\ReStart.App\bin\Release\net10.0-windows\ReStart.exe
# 隔離預覽：
dotnet run --project src/ReStart.App -c Release -- --preview
# 含執行階段的可攜式套件：
./scripts/package.ps1 -Runtime win-x64
```

ARM64 使用 `win-arm64`。ZIP 與校驗檔產生於 `artifacts/packages`。請始終發行完整 ZIP。

[問題與作品分享](https://github.com/LastResolution/ReStart/discussions) · [回報缺陷](https://github.com/LastResolution/ReStart/issues/new/choose) · [參與貢獻（英文）](CONTRIBUTING.md) · [架構（英文）](docs/architecture.md) · [測試（英文）](docs/testing.md) · [發行](https://github.com/LastResolution/ReStart/releases)

ReStart 是受後期 Windows 10 全螢幕開始功能表啟發的獨立 MIT 專案，與 Microsoft 無關。圖片顯示真正程式的英文示範、合成指標與產生的照片色塊。名稱和圖示屬於各權利人。[MIT 授權](LICENSE) · [第三方聲明（英文）](docs/third-party.md) · [支援（英文）](SUPPORT.md)

---

<!-- support:start -->
<h2 align="center">支持獨立開發</h2>
<p align="center">
  喜歡 ReStart？歡迎支持 LastResolution 持續改善 ReStart，並開發更多實用工具。<br>
  <sub>ReStart 免費且開源，支持完全自願。</sub>
</p>
<p align="center">
  <a href="https://buymeacoffee.com/LastResolution"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" height="48" alt="Buy Me a Coffee · LastResolution" /></a>
</p>
<!-- support:end -->
